#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using DxfContourStudio.Application.Commands;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Application.Editing;
using DxfContourStudio.Core.Geometry;

namespace DxfContourStudio.Application.Interaction;

/// <summary>
/// Pure interaction state machine for the editing tools (Join / Break /
/// Trim / Extend) — no WPF dependency (D17). The view layer feeds world
/// pointer events plus the current snap result; the session replies with a
/// <see cref="ToolOverlayState"/> for the canvas overlay, localized status
/// keys for the status bar, and finally requests <see cref="ICommand"/>s that
/// the view model executes through the shared undo history.
///
/// The session never mutates the document itself: it only plans and requests.
/// This keeps the whole interaction testable without a UI and guarantees the
/// "no mutation on hover" contract — mouse moves only recompute the overlay.
///
/// Gesture semantics per tool:
/// - Join: two clicks on open endpoints (Line/Arc/open Polyline; Circle and
///   closed Polyline refuse). The first pick is remembered; the hover preview
///   shows the prospective connection. Commit = <see cref="JoinEntitiesCommand"/>.
/// - Break: one click picks the target and cuts it at the clicked point
///   (intersection snap preferred). Endpoint clicks are refused. Commit =
///   <see cref="BreakEntityCommand"/>.
/// - Trim: the first click picks the target path (Line/Arc), every hover then
///   previews the section under the cursor, a second click commits the
///   removal. Commit = <see cref="TrimSectionCommand"/>.
/// - Extend: the first click picks the target and decides the extended end by
///   proximity, hover previews the extension to the nearest valid boundary,
///   a second click commits. Commit = <see cref="TrimExtendCommand"/>.
///
/// After a successful commit the session stays in the same tool (continuous
/// mode, v0.3.0 UX overhaul): only the pending gesture is cleared so the user
/// can chain joins/breaks/trims without re-picking the tool. It raises
/// <see cref="ResultEntitySelected"/> with the id that survives the operation
/// (join result / kept piece / extended entity) so the view model can select
/// it.
/// </summary>
public sealed class EditToolSession
{
    private readonly CadDocument _document;
    private readonly GeometryTolerance _tolerance;

    /// <summary>Minimum length of a non-empty kept piece after a section trim (mm).</summary>
    private const double MinKeptPiece = 1e-6;

    /// <summary>Join: the first picked open endpoint (null while no first pick exists).</summary>
    private JoinEndpointResolver.OpenEndpoint? _joinFirst;

    /// <summary>Trim: the target entity chosen by the first click.</summary>
    private long? _trimTargetId;

    /// <summary>Extend: the target entity and the side chosen by the first click.</summary>
    private long? _extendTargetId;
    private TrimSide _extendSide;

    /// <summary>Id of the entity that will survive the pending commit (select after success).</summary>
    private long? _pendingResultId;

    /// <summary>
    /// Trim: the last hover plan plus the cursor that produced it. When the
    /// cursor has not moved beyond the pick tolerance since, hover and commit
    /// reuse this plan so the preview is exactly what gets removed (no
    /// boundary flip between preview and commit).
    /// </summary>
    private TrimSectionOutcome? _trimStickyPlan;
    private Point2 _trimStickyCursor;

    /// <summary>Last status key raised — identical consecutive keys are not re-raised (anti-storm).</summary>
    private string? _lastStatusKey;

    /// <summary>The tool currently active; <see cref="ToolMode.Select"/> means no tool owns the mouse.</summary>
    public ToolMode ActiveTool { get; private set; } = ToolMode.Select;

    /// <summary>What the active tool wants to draw on the canvas for the current cursor position.</summary>
    public ToolOverlayState Overlay { get; private set; } = ToolOverlayState.Empty;

    /// <summary>True while a tool gesture is waiting for its second click.</summary>
    public bool HasPendingTarget => _joinFirst is not null || _trimTargetId is not null || _extendTargetId is not null;

    /// <summary>Raised whenever <see cref="Overlay"/> changed (the view repaints).</summary>
    public event Action? OverlayChanged;

    /// <summary>Raised whenever <see cref="ActiveTool"/> changed (the UI mirrors tool state).</summary>
    public event Action? ActiveToolChanged;

