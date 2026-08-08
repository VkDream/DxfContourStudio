#nullable enable

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DxfContourStudio.Application.Documents;
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

    private const double PickTolerancePx = 6.0;
    private const double MaxSegmentSagittaPx = 0.5;
    private const double DragStartThresholdPx = 3.0;

    private Point _lastMouse;
    private bool _isPanning;

    // Drag preview state. Entities are never mutated live: we keep the world
    // offset and draw the dragged entities shifted by it; on mouse-up one
    // MoveEntitiesCommand is raised with the accumulated delta.
    private List<long> _dragIds = [];
    private Point2 _dragStartWorld;
    private Vector2 _dragOffsetWorld;
    private bool _isDragging;

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
            _document = value;
            InvalidateVisual();
        }
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

    /// <summary>Raised for a click-pick: id plus whether the user held Ctrl (additive).</summary>
    public event Action<long, bool>? EntityClicked;

    /// <summary>Raised when the user clicks empty space (a plain click, no drag).</summary>
    public event Action? EmptySpaceClicked;

    /// <summary>
    /// Raised once when a drag-gesture that actually moved geometry ends.
    /// Carries the moved ids and the final world-space delta.
    /// </summary>
    public event Action<IReadOnlyList<long>, Vector2>? MoveGestureCommitted;

    /// <summary>Raised continuously with the world point under the mouse.</summary>
    public event Action<Point2>? WorldCursorMoved;

    private static readonly Pen SelectionPreviewPen = FreezePen(new Pen(new SolidColorBrush(Color.FromRgb(90, 120, 220)), 1.0));

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

        // One pass draws the geometry for both normal and preview state.
        foreach (var entity in _document.Entities)
        {
            if (!_document.IsVisibleForInteraction(entity))
            {
                continue;
            }

            IGeometryEntity toDraw = _isDragging && IsInDrag(entity.Id) && _dragOffsetWorld.LengthSquared > 0
                ? entity.Transformed(Transform2.CreateTranslation(_dragOffsetWorld))
                : entity;

            Pen pen = _isDragging && IsInDrag(entity.Id)
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
    }

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

    private static void DrawLineRun(DrawingContext dc, Pen pen, Point a, Point b)
    {
        dc.DrawLine(pen, a, b);
    }

    private Point ToScreen(Point2 world) => _viewport is null
        ? new Point(world.X, world.Y)
        : ToScreen(world, _viewport, Math.Max(ActualWidth, 1), Math.Max(ActualHeight, 1));

    private Point2 ToWorld(Point screen) => ScreenToWorld(_viewport!, new Point2(screen.X, screen.Y), Math.Max(ActualWidth, 1), Math.Max(ActualHeight, 1));

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
        Point2 worldBefore = ScreenToWorld(_viewport, new Point2(screen.X, screen.Y), w, h);

        double factor = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
        _viewport.ZoomAt(factor);

        Point2 worldAfter = ScreenToWorld(_viewport, new Point2(screen.X, screen.Y), w, h);
        _viewport.Pan(new Point2(worldBefore.X - worldAfter.X, worldBefore.Y - worldAfter.Y));
        InvalidateVisual();
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

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Point pos = e.GetPosition(this);

        if (_viewport is not null)
        {
            WorldCursorMoved?.Invoke(ToWorld(pos));
        }

        if (_isPanning && _viewport is not null)
        {
            double dxWorld = -(pos.X - _lastMouse.X) / _viewport.PixelsPerWorld;
            double dyWorld = (pos.Y - _lastMouse.Y) / _viewport.PixelsPerWorld;
            _viewport.Pan(new Point2(dxWorld, dyWorld));
            _lastMouse = pos;
            InvalidateVisual();
            return;
        }

        // Left-button drag of a selection.
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
        IGeometryEntity? hit = HitTester.PickClosest(_document, world, PickTolerancePx, _viewport);

        if (hit is null)
        {
            EmptySpaceClicked?.Invoke();
            return;
        }

        // Same gesture: click-select the entity, and carry that entity set as
        // the drag payload when the user starts moving the mouse.
        EntityClicked?.Invoke(hit.Id, (Keyboard.Modifiers & ModifierKeys.Control) != 0);

        // Drag hit entity; if it is part of a larger selection, drag the whole
        // selection so moving one selected entity moves them all.
        List<long> payload = _selection is not null && _selection.IsSelected(hit.Id)
            ? _selection.Ids.ToList()
            : [hit.Id];

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
        _isDragging = false;
        _dragIds.Clear();
        _dragOffsetWorld = Vector2.Zero;
        InvalidateVisual();
    }
}