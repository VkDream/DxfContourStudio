#nullable enable

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Application.Interaction;
using DxfContourStudio.Application.Selection;
using DxfContourStudio.Core.Contours;
using DxfContourStudio.Core.Diagnostics;
using DxfContourStudio.Core.Geometry;
using LineGeo = DxfContourStudio.Core.Geometry.LineGeometry;
using CircleGeo = DxfContourStudio.Core.Geometry.CircleGeometry;
using ArcGeo = DxfContourStudio.Core.Geometry.ArcGeometry;
using LineSeg = DxfContourStudio.Core.Geometry.LineSegment;
using ArcSeg = DxfContourStudio.Core.Geometry.ArcSegment;

namespace DxfContourStudio.Wpf.Views;

/// <summary>
/// WPF viewport control that renders a <see cref="CadDocument"/> through a
/// pure-math <see cref="Viewport"/> and forwards mouse gesture results to the
/// view model via events. It only does presentation: coordinate conversion,
/// zoom/pan, click hit-testing (via <see cref="HitTester"/>), a drag preview
/// and selection highlighting. The view model owns every mutation (selection
/// changes, move commands) so no business logic lives in this class.
///
/// Rendering strategy: the whole scene is redrawn on <see cref="OnRender"/>;
/// arcs and circles are flattened into short polyline runs so the rendering
/// path stays simple and correct (avoids WPF ArcTo direction pitfalls).
/// </summary>
public sealed class CadViewport : FrameworkElement
{
    private CadDocument? _document;
    private Viewport? _viewport;
    private SelectionModel? _selection;

    /// <summary>Interaction-feel constants (v0.3.0 UX overhaul).</summary>
    public InteractionSettings Interaction { get; set; } = InteractionSettings.Default;

    private const double MaxSegmentSagittaPx = 0.5;

    /// <summary>Hit tolerance in screen px (was the old 6px constant, now settings-owned).</summary>
    private double PickTolerancePx => Interaction.HitTolerancePx;

    /// <summary>Screen px of pointer movement before a press counts as a drag (was 3px).</summary>
    private double DragStartThresholdPx => Interaction.DragThresholdPx;

    private Point _lastMouse;
    private bool _isPanning;

    // Drag preview state. Entities are never mutated live: we keep the world
    // offset and draw the dragged entities shifted by it; on mouse-up one
    // MoveEntitiesCommand is raised with the accumulated delta.
    private List<long> _dragIds = [];
    private Point2 _dragStartWorld;
    private Vector2 _dragOffsetWorld;
    private bool _isDragging;

    // Box selection state (v0.3.0): a left-drag started on empty space with a
    // threshold of BoxSelectionMinSizePx becomes window selection. Window
    // (left→right) selects fully contained entities, crossing (right→left)
    // selects all touched entities. Additive with Ctrl.
    private Point _boxStartScreen;
    private bool _isBoxSelecting;
    // Ids captured when the box drag starts: entities found inside the box
    // are highlighted while the window is dragged (v0.3.0 box selection).
    private IReadOnlyList<long> _boxSnapshot = [];

    // The last click position (screen), used for coverage cycling: clicking
    // the same spot repeatedly cycles through the overlapping candidates
    // (v0.3.0). A new spot clears the cycle list and starts at the closest.
    private Point _lastClickScreen;
    private bool _pickCycleActive;
    private List<long> _cycleCandidates = [];
    private int _cycleIndex;

    // Rendering 2.0 (docs/RENDERING-2.md): culled world-space bounds cache.
    // Entity bounds are geometry-derived and only change when the document
    // fires DataChanged, so they are cached per id and reused across frames.
    private readonly Dictionary<long, Bounds> _boundsCache = [];
    private const double CullingMarginPx = 8.0;

    private static readonly Brush Background = FreezeBrush(new SolidColorBrush(Color.FromRgb(250, 250, 250)));
    private static readonly Pen GeometryPen = FreezePen(new Pen(new SolidColorBrush(Colors.Black), 1.0));
    private static readonly Pen PrimaryPen = FreezePen(new Pen(new SolidColorBrush(Colors.OrangeRed), 2.0));
    private static readonly Pen SecondaryPen = FreezePen(new Pen(new SolidColorBrush(Color.FromRgb(230, 120, 20)), 2.0));

    private static Brush FreezeBrush(Brush brush)
    {
        brush.Freeze();
        return brush;
    }

    private static Pen FreezePen(Pen pen)
    {
        pen.Freeze();
        return pen;
    }

    /// <summary>
    /// Resolves a themed brush by resource key with a built-in fallback, so
    /// the control still renders if the theme dictionary is not loaded.
    /// </summary>
    private static Brush ThemeBrush(string key, Brush fallback) =>
        System.Windows.Application.Current?.TryFindResource(key) as Brush ?? fallback;

    private static Pen ThemePen(string key, Pen fallback) =>
        FreezePen(new Pen(ThemeBrush(key, fallback.Brush), fallback.Thickness));

    /// <summary>The document being displayed.</summary>
    public CadDocument? Document
    {
        get => _document;
        set
        {
            if (_document is not null)
            {
                _document.DataChanged -= OnDocumentDataChanged;
            }

            _document = value;
            if (_document is not null)
            {
                _document.DataChanged += OnDocumentDataChanged;
            }

            _boundsCache.Clear();
            InvalidateVisual();
        }
    }

    /// <summary>Data changed → geometry may have moved; drop the cull cache.</summary>
    private void OnDocumentDataChanged()
    {
        _boundsCache.Clear();
        InvalidateVisual();
    }