    /// <summary>Requests a localized status message (key, not text — UI-owned dictionary).</summary>
    public event Action<string>? StatusKeyRequested;

    /// <summary>Requests the view model to execute one undoable command.</summary>
    public event Action<ICommand>? CommandRequested;

    /// <summary>Raised after a successful commit with the id to select.</summary>
    public event Action<long>? ResultEntitySelected;

    public EditToolSession(CadDocument document, GeometryTolerance tolerance)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _tolerance = tolerance ?? throw new ArgumentNullException(nameof(tolerance));
    }

    /// <summary>
    /// Activates a tool. Any pending gesture of the previous tool is
    /// cancelled and the overlay cleared (tool switch never runs through the
    /// normal selection flow).
    /// </summary>
    public void ActivateTool(ToolMode mode)
    {
        if (mode == ActiveTool)
        {
            return;
        }

        ClearPending();
        ActiveTool = mode;
        SetOverlay(ToolOverlayState.Empty);
        _lastStatusKey = null;
        ActiveToolChanged?.Invoke();
        if (mode != ToolMode.Select)
        {
            // Activation hint so the user knows what the first click does
            // (v0.3.0 UX overhaul, P3-18).
            RequestStatus(ActivationHintKey(mode));
        }
    }

    /// <summary>
    /// Esc handling: cancels the pending gesture (one step back). Returns
    /// false when there is nothing pending, so the caller can fall back to
    /// leaving the tool / clearing the selection.
    /// </summary>
    public bool Cancel()
    {
        if (!HasPendingTarget)
        {
            return false;
        }

        ClearPending();
        SetOverlay(ToolOverlayState.Empty);
        return true;
    }

    /// <summary>Abandons every pending gesture and overlay (document swap, tool switch).</summary>
    public void ClearToolState()
    {
        ClearPending();
        _pendingResultId = null;
        ActiveTool = ToolMode.Select;
        SetOverlay(ToolOverlayState.Empty);
        ActiveToolChanged?.Invoke();
    }

    /// <summary>Called by the view model after it executed a requested command.</summary>
    public void NotifyCommandCompleted(bool success)
    {
        if (success)
        {
            long? resultId = _pendingResultId;
            _pendingResultId = null;
            ClearPending();
            SetOverlay(ToolOverlayState.Empty);
            // Continuous mode: the tool stays active so the user can chain
            // operations without re-selecting the tool (v0.3.0 UX overhaul).
            if (resultId is long id)
            {
                ResultEntitySelected?.Invoke(id);
            }
        }
        else
        {
            // The commit failed on the view-model side — the gesture was
            // consumed but no mutation happened. Keep the tool for retry.
            _pendingResultId = null;
            ClearPending();
            SetOverlay(ToolOverlayState.Empty);
        }
    }

    /// <summary>Hover pipeline — purely recomputes the overlay, never mutates.</summary>
    public void OnPointerMoved(Point2 world, SnapResult snap, double pickToleranceWorld)
    {
        switch (ActiveTool)
        {
            case ToolMode.Join:
                JoinHover(world, pickToleranceWorld);
                break;

            case ToolMode.Break:
                BreakHover(world, snap, pickToleranceWorld);
                break;

            case ToolMode.Trim:
                TrimHover(world, snap, pickToleranceWorld);
                break;

            case ToolMode.Extend:
                ExtendHover(world, snap, pickToleranceWorld);
                break;

            default:
                SetOverlay(ToolOverlayState.Empty);
                break;
        }
    }

    /// <summary>
    /// Left-click handling. Returns true when the click was consumed by the
    /// tool (empty-space selection behaviour must not run in tool modes).
    /// </summary>
    public bool OnPointerLeftDown(Point2 world, SnapResult snap, double pickToleranceWorld)
    {
        switch (ActiveTool)
        {
            case ToolMode.Join:
                JoinClick(world, pickToleranceWorld);
                break;

            case ToolMode.Break:
                BreakClick(world, snap, pickToleranceWorld);
                break;

            case ToolMode.Trim:
                TrimClick(world, snap, pickToleranceWorld);
                break;

            case ToolMode.Extend:
                ExtendClick(world, snap, pickToleranceWorld);
                break;

            default:
                return false;
        }

        return true;
    }

    /// <summary>Mouse left the canvas — clear the preview overlay but keep the pending gesture.</summary>
    public void OnPointerLeft()
    {
        if (ActiveTool is ToolMode.Join or ToolMode.Break or ToolMode.Trim or ToolMode.Extend)
        {
            SetOverlay(ToolOverlayState.Empty);
        }
    }

    /// <summary>
    /// Called by the view model after any document mutation (command executed,
    /// undo/redo, file swapped): the pending gesture may reference stale
    /// entities, so everything is abandoned and the overlay cleared. The
    /// active tool stays active.
    /// </summary>
    public void OnDocumentChanged()
    {
        ClearPending();
        SetOverlay(ToolOverlayState.Empty);
    }

    // ---------------------------------------------------------------- Join

    private void JoinHover(Point2 world, double pickToleranceWorld)
    {
        if (_joinFirst is not { } first)
        {
            // First step: show the open endpoint under the cursor so the
            // user sees what a click will pick (v0.3.0 UX overhaul).
            JoinEndpointResolver.OpenEndpoint? candidate = ResolveEndpoint(world, pickToleranceWorld);
            if (candidate is null)
            {
                SetOverlay(ToolOverlayState.Empty);
                return;
            }

            SetOverlay(new ToolOverlayState(ToolPreviewKind.Normal, [], [candidate.Value.Point], candidate.Value.EntityId));
            return;
        }

        JoinEndpointResolver.OpenEndpoint? second = ResolveEndpoint(world, pickToleranceWorld);
        if (second is null)
        {
            SetOverlay(ToolOverlayState.Empty);
            return;
        }

        bool sameEndpoint = second.Value.EntityId == first.EntityId && second.Value.ParamIndex == first.ParamIndex;
        var preview = new ToolOverlayState(
            sameEndpoint ? ToolPreviewKind.Invalid : ToolPreviewKind.Normal,
            [new LineSegment(first.Point, second.Value.Point)],
            [first.Point, second.Value.Point],
            first.EntityId);
        SetOverlay(preview);
    }

    private void JoinClick(Point2 world, double pickToleranceWorld)
    {
        JoinEndpointResolver.OpenEndpoint? endpoint = ResolveEndpoint(world, pickToleranceWorld);
        if (endpoint is null)
        {
            RequestStatus("EditTools.Join.NoEndpoint");
            return;
        }

        if (_joinFirst is null)
        {
            _joinFirst = endpoint;
            SetOverlay(new ToolOverlayState(ToolPreviewKind.Normal, [], [endpoint.Value.Point], endpoint.Value.EntityId));
            RequestStatus("EditTools.Join.PickSecond");
            return;
        }

        JoinEndpointResolver.OpenEndpoint first = _joinFirst.Value;
        if (endpoint.Value.EntityId == first.EntityId && endpoint.Value.ParamIndex == first.ParamIndex)
        {
            RequestStatus("EditTools.Join.SameEndpoint");
            return;
        }

        if (!TryCommitJoin(first, endpoint.Value))
        {
            // pending stays so the user can pick another second endpoint.
        }
    }

    private bool TryCommitJoin(JoinEndpointResolver.OpenEndpoint first, JoinEndpointResolver.OpenEndpoint second)
    {
        IGeometryEntity? primary = _document.GetEntityById(first.EntityId);
        IGeometryEntity? secondary = _document.GetEntityById(second.EntityId);
        if (primary is null || secondary is null)
        {
            ClearPending();
            SetOverlay(ToolOverlayState.Empty);
            return false;
        }

        JoinAttempt attempt = JoinEngine.TryJoin(primary, secondary, primary.Id, _tolerance);
        if (!attempt.IsValid || attempt.Joined is null)
        {
            RequestStatus(JoinReasonKey(attempt.Reason));
            return false;
        }

        var command = new JoinEntitiesCommand(_document, primary.Id, secondary.Id, _tolerance);
        _pendingResultId = primary.Id;
        ClearPending();
        SetOverlay(ToolOverlayState.Empty);
        CommandRequested?.Invoke(command);
        return true;
    }

    // ---------------------------------------------------------------- Break

    private void BreakHover(Point2 world, SnapResult snap, double pickToleranceWorld)
    {
        Point2 cutPoint = snap.IsValid && snap.Kind == SnapKind.Intersection ? snap.WorldPoint : world;
        IGeometryEntity? target = PickClosest(cutPoint, pickToleranceWorld);
        if (target is null || !PathBreaker.TryProjectParameter(target, cutPoint, pickToleranceWorld, out double t, out Point2 projected))
        {
            SetOverlay(ToolOverlayState.Empty);
            return;
        }

        // Endpoint band: the click would be refused there — show the marker
        // as invalid instead of a usable cutpoint (v0.3.0 UX overhaul).
        ToolPreviewKind kind = t <= 1e-9 || t >= 1 - 1e-9 ? ToolPreviewKind.Invalid : ToolPreviewKind.Normal;
        var preview = new ToolOverlayState(kind, [], [projected], target.Id);
        SetOverlay(preview);
    }

    private void BreakClick(Point2 world, SnapResult snap, double pickToleranceWorld)
    {
        Point2 cutPoint = snap.IsValid && snap.Kind == SnapKind.Intersection ? snap.WorldPoint : world;
        IGeometryEntity? target = PickClosest(cutPoint, pickToleranceWorld);
        if (target is null)
        {
            RequestStatus("EditTools.Break.NoTarget");
            return;
        }

        if (!PathBreaker.TryProjectParameter(target, cutPoint, pickToleranceWorld, out double t, out _))
        {
            RequestStatus("EditTools.Break.NotOnTarget");
            return;
        }

        if (t <= 1e-9 || t >= 1 - 1e-9)
        {
            RequestStatus("EditTools.Break.EndpointGuard");
            return;
        }

        var command = new BreakEntityCommand(_document, target.Id, cutPoint, pickToleranceWorld);
        _pendingResultId = target.Id;
        SetOverlay(ToolOverlayState.Empty);
        CommandRequested?.Invoke(command);
    }

    // ---------------------------------------------------------------- Trim

    private void TrimHover(Point2 world, SnapResult snap, double pickToleranceWorld)
    {
        Point2 cursor = snap.IsValid ? snap.WorldPoint : world;
        if (_trimTargetId is not { } targetId)
        {
            IGeometryEntity? hover = PickClosest(cursor, pickToleranceWorld);
            if (hover is LineGeometry or ArcGeometry)
            {
                SetOverlay(new ToolOverlayState(ToolPreviewKind.Normal, EntityToSegments(hover), []));
            }
            else
            {
                SetOverlay(ToolOverlayState.Empty);
            }

            return;
        }

        IGeometryEntity? entity = _document.GetEntityById(targetId);
        if (entity is null)
        {
            SetOverlay(ToolOverlayState.Empty);
            return;
        }

        // Sticky plan: as long as the cursor stays put (within the pick
        // tolerance) the last plan is reused verbatim so the preview cannot
        // flicker between two sections near a cut boundary.
        if (_trimStickyPlan is { } sticky
            && cursor.DistanceTo(_trimStickyCursor) <= pickToleranceWorld
            && sticky.Plan is not null)
        {
            SetOverlay(TrimPlanOverlay(sticky, entity.Id));
            return;
        }

        IReadOnlyList<IGeometryEntity> boundaries = TrimBoundaries(entity, pickToleranceWorld);
        TrimSectionOutcome outcome = TrimSectionEngine.PlanSectionTrim(
            entity, boundaries, cursor, pickToleranceWorld, MinKeptPiece);
        if (!outcome.IsValid || outcome.Plan is null)
        {
            SetOverlay(ToolOverlayState.Empty);
            return;
        }

        _trimStickyPlan = outcome;
        _trimStickyCursor = cursor;
        SetOverlay(TrimPlanOverlay(outcome, entity.Id));
    }

    private void TrimClick(Point2 world, SnapResult snap, double pickToleranceWorld)
    {
        Point2 clickPoint = snap.IsValid ? snap.WorldPoint : world;
        if (_trimTargetId is null)
        {
            IGeometryEntity? target = PickClosest(clickPoint, pickToleranceWorld);
            if (target is null)
            {
                RequestStatus("EditTools.Trim.NoTarget");
                return;
            }

            if (target is not (LineGeometry or ArcGeometry))
            {
                RequestStatus("EditTools.Trim.UnsupportedTarget");
                return;
            }

            _trimTargetId = target.Id;
            SetOverlay(new ToolOverlayState(ToolPreviewKind.Normal, EntityToSegments(target), []));
            RequestStatus("EditTools.Trim.PickSection");
            return;
        }

        IGeometryEntity? entity = _document.GetEntityById(_trimTargetId.Value);
        if (entity is null)
        {
            ClearPending();
            SetOverlay(ToolOverlayState.Empty);
            return;
        }

        // The click reuses the sticky hover plan when the cursor has not
        // moved: the removed section is exactly the one previewed (Preview ==
        // Commit guarantee near cut boundaries).
        TrimSectionOutcome? outcome = _trimStickyPlan is { } sticky
            && clickPoint.DistanceTo(_trimStickyCursor) <= pickToleranceWorld
            && sticky.Plan is not null
            ? sticky
            : TrimSectionEngine.PlanSectionTrim(
                entity, TrimBoundaries(entity, pickToleranceWorld), clickPoint, pickToleranceWorld, MinKeptPiece);
        if (outcome is null || !outcome.Value.IsValid || outcome.Value.Plan is null)
        {
            RequestStatus(TrimReasonKey(outcome?.Reason ?? TrimSectionRefusalReason.UnsupportedTarget));
            return;
        }

        var command = new TrimSectionCommand(_document, entity.Id, outcome.Value.Plan.StartT, outcome.Value.Plan.EndT);
        _pendingResultId = entity.Id;
        ClearPending();
        SetOverlay(ToolOverlayState.Empty);
        CommandRequested?.Invoke(command);
    }

    // ---------------------------------------------------------------- Extend

    private void ExtendHover(Point2 world, SnapResult snap, double pickToleranceWorld)
    {
        Point2 cursor = snap.IsValid ? snap.WorldPoint : world;
        if (_extendTargetId is not { } targetId)
        {
            // First step: highlight the hovered entity and mark the end that
            // a click at the cursor would extend; an end without a valid
            // boundary shows the marker as invalid so the refusal does not
            // come as a surprise at click time (v0.3.0 UX overhaul).
            IGeometryEntity? hover = PickClosest(cursor, pickToleranceWorld);
            if (hover is not (LineGeometry or ArcGeometry or PolylineGeometry))
            {
                SetOverlay(ToolOverlayState.Empty);
                return;
            }

            TrimSide side = SideNearClick(hover, cursor);
            Point2 freeEnd = side == TrimSide.KeepStart ? hover.EndPoint : hover.StartPoint;
            bool hasBoundary = ExtendCandidateFinder.FindNearestExtension(
                hover, ExtendBoundaries(hover, side), side, pickToleranceWorld, _tolerance.MaxExtendDistance) is not null;
            SetOverlay(new ToolOverlayState(
                hasBoundary ? ToolPreviewKind.Normal : ToolPreviewKind.Invalid,
                EntityToSegments(hover),
                [freeEnd],
                hover.Id));
            return;
        }

        IGeometryEntity? entity = _document.GetEntityById(targetId);
        if (entity is null)
        {
            SetOverlay(ToolOverlayState.Empty);
            return;
        }

        IReadOnlyList<IGeometryEntity> boundaries = ExtendBoundaries(entity, _extendSide);
        var candidate = ExtendCandidateFinder.FindNearestExtension(
            entity, boundaries, _extendSide, pickToleranceWorld, _tolerance.MaxExtendDistance);
        if (candidate is null)
        {
            SetOverlay(ToolOverlayState.Empty);
            return;
        }

        IGeometryEntity? extended = TrimExtendEngine.TrimEnd(
            entity, candidate.Value.Boundary, _extendSide, pickToleranceWorld, entity.Id)?.Entity;
        if (extended is null)
        {
            SetOverlay(ToolOverlayState.Empty);
            return;
        }

        SetOverlay(new ToolOverlayState(ToolPreviewKind.Extend, EntityToSegments(extended), [], entity.Id));
    }

    private void ExtendClick(Point2 world, SnapResult snap, double pickToleranceWorld)
    {
        Point2 clickPoint = snap.IsValid ? snap.WorldPoint : world;
        if (_extendTargetId is null)
        {
            IGeometryEntity? target = PickClosest(clickPoint, pickToleranceWorld);
            if (target is null)
            {
                RequestStatus("EditTools.Extend.NoTarget");
                return;
            }

            _extendTargetId = target.Id;
            _extendSide = SideNearClick(target, clickPoint);
            // Show the target and mark the end that will be extended right
            // away so the user sees which side the click chose.
            Point2 freeEnd = _extendSide == TrimSide.KeepStart ? target.EndPoint : target.StartPoint;
            SetOverlay(new ToolOverlayState(ToolPreviewKind.Normal, EntityToSegments(target), [freeEnd], target.Id));
            RequestStatus("EditTools.Extend.PickEnd");
            return;
        }

        IGeometryEntity? entity = _document.GetEntityById(_extendTargetId.Value);
        if (entity is null)
        {
            ClearPending();
            SetOverlay(ToolOverlayState.Empty);
            return;
        }

        IReadOnlyList<IGeometryEntity> boundaries = ExtendBoundaries(entity, _extendSide);
        var candidate = ExtendCandidateFinder.FindNearestExtension(
            entity, boundaries, _extendSide, pickToleranceWorld, _tolerance.MaxExtendDistance);
        if (candidate is null)
        {
            RequestStatus("EditTools.Extend.NoBoundary");
            return;
        }

        var command = new TrimExtendCommand(_document, entity.Id, candidate.Value.Boundary.Id, _extendSide, pickToleranceWorld);
        _pendingResultId = entity.Id;
        ClearPending();
        SetOverlay(ToolOverlayState.Empty);
        CommandRequested?.Invoke(command);
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>The nearest interact-visible entity within tolerance of a point.</summary>
    private IGeometryEntity? PickClosest(Point2 world, double tolerance)
    {
        IGeometryEntity? best = null;
        double bestDistance = double.MaxValue;
        foreach (IGeometryEntity entity in _document.Pick(world, tolerance))
        {
            double distance = entity.DistanceToPoint(world);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = entity;
            }
        }

        return best;
    }

    private JoinEndpointResolver.OpenEndpoint? ResolveEndpoint(Point2 world, double tolerance)
    {
        IReadOnlyList<IGeometryEntity> candidates = _document.QueryNear(world, tolerance);
        return JoinEndpointResolver.ResolveNearestOpenEndpoint(candidates, world, tolerance);
    }

    /// <summary>
    /// Boundaries for the trim tool: every interact-visible entity that can
    /// cross the target — everything within a radius covering the target
    /// bounds (spatial index path so hover stays cheap).
    /// </summary>
    private IReadOnlyList<IGeometryEntity> TrimBoundaries(IGeometryEntity target, double tolerance)
    {
        Bounds bounds = target.Bounds;
        Point2 center = new((bounds.MinX + bounds.MaxX) / 2, (bounds.MinY + bounds.MaxY) / 2);
        double radius = Math.Max(bounds.MaxX - bounds.MinX, bounds.MaxY - bounds.MinY) / 2
            + tolerance * 2 + 1e-9;
        return _document.QueryNear(center, radius).Where(e => e.Id != target.Id).ToList();
    }

    /// <summary>
    /// Boundaries for the extend tool: candidates must lie in the extension
    /// half-plane of the free end (within <see cref="GeometryTolerance.MaxExtendDistance"/>)
    /// so the query stays cheap for large drawings. The exact
    /// "extension really lengthens the target" test is delegated to
    /// <see cref="ExtendCandidateFinder"/>.
    /// </summary>
    private IReadOnlyList<IGeometryEntity> ExtendBoundaries(IGeometryEntity target, TrimSide side)
    {
        // KeepStart extends the EndPoint (direction = tangent at the end);
        // KeepEnd extends the StartPoint (direction = -tangent at the start).
        Point2 freeEnd = side == TrimSide.KeepStart ? target.EndPoint : target.StartPoint;
        bool extendEnd = side == TrimSide.KeepStart;
        Vector2 endTangent = target.TangentAt(1.0);
        Vector2 startTangent = target.TangentAt(0.0);
        double dirX = extendEnd ? endTangent.X : -startTangent.X;
        double dirY = extendEnd ? endTangent.Y : -startTangent.Y;
        double max = _tolerance.MaxExtendDistance;

        return _document.QueryNear(freeEnd, max)
            .Where(e =>
            {
                if (e.Id == target.Id)
                {
                    return false;
                }

                Bounds b = e.Bounds;
                double dx = (b.MinX + b.MaxX) / 2 - freeEnd.X;
                double dy = (b.MinY + b.MaxY) / 2 - freeEnd.Y;
                return dirX * dx + dirY * dy > -1e-9;
            })
            .ToList();
    }

    /// <summary>Which end is extended: the click near the EndPoint extends the End, and vice versa.</summary>
    private static TrimSide SideNearClick(IGeometryEntity entity, Point2 click)
    {
        return click.DistanceTo(entity.EndPoint) <= click.DistanceTo(entity.StartPoint)
            ? TrimSide.KeepStart
            : TrimSide.KeepEnd;
    }

    private static IReadOnlyList<IPathSegment> EntityToSegments(IGeometryEntity entity)
    {
        return entity switch
        {
            LineGeometry line => [new LineSegment(line.StartPoint, line.EndPoint)],
            ArcGeometry arc => [new ArcSegment(arc.Center, arc.Radius, arc.StartAngleRadians, Math.Abs(arc.SweepRadians), arc.IsCounterClockwise)],
            PolylineGeometry polyline => polyline.Segments,
            _ => [],
        };
    }

    /// <summary>Overlay for a planned trim section (shared by hover and commit).</summary>
    private static ToolOverlayState TrimPlanOverlay(TrimSectionOutcome outcome, long entityId) =>
        new(ToolPreviewKind.Remove, outcome.Plan!.RemovedRuns, outcome.Plan!.BoundaryPoints, entityId);

    /// <summary>Status key shown when a tool is activated (first-click hint).</summary>
    private static string ActivationHintKey(ToolMode mode)
    {
        return mode switch
        {
            ToolMode.Join => "EditTools.Join.Hint",
            ToolMode.Break => "EditTools.Break.Hint",
            ToolMode.Trim => "EditTools.Trim.Hint",
            ToolMode.Extend => "EditTools.Extend.Hint",
            _ => "EditTools.NodeEdit.Hint",
        };
    }

    /// <summary>Localized status key for a join rejection reason.</summary>
    private static string JoinReasonKey(JoinRejectReason reason)
    {
        return reason switch
        {
            JoinRejectReason.NotConnected => "EditTools.Join.NotConnected",
            JoinRejectReason.Ambiguous => "EditTools.Join.Ambiguous",
            JoinRejectReason.DifferentLayers => "EditTools.Join.DifferentLayers",
            _ => "EditTools.Join.Unsupported",
        };
    }

    /// <summary>Localized status key for a trim refusal reason.</summary>
    private static string TrimReasonKey(TrimSectionRefusalReason reason)
    {
        return reason switch
        {
            TrimSectionRefusalReason.UnsupportedTarget => "EditTools.Trim.UnsupportedTarget",
            TrimSectionRefusalReason.NotOnTarget => "EditTools.Trim.NotOnTarget",
            TrimSectionRefusalReason.NoBoundary => "EditTools.Trim.NoBoundary",
            TrimSectionRefusalReason.DegenerateRemoval => "EditTools.Trim.InvalidSection",
            TrimSectionRefusalReason.TinyKeptPiece => "EditTools.Trim.TinyKeptPiece",
            _ => "EditTools.Trim.UnsupportedTarget",
        };
    }

    private void ClearPending()
    {
        _joinFirst = null;
        _trimTargetId = null;
        _extendTargetId = null;
        _trimStickyPlan = null;
    }

    private void RequestStatus(string key)
    {
        // Anti-storm: identical consecutive status keys are raised only once
        // (v0.3.0 UX overhaul) — repeated hover events must not re-publish
        // the same message.
        if (_lastStatusKey == key)
        {
            return;
        }

        _lastStatusKey = key;
        StatusKeyRequested?.Invoke(key);
    }

    private void SetOverlay(ToolOverlayState overlay)
    {
        if (ReferenceEquals(overlay, Overlay))
        {
            return;
        }

        Overlay = overlay;
        OverlayChanged?.Invoke();
    }
}