#nullable enable

using System.Windows;
using System.Windows.Controls;
using DxfContourStudio.Wpf.ViewModels;

namespace DxfContourStudio.Wpf;

/// <summary>
/// Interaction logic for MainWindow.xaml. The code-behind stays thin: it only
/// wires the viewport control's gesture events into the view model, pushes the
/// control's actual pixel size back for zoom-to-fit and reflects the bottom
/// diagnostics strip's collapse state onto the row height. All logic lives in
/// <see cref="MainViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    private const double DiagnosticsRowExpandedHeight = 180;

    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        ViewportControl.Document = _viewModel.Document;
        ViewportControl.Viewport = _viewModel.Viewport;
        ViewportControl.Selection = _viewModel.Selection;
        ViewportControl.ToolSession = _viewModel.ToolSession;
        ViewportControl.Diagnostics = _viewModel.CurrentDiagnostics;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewportControl.EntityClicked += OnEntityClick;
        ViewportControl.EmptySpaceClicked += OnEmptyClick;
        ViewportControl.BoxSelectionCommitted += OnBoxSelectionCommitted;
        ViewportControl.MoveGestureCommitted += OnMoveCommitted;
        ViewportControl.WorldCursorMoved += OnCursorMoved;
        ViewportControl.PointerLeft += OnPointerLeft;
        _viewModel.SnapChanged += OnSnapChanged;
        _viewModel.HoverChanged += OnHoverChanged;
        _viewModel.NodeEditPreviewChanged += OnNodeEditPreviewChanged;
        ViewportControl.GripDragStarted += OnGripDragStarted;
        ViewportControl.GripDragMoved += OnGripDragMoved;
        ViewportControl.GripDragCommitted += OnGripDragCommitted;
        ViewportControl.GripDragCancelled += OnGripDragCancelled;
        ViewportControl.SizeChanged += OnViewportSizeChanged;
        RefreshViewportSize();
    }

    /// <summary>Keeps the viewport overlay markers in sync with the view model.</summary>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentDiagnostics))
        {
            ViewportControl.Diagnostics = _viewModel.CurrentDiagnostics;
        }
        else if (e.PropertyName == nameof(MainViewModel.CurrentGeometryDiagnostics))
        {
            ViewportControl.GeometryDiagnostics = _viewModel.CurrentGeometryDiagnostics;
        }
    }

    /// <summary>Collapses / expands the bottom diagnostics strip (toggle button).</summary>
    private void OnDiagnosticsToggle(object sender, RoutedEventArgs e)
    {
        DiagnosticsRow.Height = _viewModel.DiagnosticsCollapsed
            ? new GridLength(0)
            : new GridLength(DiagnosticsRowExpandedHeight);
    }

    private void OnViewportSizeChanged(object? sender, SizeChangedEventArgs e) => RefreshViewportSize();

    private void RefreshViewportSize()
    {
        _viewModel.ViewWidth = ViewportControl.ActualWidth;
        _viewModel.ViewHeight = ViewportControl.ActualHeight;
        // A pending auto-fit (set by OpenFile) is applied once a real size is
        // known; never fits with a zero-size viewport.
        _viewModel.NotifyViewportSized();
    }

    private void OnEntityClick(long id, bool additive) => _viewModel.OnEntityClick(id, additive);

    private void OnEmptyClick() => _viewModel.OnEmptyClick();

    private void OnMoveCommitted(System.Collections.Generic.IReadOnlyList<long> ids, DxfContourStudio.Core.Geometry.Vector2 delta)
        => _viewModel.OnMoveCommitted(ids, delta);

    /// <summary>Box drag finished — the VM converts the world box into a selection.</summary>
    private void OnBoxSelectionCommitted(DxfContourStudio.Core.Geometry.Bounds box, bool additive, bool crossing)
        => _viewModel.OnBoxSelectionCommitted(box, additive, crossing);

    private void OnCursorMoved(DxfContourStudio.Core.Geometry.Point2 p) => _viewModel.OnCursorChanged(p);

    /// <summary>Pushes the current hover snap candidate into the viewport overlay.</summary>
    private void OnSnapChanged() => ViewportControl.SnapMarker = _viewModel.CurrentSnap;

    /// <summary>Pushes the hover-highlight entity id into the viewport.</summary>
    private void OnHoverChanged() => ViewportControl.HoveredEntityId = _viewModel.HoveredEntityId;

    /// <summary>Mouse leaves the canvas → the snap marker, status and tool preview clear.</summary>
    private void OnPointerLeft()
    {
        _viewModel.ClearSnap();
        _viewModel.ToolSession.OnPointerLeft();
    }

    /// <summary>Pushes the node-edit preview geometry into the viewport overlay.</summary>
    private void OnNodeEditPreviewChanged()
    {
        ViewportControl.NodeEditPreview = _viewModel.NodeEditPreview;
        if (!_viewModel.IsNodeEditing)
        {
            ViewportControl.NodeEditPreview = null;
        }
    }

    private void OnGripDragStarted(DxfContourStudio.Application.Interaction.GripDescriptor grip)
        => _viewModel.BeginNodeEdit(grip);

    private void OnGripDragMoved(DxfContourStudio.Core.Geometry.Point2 world)
        => _viewModel.NodeEditDrag(world);

    private void OnGripDragCommitted() => _viewModel.CommitNodeEdit();

    private void OnGripDragCancelled() => _viewModel.CancelNodeEdit();
}