    /// <summary>The pure-math viewport state used to transform coordinates.</summary>
    public Viewport? Viewport
    {
        get => _viewport;
        set
        {
            if (_viewport is not null)
            {
                _viewport.Changed -= OnViewportChanged;
            }

            _viewport = value;
            if (_viewport is not null)
            {
                _viewport.Changed += OnViewportChanged;
            }

            InvalidateVisual();
        }
    }

    private void OnViewportChanged() => InvalidateVisual();

    /// <summary>
    /// The selection set to highlight. The viewport renders selected entities
    /// with an accent pen; it never modifies the selection itself.
    /// </summary>
    public SelectionModel? Selection
    {
        get => _selection;
        set
        {
            if (_selection is not null)
            {
                _selection.SelectionChanged -= OnSelectionChanged;
            }

            _selection = value;
            if (_selection is not null)
            {
                _selection.SelectionChanged += OnSelectionChanged;
            }

            InvalidateVisual();
        }
    }

    /// <summary>
    /// Overlay markers from the latest contour analysis (null/empty = none).
    /// The viewport only draws them — it never runs any analysis itself.
    /// </summary>
    public IReadOnlyList<GapDiagnostic>? Diagnostics
    {
        get => _diagnostics;
        set
        {
            _diagnostics = value;
            InvalidateVisual();
        }
    }

    private IReadOnlyList<GapDiagnostic>? _diagnostics;

    /// <summary>
    /// Geometry-level findings (zero length / very small / duplicate / self
    /// intersection) drawn as overlay markers; see <see cref="DrawGeometryDiagnostics"/>.
    /// </summary>
    public IReadOnlyList<GeometryDiagnostic>? GeometryDiagnostics
    {
        get => _geometryDiagnostics;
        set
        {
            _geometryDiagnostics = value;
            InvalidateVisual();
        }
    }

    private IReadOnlyList<GeometryDiagnostic>? _geometryDiagnostics;

    /// <summary>
    /// The hover snap candidate to draw (D13A). Set by the view model on every
    /// candidate change; cleared with <see cref="SnapResult.None"/>. Drawing
    /// only — never mutates document geometry.
    /// </summary>
    public SnapResult? SnapMarker
    {
        get => _snapMarker;
        set
        {
            _snapMarker = value;
            InvalidateVisual();
        }
    }

    private SnapResult? _snapMarker;

    /// <summary>
    /// Select-mode hover highlight (v0.3.0): the entity id the view model
    /// resolved as "under the cursor". Drawn with a thin accent pen — the
    /// overlay stays unobtrusive. Null while a tool owns the mouse.
    /// </summary>
    public long? HoveredEntityId
    {
        get => _hoveredEntityId;
        set
        {
            if (_hoveredEntityId == value)
            {
                return;
            }

            _hoveredEntityId = value;
            InvalidateVisual();
        }
    }

    private long? _hoveredEntityId;

    /// <summary>
    /// Preview geometry of the active node drag (D14), set by the view model
    /// on every drag move. When its id matches an entity, that entity is drawn
    /// in its edited state with the preview pen — the document itself stays
    /// untouched until the gesture commits.
    /// </summary>
    public IGeometryEntity? NodeEditPreview
    {
        get => _nodeEditPreview;
        set
        {
            _nodeEditPreview = value;
            if (value is null)
            {
                _isNodeDragging = false;
            }

            InvalidateVisual();
        }
    }

    private IGeometryEntity? _nodeEditPreview;
    private bool _isNodeDragging;

    /// <summary>
    /// The interactive edit tool session (D17). When a tool is active the
    /// viewport drives its pointer pipeline (hover preview, click consumption)
    /// and renders its <see cref="EditToolSession.Overlay"/>. Pure forwarding:
    /// all gesture logic lives in the session.
    /// </summary>
    public EditToolSession? ToolSession
    {
        get => _toolSession;
        set
        {
            if (_toolSession is not null)
            {
                _toolSession.OverlayChanged -= OnToolOverlayChanged;
            }

            _toolSession = value;
            if (_toolSession is not null)
            {
                _toolSession.OverlayChanged += OnToolOverlayChanged;
            }

            InvalidateVisual();
        }
    }

    private EditToolSession? _toolSession;

    private void OnToolOverlayChanged() => InvalidateVisual();

    /// <summary>Raised when the mouse grabs a grip (start of a node drag).</summary>
    public event Action<GripDescriptor>? GripDragStarted;

    /// <summary>Raised continuously while a grip is dragged (world space).</summary>
    public event Action<Point2>? GripDragMoved;

    /// <summary>Raised on mouse-up of a grip drag (the VM commits its preview).</summary>
    public event Action? GripDragCommitted;

    /// <summary>Raised when a grip drag is cancelled (capture loss).</summary>
    public event Action? GripDragCancelled;

    /// <summary>
    /// Raised when the pointer leaves the viewport — the VM clears the snap
    /// marker and status (no stale marker may survive).
    /// </summary>
    public event Action? PointerLeft; 

    /// <summary>Raised for a click-pick: id plus whether the user held Ctrl (additive).</summary>
    public event Action<long, bool>? EntityClicked;

    /// <summary>Raised when the user clicks empty space (a plain click, no drag).</summary>
    public event Action? EmptySpaceClicked;

    /// <summary>
    /// Raised once when a completed box drag selects entities (v0.3.0): the
    /// world-space box plus whether the user held Ctrl (additive).
    /// <paramref name="crossing"/> true = right→left drag (touched entities),
    /// false = left→right (fully contained).
    /// </summary>
    public event Action<Bounds, bool, bool>? BoxSelectionCommitted;

    /// <summary>
    /// Raised once when a drag-gesture that actually moved geometry ends.
    /// Carries the moved ids and the final world-space delta.
    /// </summary>
    public event Action<IReadOnlyList<long>, Vector2>? MoveGestureCommitted;

    /// <summary>Raised continuously with the world point under the mouse.</summary>
    public event Action<Point2>? WorldCursorMoved;

    private static readonly Pen SelectionPreviewPen = FreezePen(new Pen(new SolidColorBrush(Color.FromRgb(90, 120, 220)), 1.0));

    /// <summary>Select-mode hover hint pen (thin accent, v0.3.0).</summary>
    private static readonly Pen HoverPen = FreezePen(new Pen(new SolidColorBrush(Color.FromRgb(255, 160, 60)), 1.6));

    /// <summary>Live box-selection window pen (cyan, v0.3.0).</summary>
    private static readonly Pen BoxSelectPen = FreezePen(new Pen(new SolidColorBrush(Color.FromRgb(0, 190, 232)), 1.6));

    private void OnSelectionChanged() => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (_document is null || _viewport is null)
        {
            return;
        }

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 1 || h <= 1)
        {
            return;
        }

        dc.DrawRectangle(ThemeBrush("Viewport.BackgroundBrush", Background), null, new Rect(0, 0, w, h));

        // Culling: compute the visible world rectangle once per frame and
        // skip entities whose bounds don't intersect it (padded by a margin).
        Bounds viewWorld = RenderCulling.WorldView(_viewport, w, h);
        double marginWorld = _viewport.PixelsToWorld(CullingMarginPx);

        // One pass draws the geometry for both normal and preview state.
        foreach (var entity in _document.Entities)
        {
            if (!_document.IsVisibleForInteraction(entity))
            {
                continue;
            }

            if (!CullTest(entity, viewWorld, marginWorld))
            {
                continue;
            }

            IGeometryEntity? toDraw = null;
            if (_nodeEditPreview is { } preview && preview.Id == entity.Id && _isNodeDragging)
            {
                toDraw = preview;
            }
            else if (_isDragging && IsInDrag(entity.Id) && _dragOffsetWorld.LengthSquared > 0)
            {
                toDraw = entity.Transformed(Transform2.CreateTranslation(_dragOffsetWorld));
            }
            else
            {
                toDraw = entity;
            }

            // The active tool highlights the hovered entity with the preview
            // pen so the user sees what the next click acts on — but only
            // for preview kinds that represent "pick this" (Normal). Remove
            // and Extend previews paint the affected section themselves via
            // the overlay runs, so the whole entity must not glow too
            // (v0.3.0 UX overhaul).
            bool toolHover = _toolSession is not null
                && _toolSession.Overlay.Kind == ToolPreviewKind.Normal
                && _toolSession.Overlay.HoverEntityId == entity.Id;
            Pen pen = toolHover
                ? ToolOverlayPen(ToolPreviewKind.Normal)
                : _hoveredEntityId == entity.Id && !IsToolActive && _toolSession is null or { ActiveTool: ToolMode.Select }
                    ? ThemePen("Viewport.HoverBrush", HoverPen)
                    : _isDragging && IsInDrag(entity.Id)
                        ? ThemePen("Viewport.DragPreviewBrush", SelectionPreviewPen)
                        : _nodeEditPreview is { } && _nodeEditPreview.Id == entity.Id && _isNodeDragging
                            ? ThemePen("Viewport.DragPreviewBrush", SelectionPreviewPen)
                            : _selection is not null && _selection.IsSelected(entity.Id)
                                ? (_selection.PrimaryId == entity.Id
                                    ? ThemePen("Viewport.PrimaryBrush", PrimaryPen)
                                    : ThemePen("Viewport.SelectedBrush", SecondaryPen))
                                : ThemePen("Viewport.GeometryBrush", GeometryPen);

            DrawEntityGeometry(dc, toDraw, pen);
        }

        DrawDiagnostics(dc);
        DrawGeometryDiagnostics(dc);
        DrawSnapMarker(dc);
        DrawGrips(dc);
        DrawToolOverlay(dc);
        DrawSelectionBox(dc);
    }

    // ---- tool overlay (D17) --------------------------------------------------

    /// <summary>True while an interactive tool owns the mouse.</summary>
    private bool IsToolActive => _toolSession is { ActiveTool: ToolMode.Join or ToolMode.Break or ToolMode.Trim or ToolMode.Extend };

    /// <summary>World-space pick tolerance used by the tool gestures (matches the click pick).</summary>
    private double ToolPickToleranceWorld => _viewport is null
        ? 1e-6
        : PickTolerancePx / Math.Max(_viewport.PixelsPerWorld, 1e-9);

    private static readonly Pen ToolNormalPen = FreezePen(new Pen(new SolidColorBrush(Color.FromRgb(80, 140, 220)), 1.6));
    private static readonly Pen ToolRemovePen = FreezePen(new Pen(new SolidColorBrush(Color.FromRgb(205, 60, 60)), 2.2));
    private static readonly Pen ToolExtendPen = FreezePen(new Pen(new SolidColorBrush(Color.FromRgb(60, 160, 110)), 1.6));
    private static readonly Pen ToolInvalidPen = FreezePen(new Pen(new SolidColorBrush(Color.FromRgb(150, 150, 150)), 1.2));

    private Pen ToolOverlayPen(ToolPreviewKind kind) => kind switch
    {
        ToolPreviewKind.Remove => ThemePen("Tool.RemoveBrush", ToolRemovePen),
        ToolPreviewKind.Extend => ThemePen("Tool.ExtendBrush", ToolExtendPen),
        ToolPreviewKind.Invalid => ToolInvalidPen,
        _ => ThemePen("Tool.PreviewBrush", ToolNormalPen),
    };

    /// <summary>
    /// Draws the active tool's hover preview: highlighted runs (join connection,
    /// trim section, extension), marker points (break point, boundaries) and —
    /// through <see cref="OnRender"/> — the hovered entity itself in the
    /// preview pen. Presentation only; the session owns the geometry.
    /// </summary>
    private void DrawToolOverlay(DrawingContext dc)
    {
        if (_toolSession is not { ActiveTool: not ToolMode.Select } session || session.Overlay is not { } overlay)
        {
            return;
        }

        if (overlay.Kind == ToolPreviewKind.None)
        {
            return;
        }

        Pen pen = ToolOverlayPen(overlay.Kind);
        foreach (IPathSegment seg in overlay.HighlightRuns)
        {
            DrawOverlaySegment(dc, pen, seg);
        }

        Brush markerBrush = pen.Brush;
        var markerPen = FreezePen(new Pen(markerBrush, 1.5));
        foreach (Point2 marker in overlay.Markers)
        {
            Point p = ToScreen(marker);
            DrawSquare(dc, markerPen, p, 3.5);
        }
    }

    private void DrawOverlaySegment(DrawingContext dc, Pen pen, IPathSegment seg)
    {
        switch (seg)
        {
            case LineSeg line:
                DrawLineRun(dc, pen, ToScreen(line.StartPoint), ToScreen(line.EndPoint));
                break;
            case ArcSeg arc:
                Point prev = ToScreen(arc.StartPoint);
                foreach (Point p in FlattenArc(arc.Center, arc.Radius, arc.StartAngleRadians, arc.SweepRadians))
                {
                    DrawLineRun(dc, pen, prev, p);
                    prev = p;
                }

                break;
        }
    }

    /// <summary>
    /// Hover snap overlay marker (D13B). The viewport only draws the marker
    /// passed in by the view model — it never computes or modifies the snap
    /// result itself. The marker shape encodes the snap kind and is drawn in
    /// screen pixels (fixed size, independent of zoom); with a single marker
    /// brush from the theme. The kind is additionally reported on the status
    /// bar. The marker is cleared by the VM (Esc, document change, snap
    /// disabled, mouse leave).
    /// </summary>
    private void DrawSnapMarker(DrawingContext dc)
    {
        if (_snapMarker is not { IsValid: true } snap)
        {
            return;
        }

        Point p = ToScreen(snap.WorldPoint);
        Brush brush = ThemeBrush("Snap.MarkerBrush", new SolidColorBrush(Color.FromRgb(0, 166, 180)));
        var pen = FreezePen(new Pen(brush, 1.6));
        switch (snap.Kind)
        {
            case SnapKind.Endpoint:
                // Square □ — a corner that can be picked.
                DrawSquare(dc, pen, p, 4.5);
                break;
            case SnapKind.Midpoint:
                // Triangle ▲ — the member middle point.
                dc.DrawGeometry(null, pen, MidpointTriangle(p, 5.5));
                break;
            case SnapKind.Center:
                // Circle ○ — a center.
                dc.DrawEllipse(null, pen, p, 4.0, 4.0);
                break;
            case SnapKind.Intersection:
                // X — crossing of two runs.
                const double arm = 5.0;
                dc.DrawLine(pen, new Point(p.X - arm, p.Y - arm), new Point(p.X + arm, p.Y + arm));
                dc.DrawLine(pen, new Point(p.X - arm, p.Y + arm), new Point(p.X + arm, p.Y - arm));
                break;
            default:
                // Nearest ◇ — plain closest point.
                DrawDiamond(dc, pen, p, 3.5);
                break;
        }
    }

    private static void DrawSquare(DrawingContext dc, Pen pen, Point c, double half)
        => dc.DrawRectangle(null, pen, new Rect(c.X - half, c.Y - half, half * 2, half * 2));

    private static void DrawDiamond(DrawingContext dc, Pen pen, Point c, double r)
    {
        var g = new StreamGeometry();
        using (var s = g.Open())
        {
            s.BeginFigure(new Point(c.X, c.Y - r), false, false);
            s.LineTo(new Point(c.X + r, c.Y), false, false);
            s.LineTo(new Point(c.X, c.Y + r), false, false);
            s.LineTo(new Point(c.X - r, c.Y), false, false);
        }

        g.Freeze();
        dc.DrawGeometry(null, pen, g);
    }

    private static Geometry MidpointTriangle(Point c, double r)
    {
        var g = new StreamGeometry();
        using (StreamGeometryContext s = g.Open())
        {
            s.BeginFigure(new Point(c.X, c.Y - r), false, false);
            s.LineTo(new Point(c.X + r, c.Y + r), false, false);
            s.LineTo(new Point(c.X - r, c.Y + r), false, false);
            s.Close();
        }

        g.Freeze();
        return g;
    }

    // ---- grip overlay (D13B) -------------------------------------------------

    private Point _lastHoverScreen;
    private int _hoveredGrip = -1;

    /// <summary>
    /// Draws the grips of the single selected entity (multi-select shows no
    /// grips to keep the canvas readable). Handles are fixed 7px boxes in
    /// screen space; the hovered one (mouse within pickup radius) is drawn
    /// larger with the hover accent. Pure presentation — dragging is handled
    /// by the interaction layer on top of the same descriptor data.
    /// </summary>
    private void DrawGrips(DrawingContext dc)
    {
        if (_document is null || _selection is not { Count: 1 } || _selection.PrimaryId is not { } id)
        {
            _hoveredGrip = -1;
            return;
        }

        // During an edit drag the grips follow the preview, not the original.
        IGeometryEntity? entity = _isNodeDragging
            ? _nodeEditPreview ?? _document.GetEntityById(id)
            : _document.GetEntityById(id);
        if (entity is null)
        {
            return;
        }

        var grips = GripBuilder.Build(entity);
        for (int i = 0; i < grips.Count; i++)
        {
            Point p = ToScreen(grips[i].WorldPosition);
            bool hovered = i == _hoveredGrip;
            Brush stroke = ThemeBrush(hovered ? "Grip.HoverBrush" : "Grip.HandleBrush",
                new SolidColorBrush(hovered ? Colors.Red : Color.FromRgb(45, 95, 138)));
            Brush fill = ThemeBrush("Grip.FillBrush", Brushes.White);
            double half = hovered ? 4.5 : 3.5;
            var pen = FreezePen(new Pen(stroke, 1.6));
            dc.DrawRectangle(fill, pen, new Rect(p.X - half, p.Y - half, half * 2, half * 2));
        }
    }

    /// <summary>Picks the grip under the current mouse position (screen-space pickup radius).</summary>
    private void UpdateHoveredGrip()
    {
        int previous = _hoveredGrip;
        _hoveredGrip = -1;
        if (_document is null || _selection is not { Count: 1 } || _selection.PrimaryId is not { } primary)
        {
            return;
        }

        if (_document.GetEntityById(primary) is not { } entity)
        {
            return;
        }

        var grips = GripBuilder.Build(entity);
        for (int i = 0; i < grips.Count; i++)
        {
            double d = Distance(_lastHoverScreen, ToScreen(grips[i].WorldPosition));
            if (d <= 8.0)
            {
                _hoveredGrip = i;
                break;
            }
        }

        if (_hoveredGrip != previous)
        {
            InvalidateVisual();
        }
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>
    /// Draws the diagnostic overlay on top of the geometry: small gaps as a
    /// connector line with marker dots, open ends as a single marker, branch
    /// junctions as an error marker. Colors resolve through the theme by key.
    /// </summary>
    private void DrawDiagnostics(DrawingContext dc)
    {
        if (_diagnostics is null || _diagnostics.Count == 0)
        {
            return;
        }

        foreach (GapDiagnostic d in _diagnostics)
        {
            switch (d.Kind)
            {
                case GapKind.SmallGap:
                    DrawGapMarker(dc, d.PositionA, d.PositionB);
                    break;
                case GapKind.OpenContourEnd:
                    DrawDot(dc, ToScreen(d.PositionA), ThemeBrush("Diagnostic.OpenEndpointBrush", new SolidColorBrush(Colors.OrangeRed)), 3.5);
                    break;
                case GapKind.BranchNode:
                    DrawDot(dc, ToScreen(d.PositionA), ThemeBrush("Diagnostic.ErrorBrush", new SolidColorBrush(Colors.Crimson)), 3.0);
                    break;
            }
        }
    }

    /// <summary>Two marker dots joined by a connector line (a small gap).</summary>
    private void DrawGapMarker(DrawingContext dc, Point2 a, Point2 b)
    {
        Brush brush = ThemeBrush("Diagnostic.GapConnectorBrush", new SolidColorBrush(Color.FromRgb(230, 120, 20)));
        Point pa = ToScreen(a);
        Point pb = ToScreen(b);
        dc.DrawLine(FreezePen(new Pen(brush, 1.5)), pa, pb);
        DrawDot(dc, pa, brush, 3.5);
        DrawDot(dc, pb, brush, 3.5);
    }

    /// <summary>
    /// Draws the geometry-level overlay markers: zero length (crimson dot),
    /// very small (amber dot), duplicates (magenta ring on both entities) and
    /// self intersections (red X). Overlay never alters the source geometry.
    /// </summary>
    private void DrawGeometryDiagnostics(DrawingContext dc)
    {
        if (_geometryDiagnostics is null || _geometryDiagnostics.Count == 0)
        {
            return;
        }

        foreach (GeometryDiagnostic g in _geometryDiagnostics)
        {
            switch (g.Kind)
            {
                case DiagnosticKind.ZeroLength:
                    DrawDot(dc, ToScreen(g.PositionA), ThemeBrush("Diagnostic.ErrorBrush", new SolidColorBrush(Colors.Crimson)), 3.5);
                    break;
                case DiagnosticKind.VerySmall:
                    DrawDot(dc, ToScreen(g.PositionA), ThemeBrush("Diagnostic.GapConnectorBrush", new SolidColorBrush(Color.FromRgb(230, 120, 20))), 3.0);
                    break;
                case DiagnosticKind.Duplicate:
                    DrawDot(dc, ToScreen(g.PositionA), ThemeBrush("Diagnostic.ErrorBrush", new SolidColorBrush(Colors.Magenta)), 3.0);
                    break;
                case DiagnosticKind.SelfIntersection:
                    DrawSelfIntersectionMarker(dc, g.PositionA, g.PositionB);
                    break;
            }
        }
    }

    /// <summary>Draws a small red X at the crossing of two segments.</summary>
    private void DrawSelfIntersectionMarker(DrawingContext dc, Point2 worldA, Point2 worldB)
    {
        Point p = ToScreen(worldA);
        Brush brush = ThemeBrush("Diagnostic.ErrorBrush", new SolidColorBrush(Colors.Crimson));
        var pen = FreezePen(new Pen(brush, 1.5));
        const double arm = 4.0;
        dc.DrawLine(pen, new Point(p.X - arm, p.Y - arm), new Point(p.X + arm, p.Y + arm));
        dc.DrawLine(pen, new Point(p.X - arm, p.Y + arm), new Point(p.X + arm, p.Y - arm));
        DrawDot(dc, p, brush, 2.0);
    }

    private static void DrawDot(DrawingContext dc, Point p, Brush brush, double radius)
    {
        if (radius <= 0)
        {
            return;
        }

        var fill = brush.Clone();
        fill.Freeze();
        dc.DrawEllipse(fill, null, p, radius, radius);
    }

    private bool IsInDrag(long id) => _dragIds.Contains(id);

    /// <summary>
    /// Draws the geometry of one entity with the given pen. Open figures are
    /// drawn as explicit line runs (DrawLine), never as open StreamGeometry
    /// built with isStroked:false segments: such figures carry no visible
    /// stroke through DrawGeometry, which is what produced the blank viewport
    /// for files made of plain lines (small_gap_003, polyline outlines, arcs).
    /// </summary>
    private void DrawEntityGeometry(DrawingContext dc, IGeometryEntity entity, Pen pen)
    {
        switch (entity)
        {
            case LineGeo line:
                DrawLineRun(dc, pen, ToScreen(line.P0), ToScreen(line.P1));
                break;
            case CircleGeo circle:
                Point c = ToScreen(circle.Center);
                double r = circle.Radius * _viewport!.PixelsPerWorld;
                dc.DrawEllipse(null, pen, c, r, r);
                break;
            case ArcGeo arc:
                Point first = ToScreen(arc.StartPoint);
                Point prev = first;
                foreach (Point p in FlattenArc(arc.Center, arc.Radius, arc.StartAngleRadians, arc.SweepRadians))
                {
                    DrawLineRun(dc, pen, prev, p);
                    prev = p;
                }

                break;
            case PolylineGeometry poly:
                if (poly.Segments.Count == 0)
                {
                    break;
                }

                Point start = ToScreen(poly.Segments[0].StartPoint);
                Point cur = start;
                foreach (IPathSegment seg in poly.Segments)
                {
                    if (seg is LineSeg lineSeg)
                    {
                        Point next = ToScreen(lineSeg.EndPoint);
                        DrawLineRun(dc, pen, cur, next);
                        cur = next;
                    }
                    else if (seg is ArcSeg arcSeg)
                    {
                        Point arcPrev = cur;
                        foreach (Point p in FlattenArc(arcSeg.Center, arcSeg.Radius, arcSeg.StartAngleRadians, arcSeg.SweepRadians))
                        {
                            DrawLineRun(dc, pen, arcPrev, p);
                            arcPrev = p;
                        }

                        cur = arcPrev;
                    }
                }

                if (poly.IsClosed)
                {
                    DrawLineRun(dc, pen, cur, start);
                }

                break;
        }
    }

    /// <summary>
    /// Live box-selection overlay (v0.3.0): entities under the current box
    /// are tinted with the primary pen and the window itself is outlined in
    /// cyan. Presentation only — the selection commits on mouse-up.
    /// </summary>
    private void DrawSelectionBox(DrawingContext dc)
    {
        if (!_isBoxSelecting || _viewport is null)
        {
            return;
        }

        Point pos = Mouse.GetPosition(this);
        double x = Math.Min(_boxStartScreen.X, pos.X);
        double y = Math.Min(_boxStartScreen.Y, pos.Y);
        double w = Math.Abs(pos.X - _boxStartScreen.X);
        double h = Math.Abs(pos.Y - _boxStartScreen.Y);
        if (w <= 0.5 || h <= 0.5)
        {
            return;
        }

        // Tint the captured entities so the user sees the pending selection
        // grow while dragging (cheap: bounds membership was computed on move).
        foreach (long id in _boxSnapshot)
        {
            if (_document?.GetEntityById(id) is { } e && _document.IsVisibleForInteraction(e))
            {
                DrawEntityGeometry(dc, e, ThemePen("Viewport.PrimaryBrush", PrimaryPen));
            }
        }

        dc.DrawRectangle(null, BoxSelectPen, new Rect(x, y, w, h));
    }

    /// <summary>
    /// World-space cull test with a per-entity bounds cache. Dragged preview
    /// entities are never culled (they may move anywhere during a gesture).
    /// </summary>
    private bool CullTest(IGeometryEntity entity, Bounds viewWorld, double marginWorld)
    {
        if (_isDragging && IsInDrag(entity.Id) && _dragOffsetWorld.LengthSquared > 0)
        {
            return true;
        }

        if (!_boundsCache.TryGetValue(entity.Id, out Bounds bounds))
        {
            bounds = entity.Bounds;
            _boundsCache[entity.Id] = bounds;
        }

        return RenderCulling.IsVisible(bounds, viewWorld, marginWorld);
    }

    private static void DrawLineRun(DrawingContext dc, Pen pen, Point a, Point b)
    {
        dc.DrawLine(pen, a, b);
    }

    private Point ToScreen(Point2 world) => _viewport is null
        ? new Point(world.X, world.Y)
        : ToScreen(world, _viewport, Math.Max(ActualWidth, 1), Math.Max(ActualHeight, 1));

    private Point2 ToWorld(Point screen) => ScreenToWorld(_viewport!, new Point2(screen.X, screen.Y), Math.Max(ActualWidth, 1), Math.Max(ActualHeight, 1));

    /// <summary>Screen-pixel distance between two points (click-stability test).</summary>
    private static double DistPx(Point a, Point b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static Point ToScreen(Point2 world, Viewport viewport, double w, double h)
    {
        Point2 s = viewport.WorldToScreen(world, w, h);
        return new Point(s.X, s.Y);
    }

    private static Point2 ScreenToWorld(Viewport viewport, Point2 screen, double w, double h) =>
        viewport.ScreenToWorld(screen, w, h);

    /// <summary>
    /// Flattens an arc into screen-space polyline points (end point of each
    /// run), stepping the angle so the maximum sagitta stays under
    /// <see cref="MaxSegmentSagittaPx"/> screen pixels. The start point itself
    /// is not repeated.
    /// </summary>
    private IEnumerable<Point> FlattenArc(Point2 centerWorld, double radiusWorld, double startAngleRad, double sweepRad)
    {
        if (_viewport is null)
        {
            yield break;
        }

        double r = radiusWorld * _viewport.PixelsPerWorld;
        double step = r > 1e-6 ? 2.0 * Math.Acos(Math.Max(-1.0, Math.Min(1.0, 1.0 - MaxSegmentSagittaPx / r))) : 0.02;
        step = Math.Min(Math.Max(step, 0.01), Math.PI / 8);
        double sweep = sweepRad;
        int n = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / step));
        double actualStep = sweep / n;

        for (int i = 1; i <= n; i++)
        {
            double angle = startAngleRad + actualStep * i;
            yield return ToScreen(new Point2(
                centerWorld.X + radiusWorld * Math.Cos(angle),
                centerWorld.Y + radiusWorld * Math.Sin(angle)));
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_viewport is null)
        {
            return;
        }

        double w = Math.Max(ActualWidth, 1);
        double h = Math.Max(ActualHeight, 1);
        Point screen = e.GetPosition(this);

        double factor = e.Delta > 0 ? Interaction.ZoomFactorPerNotch : 1.0 / Interaction.ZoomFactorPerNotch;
        // Cursor-anchored zoom moved into the pure-math viewport (v0.3.0) —
        // clamped to the configured zoom bounds, unit-testable without WPF.
        _viewport.ZoomAtScreen(factor, new Point2(screen.X, screen.Y), w, h);
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton is MouseButton.Middle or MouseButton.Right)
        {
            _isPanning = true;
            _lastMouse = e.GetPosition(this);
            Focus();
            e.Handled = true;
        }
    }

    /// <summary>Pointer leaving the viewport → the VM clears the snap marker.</summary>
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        PointerLeft?.Invoke();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Point pos = e.GetPosition(this);
        _lastHoverScreen = pos;

        if (_viewport is not null)
        {
            WorldCursorMoved?.Invoke(ToWorld(pos));
        }
        else
        {
            return;
        }

        // Active tool: the hover pipeline recomputes the preview overlay from
        // the current snap candidate (already refreshed by the VM via
        // WorldCursorMoved) — hovering never mutates the document.
        if (IsToolActive)
        {
            _toolSession!.OnPointerMoved(ToWorld(pos), _snapMarker ?? SnapResult.None, ToolPickToleranceWorld);
        }

        UpdateHoveredGrip();

        UpdateCursor(pos);

        if (_isPanning && _viewport is not null)
        {
            // 1:1 pan: content follows the pointer (v0.3.0 — pure-math helper).
            _viewport.PanByScreen(pos.X - _lastMouse.X, pos.Y - _lastMouse.Y);
            _lastMouse = pos;
            InvalidateVisual();
            return;
        }

        // Left-button drag of a selection or a grip (node edit).
        if (_isNodeDragging)
        {
            GripDragMoved?.Invoke(ToWorld(pos));
            return;
        }

        if (_isDragging && _dragIds.Count > 0 && _viewport is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            Point2 current = ToWorld(pos);
            double dxPx = (current.X - _dragStartWorld.X) * _viewport.PixelsPerWorld;
            double dyPx = -(current.Y - _dragStartWorld.Y) * _viewport.PixelsPerWorld;
            double distPx = Math.Sqrt(dxPx * dxPx + dyPx * dyPx);
            if (distPx > DragStartThresholdPx)
            {
                _dragOffsetWorld = new Vector2(current.X - _dragStartWorld.X, current.Y - _dragStartWorld.Y);
                InvalidateVisual();
            }
        }

        if (_isBoxSelecting)
        {
            UpdateBoxSnapshot(e.GetPosition(this));
            InvalidateVisual();
            return;
        }
    }

    /// <summary>
    /// Recomputes the id list of entities under the live box (v0.3.0). A
    /// left→right drag (window) captures fully contained entities; a
    /// right→left drag (crossing) captures every touched entity. Presentation
    /// only — the actual selection change happens on mouse-up via
    /// <see cref="BoxSelectionCommitted"/>.
    /// </summary>
    private void UpdateBoxSnapshot(Point currentScreen)
    {
        if (_document is null || _viewport is null)
        {
            return;
        }

        Point2 a = ToWorld(_boxStartScreen);
        Point2 b = ToWorld(currentScreen);
        var box = new Bounds(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        bool window = currentScreen.X >= _boxStartScreen.X;

        var ids = new List<long>();
        foreach (IGeometryEntity e in _document.Entities)
        {
            if (!_document.IsVisibleForInteraction(e))
            {
                continue;
            }

            bool inside = window
                ? box.Contains(new Point2(e.Bounds.MinX, e.Bounds.MinY))
                    && box.Contains(new Point2(e.Bounds.MaxX, e.Bounds.MaxY))
                : box.Intersects(e.Bounds);
            if (inside)
            {
                ids.Add(e.Id);
            }
        }

        _boxSnapshot = ids;
    }

    /// <summary>
    /// Cursor feedback (v0.3.0): pan glove while panning, size-all over a
    /// grip, crosshair while a tool owns the mouse, plain arrow otherwise.
    /// </summary>
    private void UpdateCursor(Point screenPos)
    {
        if (_isPanning)
        {
            Cursor = Cursors.SizeAll;
            return;
        }

        if (IsToolActive)
        {
            Cursor = Cursors.Cross;
            return;
        }

        if (_hoveredGrip >= 0)
        {
            Cursor = Cursors.SizeAll;
            return;
        }

        Cursor = Cursors.Arrow;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.ChangedButton is MouseButton.Middle or MouseButton.Right)
        {
            _isPanning = false;
        }

        // Left-button release is handled in OnMouseLeftButtonUp only, so a
        // drag gesture is committed exactly once.
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_isPanning)
        {
            return;
        }

        if (_viewport is null || _document is null)
        {
            return;
        }

        CaptureMouse();
        Focus();

        Point2 world = ToWorld(e.GetPosition(this));

        // An active tool owns every left click: it either commits its gesture
        // or reports a refusal — plain pick/drag never runs in tool modes.
        if (IsToolActive)
        {
            _toolSession!.OnPointerLeftDown(world, _snapMarker ?? SnapResult.None, ToolPickToleranceWorld);
            CaptureMouse();
            e.Handled = true;
            return;
        }

        // Grips take precedence over picking: grabbing a handle on the single
        // selected entity starts a node edit (one gesture → one command).
        if (_hoveredGrip >= 0 && _selection is { Count: 1 } && _selection.PrimaryId is { } primary)
        {
            IGeometryEntity? target = _document.GetEntityById(primary);
            if (target is not null)
            {
                var grips = GripBuilder.Build(target);
                if (_hoveredGrip < grips.Count)
                {
                    _isNodeDragging = true;
                    GripDragStarted?.Invoke(grips[_hoveredGrip]);
                    e.Handled = true;
                    return;
                }
            }
        }

        IGeometryEntity? hit = HitTester.PickClosest(_document, world, PickTolerancePx, _viewport);

        if (hit is null)
        {
            // Empty-space press: arm a box drag (window/crossing selection,
            // v0.3.0). A plain click without a drag clears the selection on
            // mouse-up; see OnMouseLeftButtonUp.
            _boxStartScreen = e.GetPosition(this);
            _isBoxSelecting = true;
            _boxSnapshot = [];
            CaptureMouse();
            e.Handled = true;
            return;
        }

        // Overlap cycling (v0.3.0): clicking the same spot repeatedly walks
        // the candidates sorted by distance. A new click spot starts a fresh
        // cycle at the closest candidate.
        long pickId = hit.Id;
        IReadOnlyList<IGeometryEntity> all = HitTester.PickAll(_document, world, PickTolerancePx, _viewport);
        if (all.Count > 1)
        {
            if (_pickCycleActive
                && DistPx(e.GetPosition(this), _lastClickScreen) <= Interaction.ClickStableRadiusPx
                && _cycleCandidates.Count > 0)
            {
                int current = _cycleCandidates.IndexOf(hit.Id);
                if (current < 0)
                {
                    current = 0;
                }

                _cycleIndex = (current + 1) % all.Count;
                pickId = all[_cycleIndex].Id;
            }
            else
            {
                _cycleIndex = 0;
            }

            _cycleCandidates = all.Select(x => x.Id).ToList();
            _lastClickScreen = e.GetPosition(this);
            _pickCycleActive = true;
        }
        else
        {
            _pickCycleActive = false;
            _cycleCandidates = [];
        }

        // Same gesture: click-select the entity, and carry that entity set as
        // the drag payload when the user starts moving the mouse.
        EntityClicked?.Invoke(pickId, (Keyboard.Modifiers & ModifierKeys.Control) != 0);

        // Drag hit entity; if it is part of a larger selection, drag the whole
        // selection so moving one selected entity moves them all.
        List<long> payload = _selection is not null && _selection.IsSelected(pickId)
            ? _selection.Ids.ToList()
            : [pickId];

        _dragIds = payload;
        _dragStartWorld = world;
        _dragOffsetWorld = Vector2.Zero;
        _isDragging = true;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_isPanning)
        {
            return;
        }

        if (_isNodeDragging)
        {
            _isNodeDragging = false;
            GripDragCommitted?.Invoke();
            ReleaseMouseCapture();
            return;
        }

        if (_isBoxSelecting)
        {
            _isBoxSelecting = false;
            Point pos = e.GetPosition(this);
            double size = Math.Max(
                Math.Abs(pos.X - _boxStartScreen.X),
                Math.Abs(pos.Y - _boxStartScreen.Y));
            if (size >= Interaction.BoxSelectionMinSizePx)
            {
                Point2 a = ToWorld(_boxStartScreen);
                Point2 b = ToWorld(pos);
                var box = new Bounds(
                    Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
                    Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
                bool crossing = pos.X < _boxStartScreen.X;
                bool additive = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                BoxSelectionCommitted?.Invoke(box, additive, crossing);
            }
            else
            {
                // Plain click on empty space: clears the selection.
                EmptySpaceClicked?.Invoke();
            }

            _boxSnapshot = [];
            ReleaseMouseCapture();
            InvalidateVisual();
            return;
        }

        if (_isDragging)
        {
            _isDragging = false;
            if (_dragIds.Count > 0 && _dragOffsetWorld.LengthSquared > 1e-12)
            {
                MoveGestureCommitted?.Invoke(_dragIds.ToList(), _dragOffsetWorld);
            }

            _dragIds.Clear();
            _dragOffsetWorld = Vector2.Zero;
            InvalidateVisual();
        }

        ReleaseMouseCapture();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_isNodeDragging)
        {
            _isNodeDragging = false;
            GripDragCancelled?.Invoke();
        }

        _isDragging = false;
        _dragIds.Clear();
        _dragOffsetWorld = Vector2.Zero;
        InvalidateVisual();
    }
}