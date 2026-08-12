#nullable enable

using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DxfContourStudio.Application.Commands;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Application.Editing;
using DxfContourStudio.Application.Exports;
using DxfContourStudio.Application.Imports;
using DxfContourStudio.Application.Interaction;
using DxfContourStudio.Application.Localization;
using DxfContourStudio.Application.Projects;
using DxfContourStudio.Application.Selection;
using DxfContourStudio.Application.Snapping;
using DxfContourStudio.Core.Contours;
using DxfContourStudio.Core.Diagnostics;
using DxfContourStudio.Core.Geometry;
using DxfContourStudio.Dxf.Abstractions;
using DxfContourStudio.Dxf.Infrastructure;
using DxfContourStudio.Wpf.Localization;

namespace DxfContourStudio.Wpf.ViewModels;

/// <summary>
/// One display row of the properties panel: label and value, both already
/// resolved to the active culture. Rebuilt whenever the culture changes.
/// </summary>
public sealed record PropertyRowItem(string Label, string Value);

/// <summary>
/// One titled section of the properties panel (基本信息 / 几何 / 边界 / 汇总).
/// </summary>
public sealed record PropertyGroupItem(string Title, IReadOnlyList<PropertyRowItem> Rows);

/// <summary>One label/value row of the Import Report panel.</summary>
public sealed record ImportReportItem(string Label, string Value);

/// <summary>One row of the entity statistics table (localized type name + count).</summary>
public sealed record EntityStatItem(string TypeName, int Count);

/// <summary>
/// One row of the Contours panel: id, role label, length/area summary and the
/// underlying contour (kept so the UI can react to selection later).
/// </summary>
public sealed record ContourItemViewModel(string IdText, string TypeText, string Summary, bool IsClosed, Contour Contour);

/// <summary>
/// One row of the Diagnostics panel: severity, localized type, detail, world
/// position, source entity text and the underlying finding. The finding is
/// either a topology <see cref="GapDiagnostic"/> (drive the repair commands)
/// or a geometry <see cref="GeometryDiagnostic"/> (zero length / duplicate /
/// self intersection — display and locate only).
/// </summary>
public sealed record DiagnosticItemViewModel(
    string SeverityText,
    bool IsError,
    string TypeText,
    string Detail,
    string PositionText,
    string EntityText,
    GapDiagnostic? Gap,
    GeometryDiagnostic? Geometry)
{
    /// <summary>True when this row can drive the single-gap repair command.</summary>
    public bool CanRepair => Gap is { CanAutoRepair: true };
}

/// <summary>
/// Main window view model. Holds the document, the pure-math viewport, the
/// command history and the selection, and exposes everything the UI binds to:
/// file open, zoom, undo/redo/delete, layer visibility, the sidebar panels,
/// the import report and the language menu. All heavy lifting is delegated to
/// the Application layer — this class only orchestrates state and forwards the
/// events raised by the viewport control.
///
/// Localization: no user-visible string is hard-coded here. Every label,
/// message and formatted value flows through the localizer or
/// <see cref="DisplayFormat"/>; on culture change all of them are rebuilt.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>The opened drawing (empty until a file is loaded).</summary>
    public CadDocument Document { get; } = new();

    /// <summary>Pure-math viewport state bound to the rendering control.</summary>
    public Viewport Viewport { get; } = new();

    /// <summary>Selection set (entity ids) used by pick interactions.</summary>
    public SelectionModel Selection { get; } = new();

    /// <summary>Undo/redo stack; commands are created by this VM.</summary>
    public CommandHistory History { get; } = new();

    /// <summary>
    /// Interactive editing tool session (D17): owns the Join / Break / Trim /
    /// Extend gesture state machine. The viewport control drives its pointer
    /// events; this VM bridges its requests (status keys, undoable commands,
    /// result selection) into the UI and the history stack. Tool gestures are
    /// executed as undoable commands, so Ctrl+Z always restores the geometry.
    /// </summary>
    public EditToolSession ToolSession { get; }

    /// <summary>Currently active tool (mirrors <see cref="ToolSession.ActiveTool"/>).</summary>
    [ObservableProperty]
    private ToolMode _currentTool = ToolMode.Select;

    /// <summary>What the active tool wants to draw on the canvas (hover preview).</summary>
    public ToolOverlayState ToolOverlay => ToolSession.Overlay;

    /// <summary>Localized tool name shown in the status bar (empty in Select mode).</summary>
    [ObservableProperty]
    private string _toolStatusText = "";

    /// <summary>True while the analysis panels are out of date after a geometry edit.</summary>
    [ObservableProperty]
    private bool _isAnalysisStale;

    /// <summary>Monotonic analysis counter — bumped on every successful (re)analysis.</summary>
    public int AnalysisRevision { get; private set; }

    /// <summary>Grouped rows shown in the properties sidebar.</summary>
    public ObservableCollection<PropertyGroupItem> PropertyGroups { get; } = [];

    /// <summary>Rows shown in the layers sidebar (name / visibility / count).</summary>
    public ObservableCollection<LayerRowViewModel> LayerRows { get; } = [];

    /// <summary>Label/value rows of the Import Report tab.</summary>
    public ObservableCollection<ImportReportItem> ImportReportItems { get; } = [];

    /// <summary>Per-kind entity statistics of the Import Report tab.</summary>
    public ObservableCollection<EntityStatItem> EntityStatItems { get; } = [];

    /// <summary>Rows of the Contours panel (one per assembled contour).</summary>
    public ObservableCollection<ContourItemViewModel> ContourItems { get; } = [];

    /// <summary>Rows of the Diagnostics panel (one per gap / open-end finding).</summary>
    public ObservableCollection<DiagnosticItemViewModel> DiagnosticItems { get; } = [];

    /// <summary>The last contour analysis (null until the user runs Analyze).</summary>
    [ObservableProperty]
    private ContourAnalysisResult? _analysisResult;

    /// <summary>The selected diagnostics row (drives RepairSelectedGap).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RepairSelectedGapCommand))]
    private DiagnosticItemViewModel? _selectedDiagnostic;

    /// <summary>
    /// The diagnostics currently drawn as overlay markers in the viewport
    /// (null/empty when no analysis is active). Raised via PropertyChanged.
    /// </summary>
    public IReadOnlyList<GapDiagnostic>? CurrentDiagnostics { get; private set; }

    /// <summary>Geometry-level findings for the viewport overlay (zero length etc.).</summary>
    public IReadOnlyList<GeometryDiagnostic>? CurrentGeometryDiagnostics { get; private set; }

    /// <summary>True while the bottom diagnostics strip is collapsed.</summary>
    [ObservableProperty]
    private bool _diagnosticsCollapsed;

    /// <summary>True while no analysis ran — shows the Contours panel empty state.</summary>
    public bool ContoursEmptyVisible => ContourItems.Count == 0;

    /// <summary>True while no analysis ran — shows the Diagnostics panel empty state.</summary>
    public bool DiagnosticsEmptyVisible => DiagnosticItems.Count == 0;

    /// <summary>The last import report (null before any import).</summary>
    private DxfImportReport? _lastReport;

    /// <summary>Snap settings for the hover pipeline (session only, not persisted).</summary>
    public SnapSettings SnapSettings { get; } = new();

    /// <summary>Current project file path (null until the user saves one).</summary>
    private string? _projectPath;

    /// <summary>DXF export service (lazy).</summary>
    private readonly DxfExportService _exportService = new(new AcadSharpDxfWriter());

    /// <summary>Unsaved-changes guard; the WPF layer injects the real prompt.</summary>
    private readonly UnsavedChangesGuard _unsavedGuard;

    [ObservableProperty]
    private string _statusText = "";

    /// <summary>Localized window title (bound with compiled {Binding}, see MainWindow.xaml).</summary>
    [ObservableProperty]
    private string _windowTitle = "";    [ObservableProperty]
    private string _cursorStatus = "";

    [ObservableProperty]
    private string _zoomStatus = "";

    [ObservableProperty]
    private string _unitStatus = "";

    [ObservableProperty]
    private string _entityCountStatus = "";

    [ObservableProperty]
    private string _selectedCountStatus = "";

    [ObservableProperty]
    private string? _fileName;

    [ObservableProperty]
    private string? _fileNameTooltip;

    /// <summary>Status-bar text for the snap pipeline ("捕捉：关" / "Snap: Endpoint").</summary>
    [ObservableProperty]
    private string _snapStatusText = "";

    /// <summary>
    /// The hover snap candidate of the current mouse position (D13A). Only the
    /// overlay and the status bar consume it — the document is never modified
    /// by hovering. Raised via <see cref="SnapChanged"/> for the viewport.
    /// </summary>
    public SnapResult? CurrentSnap { get; private set; }

    /// <summary>Raised whenever <see cref="CurrentSnap"/> changes (viewport repaint).</summary>
    public event Action? SnapChanged;

    /// <summary>
    /// Hysteresis layer between the raw snap engine and the UI (v0.3.0 UX
    /// overhaul): a shown marker stays within the release radius instead of
    /// flickering at the acquire boundary, and equal-priority candidates need
    /// a minimum delta before they displace each other.
    /// </summary>
    private readonly SnapHoverController _snapHover = new(InteractionSettings.Default);

    /// <summary>
    /// The entity under the cursor in Select mode (v0.3.0 hover highlight).
    /// Stable: the highlight survives cursor jitter until the previous
    /// candidate is clearly no longer the closest one. Null while a tool
    /// owns the mouse (the tool overlay shows its own highlight).
    /// </summary>
    public long? HoveredEntityId { get; private set; }

    /// <summary>Raised whenever <see cref="HoveredEntityId"/> changes (viewport repaint).</summary>
    public event Action? HoverChanged;

    /// <summary>Tracks the hover highlight across cursor moves (v0.3.0 stability).</summary>
    private long? _hoverPrevId;
    private Point2 _hoverPrevCursor;

    private const double HoverStabilityPx = 2.0;

    /// <summary>Master snap toggle (Tools → 捕捉 / Snap, F3). Session only.</summary>
    [ObservableProperty]
    private bool _snapMasterEnabled = true;

    partial void OnSnapMasterEnabledChanged(bool value)
    {
        SnapSettings.Enabled = value;
        if (!value)
        {
            ClearSnap();
        }
    }

    [ObservableProperty]
    private bool _snapEndpointEnabled = true;

    [ObservableProperty]
    private bool _snapMidpointEnabled = true;

    [ObservableProperty]
    private bool _snapCenterEnabled = true;

    [ObservableProperty]
    private bool _snapIntersectionEnabled = true;

    [ObservableProperty]
    private bool _snapNearestEnabled;

    partial void OnSnapEndpointEnabledChanged(bool value) => SnapSettings.EndpointEnabled = value;

    partial void OnSnapMidpointEnabledChanged(bool value) => SnapSettings.MidpointEnabled = value;

    partial void OnSnapCenterEnabledChanged(bool value) => SnapSettings.CenterEnabled = value;

    partial void OnSnapIntersectionEnabledChanged(bool value) => SnapSettings.IntersectionEnabled = value;

    partial void OnSnapNearestEnabledChanged(bool value) => SnapSettings.NearestEnabled = value;

    /// <summary>F3: flips the master snap switch.</summary>
    [RelayCommand]
    private void ToggleSnap() => SnapMasterEnabled = !SnapMasterEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyViewportVisible))]
    private bool _isOpen;

    [ObservableProperty]
    private bool _isZhCulture;

    [ObservableProperty]
    private bool _isEnCulture;

    /// <summary>True while no DXF is loaded — shows the viewport empty state.</summary>
    public bool EmptyViewportVisible => !IsOpen;

    /// <summary>True while nothing is selected — shows the properties empty state.</summary>
    public bool PropsEmptyVisible => PropertyGroups.Count == 0;

    /// <summary>True while no import happened — shows the report empty state.</summary>
    public bool ReportEmptyVisible => ImportReportItems.Count == 0;

    private readonly DxfImportService _importService;

    public MainViewModel()
        : this(new UnsavedChangesGuard(new UnsavedPromptBox()))
    {
    }

    /// <summary>Creates the view model with a custom unsaved-changes prompt (tests).</summary>
    public MainViewModel(UnsavedChangesGuard unsavedGuard)
    {
        _unsavedGuard = unsavedGuard;
        _importService = new DxfImportService(new AcadSharpDxfReader());
        ToolSession = new EditToolSession(Document, GeometryTolerance.Default);
        ToolSession.OverlayChanged += OnToolSessionOverlayChanged;
        ToolSession.ActiveToolChanged += OnToolSessionActiveToolChanged;
        ToolSession.StatusKeyRequested += OnToolSessionStatusKey;
        ToolSession.CommandRequested += OnToolSessionCommandRequested;
        ToolSession.ResultEntitySelected += OnToolSessionResultSelected;
        Selection.SelectionChanged += OnSelectionChanged;
        History.Changed += OnHistoryChanged;
        Document.DataChanged += OnDocumentDataChanged;
        LocalizationService.Instance.CultureChanged += OnCultureChanged;
        RefreshAllLocalized();
    }

    // ---- tool session bridge (D17) -------------------------------------------

    /// <summary>Overlay changed → the canvas repaints the tool preview.</summary>
    private void OnToolSessionOverlayChanged()
    {
        OnPropertyChanged(nameof(ToolOverlay));
    }

    /// <summary>Tool changed (activation, completion, document swap) → the UI mirrors it.</summary>
    private void OnToolSessionActiveToolChanged()
    {
        CurrentTool = ToolSession.ActiveTool;
        RefreshToolStatusText();
    }

    /// <summary>The session requests a localized status line (key, never text).</summary>
    private void OnToolSessionStatusKey(string key) => StatusText = L(key);

    /// <summary>
    /// Executes a tool-requested undoable command. A refused command keeps the
    /// tool active for retry and reports the engine message; a successful one
    /// switches back to Select through the session's completion flow.
    /// </summary>
    private void OnToolSessionCommandRequested(ICommand command)
    {
        try
        {
            History.Execute(command);
            ToolSession.NotifyCommandCompleted(true);
        }
        catch (Exception ex)
        {
            ToolSession.NotifyCommandCompleted(false);
            StatusText = ex.Message;
        }
    }

    /// <summary>Selects the entity that survived a tool commit (result id).</summary>
    private void OnToolSessionResultSelected(long id)
    {
        if (Document.GetEntityById(id) is not null)
        {
            Selection.SelectSingle(id);
        }
    }

    /// <summary>Activates a tool (Select = no tool owns the mouse). XAML passes the enum name.</summary>
    [RelayCommand]
    private void ActivateTool(string mode)
    {
        if (Enum.TryParse(mode, out ToolMode parsed))
        {
            ActivateToolCore(parsed);
        }
    }

    private void ActivateToolCore(ToolMode mode)
    {
        ToolSession.ActivateTool(mode);
        CurrentTool = mode;
        RefreshToolStatusText();
        if (mode != ToolMode.Select)
        {
            // v0.3.0: a tool owns the canvas but the selection survives the
            // switch — the viewport simply hides grips while a tool is
            // active, so the user does not lose their pick.
            ClearSnap();
            HoveredEntityId = null;
            HoverChanged?.Invoke();
        }
    }

    /// <summary>Esc: cancels the pending gesture, then leaves the tool, then clears the selection.</summary>
    [RelayCommand]
    private void CancelTool()
    {
        if (ToolSession.Cancel())
        {
            return;
        }

        if (CurrentTool != ToolMode.Select)
        {
            ActivateToolCore(ToolMode.Select);
            return;
        }

        if (IsNodeEditing)
        {
            CancelNodeEdit();
            return;
        }

        if (Selection.Count > 0)
        {
            StatusText = L(LocalizationKeys.StatusCleared);
        }

        Selection.Clear();
        ClearSnap();
    }

    private void RefreshToolStatusText()
    {
        ToolStatusText = CurrentTool switch
        {
            ToolMode.Select => "",
            ToolMode.NodeEdit => L(LocalizationKeys.ToolNameNodeEdit),
            ToolMode.Join => L(LocalizationKeys.ToolNameJoin),
            ToolMode.Break => L(LocalizationKeys.ToolNameBreak),
            ToolMode.Trim => L(LocalizationKeys.ToolNameTrim),
            ToolMode.Extend => L(LocalizationKeys.ToolNameExtend),
            _ => "",
        };
    }

    // ---- localization ------------------------------------------------------

    /// <summary>
    /// Rebuilds every culture-dependent string and panel row. Called once at
    /// construction and after every language switch.
    /// </summary>
    private void RefreshAllLocalized()
    {
        var loc = LocalizationService.Instance;
        IsZhCulture = loc.CurrentCulture == LocalizationService.ZhCnName;
        IsEnCulture = loc.CurrentCulture == LocalizationService.EnUsName;
        WindowTitle = loc.Get(LocalizationKeys.AppName);
        StatusText = loc.Get(LocalizationKeys.StatusReady);
        RefreshStatusCounts();
        RefreshUnitStatus();
        RefreshCursorStatus(Point2.Origin);
        RefreshPropertyRows();
        RefreshImportReport();
        RefreshSnapStatusText();
        if (AnalysisResult is not null)
        {
            if (IsAnalysisStale)
            {
                // The panels stay in their stale empty state; only the banner
                // and status text are culture-dependent.
                OnPropertyChanged(nameof(IsAnalysisStale));
            }
            else
            {
                // Rebuild culture-dependent contour / diagnostics text.
                RefreshAnalysis();
            }
        }

        RefreshToolStatusText();
    }

    /// <summary>Rebuilds the snap status line after a culture switch.</summary>
    private void RefreshSnapStatusText()
    {
        if (SnapMasterEnabled && CurrentSnap is { } snap)
        {
            SetSnap(snap);
        }
        else
        {
            SnapStatusText = L(LocalizationKeys.StatusSnapOff);
        }
    }

    private void OnCultureChanged() => RefreshAllLocalized();

    private string L(string key) => LocalizationService.Instance.Get(key);

    private string L(string key, params object?[] args) => LocalizationService.Instance.Get(key, args);

    // ---- document lifecycle -------------------------------------------------

    /// <summary>Opens a DXF via the system file dialog and imports it.</summary>
    [RelayCommand]
    private void Open()
    {
        var (proceed, shouldSave) = _unsavedGuard.ConfirmBeforeDiscard(Document, L(LocalizationKeys.MenuFileOpen));
        if (!proceed)
        {
            return;
        }

        if (shouldSave && !SaveProjectCore())
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = L(LocalizationKeys.MenuFileOpen),
            Filter = L(LocalizationKeys.DialogDxfFilter),
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        OpenFile(dialog.FileName);
    }

    /// <summary>Imports a DXF file into the document (also used by CLI/tests).</summary>
    public void OpenFile(string path)
    {
        var outcome = _importService.Import(path, Document);
        IsOpen = outcome.IsSuccess;
        _lastReport = outcome.Report;

        if (outcome.IsSuccess)
        {
            FileName = Path.GetFileName(path);
            FileNameTooltip = path;
            _projectPath = null;
            History.Clear();
            Selection.Clear();
            ToolSession.ClearToolState();
            ClearAnalysis();
            RefreshLayers();
            Document.IsDirty = false;
            StatusText = L(LocalizationKeys.StatusOpened, outcome.Report!.ImportedCount);
            // Fit the newly imported geometry once the viewport has a real pixel
            // size. The view pushes its size back via NotifyViewportSized after
            // every SizeChanged; comb hing a zero-size fit is avoided.
            _fitPending = true;
            NotifyViewportSized();
        }
        else
        {
            FileName = null;
            FileNameTooltip = null;
            StatusText = L(LocalizationKeys.StatusImportFailed, outcome.ErrorMessage);
        }

        UpdateWindowTitle();
        RefreshStatusCounts();
        RefreshUnitStatus();
        RefreshPropertyRows();
        RefreshImportReport();
    }

    // ---- view commands ------------------------------------------------------

    /// <summary>Fits the viewport to the current (visible) bounds.</summary>
    [RelayCommand]
    private void ZoomToFit()
    {
        var bounds = Document.OverallBounds;
        if (bounds is null)
        {
            StatusText = L(LocalizationKeys.StatusNothingToFit);
            return;
        }

        Viewport.ZoomToFit(bounds.Value, ViewWidth, ViewHeight);
        RefreshZoomStatus();
        StatusText = L(LocalizationKeys.StatusFit);
    }

    /// <summary>Zooms in around the view centre (menu / toolbar).</summary>
    [RelayCommand]
    private void ZoomIn()
    {
        Viewport.ZoomAt(1.25);
        RefreshZoomStatus();
    }

    /// <summary>Zooms out around the view centre (menu / toolbar).</summary>
    [RelayCommand]
    private void ZoomOut()
    {
        Viewport.ZoomAt(0.8);
        RefreshZoomStatus();
    }

    // ---- edit commands -------------------------------------------------------

    private bool CanUndo() => History.CanUndo;
    private bool CanRedo() => History.CanRedo;
    private bool CanDelete() => Selection.Count > 0;
    private bool CanSelectAll() => Document.Entities.Count > 0;

    /// <summary>Undo the most recent command.</summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        ICommand? next = History.PeekUndo();
        History.TryUndo();
        PruneSelectionToDocument();
        StatusText = L(next is MoveEntitiesCommand
            ? LocalizationKeys.StatusUndoMove
            : LocalizationKeys.StatusUndoGeneric);
        RefreshStatusCounts();
    }

    /// <summary>Redo the most recently undone command.</summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        ICommand? next = History.PeekRedo();
        History.TryRedo();
        PruneSelectionToDocument();
        StatusText = L(next is MoveEntitiesCommand
            ? LocalizationKeys.StatusRedoMove
            : LocalizationKeys.StatusRedoGeneric);
        RefreshStatusCounts();
    }

    /// <summary>Delete the selected entities (undoable).</summary>
    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void Delete()
    {
        if (Selection.Count == 0)
        {
            return;
        }

        int count = Selection.Count;
        History.Execute(new DeleteEntitiesCommand(Document, Selection.Ids));
        Selection.Clear();
        StatusText = L(LocalizationKeys.StatusDeleted, count);
        RefreshStatusCounts();
    }

    /// <summary>Selects every visible entity (Ctrl+A).</summary>
    [RelayCommand(CanExecute = nameof(CanSelectAll))]
    private void SelectAll()
    {
        Selection.SelectAll(Document.VisibleEntities.Select(e => e.Id));
    }

    /// <summary>Clears the selection (Esc / click on empty space).</summary>
    [RelayCommand]
    private void ClearSelection()
    {
        // Esc during an active node drag cancels the gesture instead of
        // clearing the selection — the dragged entity stays selected.
        if (IsNodeEditing)
        {
            CancelNodeEdit();
            return;
        }

        if (Selection.Count > 0)
        {
            StatusText = L(LocalizationKeys.StatusCleared);
        }

        Selection.Clear();
        ClearSnap();
    }

    // ---- geometry editing (D4–D7 commands surfaced to the UI) ----------------

    private bool CanJoinSelected() => Selection.Count >= 2;

    /// <summary>Builds the selected entity list in document order.</summary>
    private List<long> SelectedIdsInDocumentOrder()
    {
        var order = new Dictionary<long, int>();
        var entities = Document.Entities;
        for (int i = 0; i < entities.Count; i++)
        {
            order[entities[i].Id] = i;
        }

        var selected = Selection.Ids.Where(order.ContainsKey).ToList();
        selected.Sort((a, b) => order[a].CompareTo(order[b]));
        return selected;
    }

    /// <summary>
    /// Joins all selected entities (document order) into one mixed polyline
    /// as a single undoable transaction. Refused with a status line when the
    /// chain is not endpoint-adjacent.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanJoinSelected))]
    private void JoinSelected()
    {
        List<long> ids = SelectedIdsInDocumentOrder();
        if (ids.Count < 2)
        {
            StatusText = L(LocalizationKeys.StatusEditNeedTwo);
            return;
        }

        try
        {
            History.Execute(new JoinManyCommand(Document, ids, GeometryTolerance.Default));
        }
        catch (ArgumentException ex)
        {
            StatusText = ex.Message;
            return;
        }

        Selection.Clear();
        Selection.SelectSingle(ids[0]);
        StatusText = L(LocalizationKeys.StatusJoined, ids.Count);
        RefreshStatusCounts();
    }

    private bool CanBreakSelected() => Selection.Count == 1;

    /// <summary>
    /// Breaks the single selected entity into two halves at its parameter
    /// midpoint (undoable). Used by the Edit → Break in Half action.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanBreakSelected))]
    private void BreakSelected()
    {
        long id = Selection.Ids.FirstOrDefault();
        IGeometryEntity? entity = Document.GetEntityById(id);
        if (entity is null)
        {
            return;
        }

        var midPoint = entity.PointAtParameter(0.5);
        try
        {
            History.Execute(new BreakEntityCommand(Document, id, midPoint, GeometryTolerance.Default.JoinTolerance));
        }
        catch (ArgumentException ex)
        {
            StatusText = ex.Message;
            return;
        }

        Selection.Clear();
        StatusText = L(LocalizationKeys.StatusBroken);
        RefreshStatusCounts();
    }

    private bool CanTrimSelected() => Selection.Count >= 2;

    /// <summary>Trims the primary (first in document order) with the second as boundary.</summary>
    private void TrimCore(TrimSide side)
    {
        List<long> ids = SelectedIdsInDocumentOrder();
        if (ids.Count < 2)
        {
            StatusText = L(LocalizationKeys.StatusEditNeedLine);
            return;
        }

        try
        {
            History.Execute(new TrimExtendCommand(Document, ids[0], ids[1], side, GeometryTolerance.Default.JoinTolerance));
        }
        catch (ArgumentException ex)
        {
            StatusText = ex.Message;
            return;
        }

        Selection.Clear();
        Selection.SelectSingle(ids[0]);
        StatusText = L(LocalizationKeys.StatusTrimmed);
        RefreshStatusCounts();
    }

    /// <summary>Edit → Trim Start: keeps the primary's start, trims its end to the boundary.</summary>
    [RelayCommand(CanExecute = nameof(CanTrimSelected))]
    private void TrimSelectedStart() => TrimCore(TrimSide.KeepStart);

    /// <summary>Edit → Trim End: keeps the primary's end, trims its start to the boundary.</summary>
    [RelayCommand(CanExecute = nameof(CanTrimSelected))]
    private void TrimSelectedEnd() => TrimCore(TrimSide.KeepEnd);

    // ---- extend (D13A): unique-boundary extension via the existing engine ----

    /// <summary>
    /// Extends the single selected entity towards the unique qualifying
    /// boundary in the document (other visible entities). Ambiguity or the
    /// absence of any qualifying boundary leaves the document untouched and
    /// reports a localized status message.
    /// </summary>
    private bool CanExtendSelected() =>
        Selection.Count == 1 && Document.GetEntityById(Selection.Ids.FirstOrDefault()) is
            LineGeometry or ArcGeometry or PolylineGeometry;

    [RelayCommand(CanExecute = nameof(CanExtendSelected))]
    private void ExtendSelected()
    {
        long id = Selection.Ids.FirstOrDefault();
        IGeometryEntity? target = Document.GetEntityById(id);
        if (target is null)
        {
            return;
        }

        List<IGeometryEntity> candidates = OtherBoundaryCandidates(target);
        var found = ExtendCandidateFinder.FindUniqueExtension(target, candidates, GeometryTolerance.Default.JoinTolerance);
        if (found is null)
        {
            // No qualifying boundary, or several (ambiguous) — never pick at random.
            StatusText = L(LocalizationKeys.StatusNoUniqueExtendBoundary);
            return;
        }

        try
        {
            History.Execute(new TrimExtendCommand(Document, id, found.Value.Boundary.Id, found.Value.Side, GeometryTolerance.Default.JoinTolerance));
        }
        catch (ArgumentException ex)
        {
            StatusText = ex.Message;
            return;
        }

        Selection.SelectSingle(id);
        StatusText = L(LocalizationKeys.StatusExtended);
        RefreshStatusCounts();
    }

    /// <summary>
    /// Other interact-visible entities, fetched through the document's
    /// spatial index (bounds-covered query) so extension never scans the full
    /// document list more than once per invocation.
    /// </summary>
    private List<IGeometryEntity> OtherBoundaryCandidates(IGeometryEntity target)
    {
        double radius = Document.OverallBounds is { } b ? b.DiagonalLength * 2 + 1 : 4096.0;
        return Document.QueryNear(target.Bounds.Center, radius)
            .Where(e => e.Id != target.Id)
            .ToList();
    }

    // ---- snap hover pipeline (D13A) -----------------------------------------

    /// <summary>
    /// Resolves the current hover snap candidate from a world-space cursor
    /// position. Only runs while the master switch is on; candidates come
    /// from the document spatial index, then the pure snap engine picks the
    /// best kind by priority. The document is never modified.
    /// </summary>
    public void UpdateSnap(Point2 worldPoint)
    {
        if (!SnapSettings.Enabled || Document.Entities.Count == 0)
        {
            _snapHover.Reset();
            SetSnap(null);
            return;
        }

        double toleranceWorld = SnapSettings.PixelTolerance > 0
            ? SnapSettings.PixelTolerance / Viewport.PixelsPerWorld
            : 0;
        var candidates = Document.QueryNear(worldPoint, toleranceWorld);
        SnapResult result = SnapEngine.Snap(candidates, worldPoint, toleranceWorld, GeometryTolerance.Default, SnapSettings.EnabledKinds);
        _snapHover.Update(worldPoint, result, Viewport.PixelsPerWorld);
        SetSnap(_snapHover.Current);
    }

    /// <summary>Clears the snap candidate (Esc / tool cancel / mouse leave / document change).</summary>
    public void ClearSnap()
    {
        _snapHover.Reset();
        SetSnap(null);
    }

    private void SetSnap(SnapResult? snap)
    {
        bool changed = !Equals(CurrentSnap, snap);
        CurrentSnap = snap;
        if (changed)
        {
            SnapChanged?.Invoke();
        }

        if (!SnapSettings.Enabled || snap is null)
        {
            if (SnapStatusText != SnapOffText)
            {
                SnapStatusText = SnapOffText;
            }

            return;
        }

        string kindText = snap.Value.Kind switch
        {
            SnapKind.Endpoint => L(LocalizationKeys.SnapKindEndpoint),
            SnapKind.Midpoint => L(LocalizationKeys.SnapKindMidpoint),
            SnapKind.Center => L(LocalizationKeys.SnapKindCenter),
            SnapKind.Intersection => L(LocalizationKeys.SnapKindIntersection),
            _ => L(LocalizationKeys.SnapKindNearest),
        };
        SnapStatusText = L(LocalizationKeys.StatusSnapActive, kindText);
    }

    private string SnapOffText => L(LocalizationKeys.StatusSnapOff);

    // ---- node-edit session (D14) --------------------------------------------

    /// <summary>
    /// The entity state currently being edited by the active grip drag
    /// (null when idle). The document is never mutated per mouse move — this
    /// is a preview only; one <see cref="Commands.NodeEditCommand"/> commits
    /// the whole gesture on mouse-up.
    /// </summary>
    public IGeometryEntity? NodeEditPreview { get; private set; }

    /// <summary>True while a node drag gesture is active (Escape cancels it).</summary>
    public bool IsNodeEditing { get; private set; }

    /// <summary>Raised whenever <see cref="NodeEditPreview"/> or the session state changes.</summary>
    public event Action? NodeEditPreviewChanged;

    private IGeometryEntity? _nodeEditOriginal;
    private GripKind _nodeEditKind;
    private int _nodeEditParameter;
    private Point2 _nodeEditGripStartWorld;
    private bool _nodeEditPreviewUpdated;
    private bool _nodeEditRefused;

    /// <summary>Starts a node drag on the given grip (called by the viewport on grab).</summary>
    public void BeginNodeEdit(GripDescriptor grip)
    {
        if (!grip.Enabled)
        {
            return;
        }

        IGeometryEntity? entity = Document.GetEntityById(grip.EntityId);
        if (entity is null)
        {
            return;
        }

        _nodeEditOriginal = entity;
        _nodeEditKind = grip.Kind;
        _nodeEditParameter = grip.Parameter;
        _nodeEditGripStartWorld = grip.WorldPosition;
        _nodeEditPreviewUpdated = false;
        _nodeEditRefused = false;
        NodeEditPreview = null;
        IsNodeEditing = true;
        NodeEditPreviewChanged?.Invoke();
    }

    /// <summary>
    /// Applies the drag position (snap-filtered, excluding the grabbed grip
    /// itself) to the current session. Refused engine results keep the last
    /// preview — they are reported at commit time.
    /// </summary>
    public void NodeEditDrag(Point2 worldPoint)
    {
        if (!IsNodeEditing || _nodeEditOriginal is null)
        {
            return;
        }

        Point2 target = SnapForNodeDrag(worldPoint) ?? worldPoint;
        IGeometryEntity? edited = ComputeNodeEdit(_nodeEditOriginal, target);
        if (edited is null)
        {
            _nodeEditRefused = true;
            return;
        }

        if (NodeEditValidator.IsValid(edited))
        {
            NodeEditPreview = edited;
            _nodeEditPreviewUpdated = true;
            NodeEditPreviewChanged?.Invoke();
        }
        else
        {
            _nodeEditRefused = true;
        }
    }

    /// <summary>Commits the current preview as exactly one undoable command.</summary>
    public void CommitNodeEdit()
    {
        if (!IsNodeEditing)
        {
            return;
        }

        if (!_nodeEditPreviewUpdated || NodeEditPreview is not { } edited)
        {
            // A refused target mid-gesture is reported; a click without any
            // real move is dropped silently.
            bool refused = _nodeEditRefused;
            CancelNodeEdit();
            if (refused)
            {
                StatusText = L(LocalizationKeys.StatusNodeEditInvalid);
            }

            return;
        }

        if (!NodeEditValidator.IsValid(edited))
        {
            CancelNodeEdit();
            StatusText = L(LocalizationKeys.StatusNodeEditInvalid);
            return;
        }

        History.Execute(new NodeEditCommand(Document, _nodeEditOriginal!, edited, L(LocalizationKeys.StatusNodeEditUpdated)));
        StatusText = L(LocalizationKeys.StatusNodeEditUpdated);
        RefreshStatusCounts();
        CancelNodeEdit();
    }

    /// <summary>Cancels the gesture (Esc / capture loss): the original geometry stays untouched.</summary>
    public void CancelNodeEdit()
    {
        IsNodeEditing = false;
        _nodeEditOriginal = null;
        NodeEditPreview = null;
        _nodeEditPreviewUpdated = false;
        _nodeEditRefused = false;
        NodeEditPreviewChanged?.Invoke();
    }

    /// <summary>Maps a grip onto the node-edit engine call (circle/arc special grips included).</summary>
    private IGeometryEntity? ComputeNodeEdit(IGeometryEntity entity, Point2 target)
    {
        return _nodeEditKind switch
        {
            GripKind.LineStart => NodeEditEngine.MoveNode(entity, 0, target),
            GripKind.LineEnd => NodeEditEngine.MoveNode(entity, 1, target),
            GripKind.CircleCenter => NodeEditEngine.MoveCircleCenter((CircleGeometry)entity, target),
            GripKind.CircleRadius => NodeEditEngine.SetCircleRadius(
                (CircleGeometry)entity, target.DistanceTo(((CircleGeometry)entity).Center)),
            GripKind.ArcCenter => NodeEditEngine.TranslateArcCenter(
                (ArcGeometry)entity, new Point2(target.X - _nodeEditGripStartWorld.X, target.Y - _nodeEditGripStartWorld.Y)),
            GripKind.ArcStart => NodeEditEngine.MoveNode(entity, 0, target),
            GripKind.ArcEnd => NodeEditEngine.MoveNode(entity, 1, target),
            GripKind.PolylineVertex => NodeEditEngine.MoveNode(entity, _nodeEditParameter, target),
            _ => null,
        };
    }

    /// <summary>
    /// Snap resolution for node dragging: the grabbed grip point itself is
    /// excluded so the cursor is never glued back to its own origin; other
    /// points of the same entity stay legal (e.g. closing a polyline vertex).
    /// </summary>
    private Point2? SnapForNodeDrag(Point2 worldPoint)
    {
        if (!SnapSettings.Enabled || Document.Entities.Count == 0)
        {
            return null;
        }

        double toleranceWorld = SnapSettings.PixelTolerance > 0
            ? SnapSettings.PixelTolerance / Viewport.PixelsPerWorld
            : 0;
        if (toleranceWorld <= 0)
        {
            return null;
        }

        var candidates = Document.QueryNear(worldPoint, toleranceWorld);
        SnapResult result = SnapEngine.Snap(candidates, worldPoint, toleranceWorld, GeometryTolerance.Default, SnapSettings.EnabledKinds);
        if (!result.IsValid)
        {
            return null;
        }

        return result.WorldPoint.DistanceTo(_nodeEditGripStartWorld) < toleranceWorld
            ? null
            : result.WorldPoint;
    }

    // ---- contour analysis -----------------------------------------------------

    /// <summary>True while the document holds at least one entity to analyze.</summary>
    private bool CanAnalyze() => Document.Entities.Count > 0;

    /// <summary>
    /// Runs the topology → contour → gap → nesting pipeline and refreshes both
    /// analysis panels and the viewport overlay.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private void Analyze()
    {
        try
        {
            RefreshAnalysis();
        }
        catch (Exception ex)
        {
            // Recoverable analysis failure: surface a localized status line and
            // keep any previous panels/overlay intact instead of letting an
            // unexpected input crash the app. The full exception is logged to
            // the crash/analysis log so the root cause stays diagnosable.
            App.LogUnexpected("Analyze", ex);
            ClearAnalysis();
            StatusText = L(LocalizationKeys.StatusAnalyzeFailed);
            return;
        }

        StatusText = AnalysisResult is null
            ? L(LocalizationKeys.StatusNoContours)
            : L(LocalizationKeys.StatusAnalyzed,
                AnalysisResult.Contours.Count,
                AnalysisResult.ClosedCount,
                AnalysisResult.OpenCount);
        MarkAnalysisFresh();
    }

    /// <summary>Bumps the revision and clears the stale flag after a successful analysis.</summary>
    private void MarkAnalysisFresh()
    {
        IsAnalysisStale = false;
        AnalysisRevision++;
    }

    /// <summary>True while a repairable (auto-repairable) gap row is selected.</summary>
    private bool CanRepairSelectedGap() =>
        SelectedDiagnostic is { Gap: { CanAutoRepair: true } };

    /// <summary>
    /// Repairs the gap of the selected diagnostics row (undoable) and re-runs
    /// the analysis so panels and overlay reflect the new geometry.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRepairSelectedGap))]
    private void RepairSelectedGap()
    {
        if (SelectedDiagnostic is not { Gap: { CanAutoRepair: true } gap })
        {
            return;
        }

        History.Execute(new RepairGapCommand(Document, gap));
        StatusText = L(LocalizationKeys.StatusGapRepaired);
        RefreshAnalysis();
        MarkAnalysisFresh();
        OnPropertyChanged(nameof(RepairSelectedGapCommand));
    }

    /// <summary>True while at least one repairable gap exists in the last analysis.</summary>
    private bool CanRepairAllSafeGaps() =>
        AnalysisResult is { } r && r.Diagnostics.Any(d => d.Kind == GapKind.SmallGap && d.CanAutoRepair);

    /// <summary>
    /// Repairs every auto-repairable gap as one undoable batch (single Ctrl+Z
    /// restores everything) and re-runs the analysis.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRepairAllSafeGaps))]
    private void RepairAllSafeGaps()
    {
        if (AnalysisResult is not { } result)
        {
            return;
        }

        var batch = new BatchRepairCommand(Document, result);
        if (batch.GapCount == 0)
        {
            return;
        }

        History.Execute(batch);
        StatusText = L(LocalizationKeys.StatusBatchRepaired, batch.GapCount);
        RefreshAnalysis();
        MarkAnalysisFresh();
        OnPropertyChanged(nameof(RepairAllSafeGapsCommand));
    }

    /// <summary>
    /// (Re)runs the analysis pipeline and rebuilds the Contours / Diagnostics
    /// collections plus the viewport markers. Status text is left untouched
    /// (the caller decides what to report); no-op while no file is open.
    /// </summary>
    private void RefreshAnalysis()
    {
        if (!IsOpen || Document.Entities.Count == 0)
        {
            ClearAnalysis();
            return;
        }

        ContourAnalysisResult result = ContourAnalyzer.Analyze(Document.Entities.ToList());
        AnalysisResult = result;

        ContourItems.Clear();
        foreach (Contour c in result.Contours)
        {
            ContourItems.Add(new ContourItemViewModel(
                $"#{c.Id}",
                ContourTypeText(c),
                ContourSummary(c),
                c.IsClosed,
                c));
        }

        DiagnosticItems.Clear();
        foreach (GapDiagnostic d in result.Diagnostics)
        {
            DiagnosticItems.Add(DiagnosticItemFromGap(d));
        }

        foreach (GeometryDiagnostic g in result.GeometryDiagnostics)
        {
            DiagnosticItems.Add(DiagnosticItemFromGeometry(g));
        }

        SelectedDiagnostic = null;
        CurrentDiagnostics = new List<GapDiagnostic>(result.Diagnostics);
        CurrentGeometryDiagnostics = new List<GeometryDiagnostic>(result.GeometryDiagnostics);
        OnPropertyChanged(nameof(CurrentDiagnostics));
        OnPropertyChanged(nameof(CurrentGeometryDiagnostics));
        OnPropertyChanged(nameof(ContoursEmptyVisible));
        OnPropertyChanged(nameof(DiagnosticsEmptyVisible));
        AnalyzeCommand.NotifyCanExecuteChanged();
        RepairSelectedGapCommand.NotifyCanExecuteChanged();
        RepairAllSafeGapsCommand.NotifyCanExecuteChanged();
    }

    private DiagnosticItemViewModel DiagnosticItemFromGap(GapDiagnostic d)
    {
        bool error = d.Kind != GapKind.SmallGap;
        return new DiagnosticItemViewModel(
            L(error ? LocalizationKeys.DiagSeverityError : LocalizationKeys.DiagSeverityWarning),
            error,
            L(d.TypeKey),
            DiagnosticDetail(d),
            DisplayFormat.Point(d.PositionA),
            DiagnosticEntityText(d),
            d,
            null);
    }

    private DiagnosticItemViewModel DiagnosticItemFromGeometry(GeometryDiagnostic g)
    {
        bool error = g.Severity == DiagnosticSeverity.Error;
        string severityKey = g.Severity switch
        {
            DiagnosticSeverity.Info => LocalizationKeys.DiagSeverityInfo,
            DiagnosticSeverity.Warning => LocalizationKeys.DiagSeverityWarning,
            _ => LocalizationKeys.DiagSeverityError,
        };
        return new DiagnosticItemViewModel(
            L(severityKey),
            error,
            L(g.TypeKey),
            GeometryDiagnosticDetail(g),
            DisplayFormat.Point(g.PositionA),
            GeometryDiagnosticEntityText(g),
            null,
            g);
    }

    private string GeometryDiagnosticDetail(GeometryDiagnostic g) => g.Kind switch
    {
        DiagnosticKind.ZeroLength => L(LocalizationKeys.DiagDetailZeroLength, DisplayFormat.Length(g.MeasuredLength)),
        DiagnosticKind.VerySmall => L(LocalizationKeys.DiagDetailVerySmall, DisplayFormat.Length(g.MeasuredLength)),
        DiagnosticKind.Duplicate => L(LocalizationKeys.DiagDetailDuplicate, g.EntityIdB, g.EntityIdA),
        DiagnosticKind.SelfIntersection => L(LocalizationKeys.DiagDetailSelfIntersection, g.EntityIdA, g.EntityIdB),
        _ => g.TypeKey,
    };

    private string GeometryDiagnosticEntityText(GeometryDiagnostic g) => g.Kind switch
    {
        DiagnosticKind.Duplicate or DiagnosticKind.SelfIntersection => $"#{g.EntityIdA} ↔ #{g.EntityIdB}",
        _ => $"#{g.EntityIdA}",
    };

    /// <summary>Drops every analysis-derived state (open file / empty drawing).</summary>
    private void ClearAnalysis()
    {
        AnalysisResult = null;
        IsAnalysisStale = false;
        ContourItems.Clear();
        DiagnosticItems.Clear();
        SelectedDiagnostic = null;
        CurrentDiagnostics = null;
        CurrentGeometryDiagnostics = null;
        OnPropertyChanged(nameof(CurrentDiagnostics));
        OnPropertyChanged(nameof(CurrentGeometryDiagnostics));
        OnPropertyChanged(nameof(ContoursEmptyVisible));
        OnPropertyChanged(nameof(DiagnosticsEmptyVisible));
        RepairAllSafeGapsCommand.NotifyCanExecuteChanged();
    }

    private string ContourTypeText(Contour c) => c.IsClosed
        ? c.Role switch
        {
            ContourRole.Outer => L(LocalizationKeys.ContoursOuter),
            ContourRole.Hole => L(LocalizationKeys.ContoursHole),
            ContourRole.Island => L(LocalizationKeys.ContoursIsland),
            _ => L(LocalizationKeys.ContoursClosed),
        }
        : L(LocalizationKeys.ContoursOpen);

    private string ContourSummary(Contour c) => c.IsClosed && c.SignedArea is { } area
        ? L(LocalizationKeys.ContoursRowSummary, DisplayFormat.Length(area), DisplayFormat.Length(c.Length))
        : $"{L(LocalizationKeys.ContoursWarningOpen)} · {DisplayFormat.Length(c.Length)}";

    private string DiagnosticDetail(GapDiagnostic d) => d.Kind switch
    {
        GapKind.SmallGap => L(LocalizationKeys.DiagDetailGap, DisplayFormat.Length(d.Distance)),
        GapKind.OpenContourEnd => d.HasDistance
            ? L(LocalizationKeys.DiagDetailOpenEnd, DisplayFormat.Length(d.Distance))
            : L(LocalizationKeys.DiagDetailNoMatch),
        _ => L(LocalizationKeys.DiagDetailBranch),
    };

    private string DiagnosticEntityText(GapDiagnostic d) => d.Kind == GapKind.SmallGap
        ? $"#{d.EntityIdA} ↔ #{d.EntityIdB}"
        : $"#{d.EntityIdA}";

    // ---- language ------------------------------------------------------------

    /// <summary>Switches the UI culture and persists the choice for next launch.</summary>
    [RelayCommand]
    private void SetLanguage(string cultureName)
    {
        if (!LocalizationService.IsKnownCulture(cultureName))
        {
            return;
        }

        LocalizationService.Instance.SetCulture(cultureName);
        AppSettings.SaveCulture(LocalizationService.Instance.CurrentCulture);
    }

    // ---- window ---------------------------------------------------------------

    /// <summary>Closes the main window (File → Exit).</summary>
    [RelayCommand]
    private void Exit()
    {
        var (proceed, shouldSave) = _unsavedGuard.ConfirmBeforeDiscard(Document, L(LocalizationKeys.MenuFileExit));
        if (!proceed)
        {
            return;
        }

        if (shouldSave && !SaveProjectCore())
        {
            return;
        }

        System.Windows.Application.Current.Shutdown();
    }

    // ---- project save / load ---------------------------------------------------

    /// <summary>True while a project can be saved (document has content).</summary>
    private bool CanSaveProject() => Document.Entities.Count > 0;

    /// <summary>File → Save Project (Ctrl+S). First save opens Save As.</summary>
    [RelayCommand(CanExecute = nameof(CanSaveProject))]
    private void SaveProject()
    {
        if (_projectPath is null)
        {
            SaveProjectAs();
            return;
        }

        SaveProjectCore();
    }

    /// <summary>File → Save Project As.</summary>
    [RelayCommand(CanExecute = nameof(CanSaveProject))]
    private void SaveProjectAs()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = L(LocalizationKeys.MenuFileSaveAs),
            Filter = L(LocalizationKeys.DialogProjectFilter),
            DefaultExt = ".dxfstudio",
            AddExtension = true,
            FileName = (FileName is null ? "project" : Path.GetFileNameWithoutExtension(FileName)) + ".dxfstudio",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (!SaveProjectTo(dialog.FileName))
        {
            return;
        }

        _projectPath = dialog.FileName;
        StatusText = L(LocalizationKeys.StatusProjectSaved, Path.GetFileName(dialog.FileName));
    }

    /// <summary>Saves to the current project path; false when nothing to save or on failure.</summary>
    private bool SaveProjectCore()
    {
        if (Document.Entities.Count == 0)
        {
            StatusText = L(LocalizationKeys.StatusNothingToSave);
            return false;
        }

        if (_projectPath is null)
        {
            SaveProjectAs();
            return _projectPath is not null;
        }

        return SaveProjectTo(_projectPath);
    }

    private bool SaveProjectTo(string path)
    {
        try
        {
            var project = ProjectSerializer.ToProject(Document, GeometryTolerance.Default);
            ProjectSerializer.Save(project, path);
            Document.IsDirty = false;
            UpdateWindowTitle();
            return true;
        }
        catch (Exception ex)
        {
            StatusText = L(LocalizationKeys.StatusProjectSaveFailed, ex.Message);
            App.LogUnexpected("SaveProject", ex);
            return false;
        }
    }

    /// <summary>File → Open Project.</summary>
    [RelayCommand]
    private void OpenProject()
    {
        var (proceed, shouldSave) = _unsavedGuard.ConfirmBeforeDiscard(Document, L(LocalizationKeys.MenuFileOpenProject));
        if (!proceed)
        {
            return;
        }

        if (shouldSave && !SaveProjectCore())
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = L(LocalizationKeys.MenuFileOpenProject),
            Filter = L(LocalizationKeys.DialogProjectFilter),
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        LoadProjectFile(dialog.FileName);
    }

    /// <summary>Loads a .dxfstudio project (also used by tests).</summary>
    public bool LoadProjectFile(string path)
    {
        try
        {
            (CadDocument loaded, GeometryTolerance tolerance) = ProjectSerializer.ToDocument(ProjectSerializer.Load(path));
            Document.ReplaceContent(loaded.Entities.ToList(), loaded.Layers.ToList(), loaded.SourceFilePath, loaded.ImportSummary, null);
            Document.Units = loaded.Units;
            foreach (LayerState layer in loaded.Layers)
            {
                Document.SetLayerVisible(layer.Name, loaded.IsLayerVisible(layer.Name));
            }

            IsOpen = true;
            _projectPath = path;
            _lastReport = null;
            FileName = Path.GetFileName(path);
            FileNameTooltip = path;
            History.Clear();
            Selection.Clear();
            ClearAnalysis();
            RefreshLayers();
            Document.IsDirty = false;
            StatusText = L(LocalizationKeys.StatusProjectLoaded, Path.GetFileName(path));
            _fitPending = true;
            NotifyViewportSized();
            UpdateWindowTitle();
            RefreshStatusCounts();
            RefreshUnitStatus();
            RefreshPropertyRows();
            RefreshImportReport();
            return true;
        }
        catch (Exception ex)
        {
            StatusText = L(LocalizationKeys.StatusProjectLoadFailed, ex.Message);
            App.LogUnexpected("OpenProject", ex);
            return false;
        }
    }

    // ---- clean DXF export -------------------------------------------------------

    /// <summary>True while there is geometry to export.</summary>
    private bool CanExportCleanDxf() => Document.Entities.Count > 0;

    /// <summary>File → Export Clean DXF (Save As dialog, never overwrites source by default).</summary>
    [RelayCommand(CanExecute = nameof(CanExportCleanDxf))]
    private void ExportCleanDxf()
    {
        if (Document.Entities.Count == 0)
        {
            StatusText = L(LocalizationKeys.StatusNothingToExport);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = L(LocalizationKeys.MenuFileExportClean),
            Filter = L(LocalizationKeys.DialogExportFilter),
            DefaultExt = ".dxf",
            AddExtension = true,
            FileName = (FileName is null ? "export" : Path.GetFileNameWithoutExtension(FileName)) + "_cleaned.dxf",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        ExportCleanDxfTo(dialog.FileName);
    }

    /// <summary>Exports to the given path (also used by tests).</summary>
    public bool ExportCleanDxfTo(string path)
    {
        try
        {
            var options = new DxfExportOptions
            {
                OutputUnit = Document.Units,
                OverwriteSource = false,
            };
            DxfExportReport report = _exportService.Export(Document, path, options);
            if (report.ErrorCount > 0)
            {
                StatusText = L(LocalizationKeys.StatusExportFailed, report.Messages.FirstOrDefault() ?? "unknown error");
                return false;
            }

            StatusText = L(LocalizationKeys.StatusExportDone, Path.GetFileName(path), report.WrittenCount);
            return true;
        }
        catch (Exception ex)
        {
            StatusText = L(LocalizationKeys.StatusExportFailed, ex.Message);
            App.LogUnexpected("ExportCleanDxf", ex);
            return false;
        }
    }

    // ---- anomaly navigation -----------------------------------------------------

    /// <summary>True while at least one diagnostic row exists.</summary>
    private bool CanNavigateAnomalies() => DiagnosticItems.Count > 0;

    /// <summary>Selects the next diagnostic row (wrap-around).</summary>
    [RelayCommand(CanExecute = nameof(CanNavigateAnomalies))]
    private void NextAnomaly()
    {
        if (DiagnosticItems.Count == 0)
        {
            return;
        }

        int idx = SelectedDiagnostic is null ? -1 : DiagnosticItems.IndexOf(SelectedDiagnostic);
        int next = (idx + 1) % DiagnosticItems.Count;
        SelectedDiagnostic = DiagnosticItems[next];
        StatusText = L(LocalizationKeys.StatusAnomalyLocated, DiagnosticItems[next].TypeText);
    }

    /// <summary>Selects the previous diagnostic row (wrap-around).</summary>
    [RelayCommand(CanExecute = nameof(CanNavigateAnomalies))]
    private void PrevAnomaly()
    {
        if (DiagnosticItems.Count == 0)
        {
            return;
        }

        int idx = SelectedDiagnostic is null ? 0 : DiagnosticItems.IndexOf(SelectedDiagnostic);
        int prev = (idx - 1 + DiagnosticItems.Count) % DiagnosticItems.Count;
        SelectedDiagnostic = DiagnosticItems[prev];
        StatusText = L(LocalizationKeys.StatusAnomalyLocated, DiagnosticItems[prev].TypeText);
    }

    /// <summary>Shows the about dialog (Help → About).</summary>
    [RelayCommand]
    private void About()
    {
        var dialog = new DxfContourStudio.Wpf.Views.AboutDialog
        {
            Owner = System.Windows.Application.Current?.MainWindow,
        };
        dialog.ShowDialog();
    }

    // ---- layers --------------------------------------------------------------

    /// <summary>Refreshes the layer sidebar from the document (import/clear).</summary>
    public void RefreshLayers()
    {
        LayerRows.Clear();
        foreach (LayerState layer in Document.Layers)
        {
            LayerRows.Add(new LayerRowViewModel(
                this,
                layer.Name,
                Document.IsLayerVisible(layer.Name),
                Document.EntityCountOnLayer(layer.Name)));
        }
    }

    /// <summary>Called by a <see cref="LayerRowViewModel"/> when its checkbox flips.</summary>
    public void SetLayerVisibility(string layerName, bool visible)
    {
        Document.SetLayerVisible(layerName, visible);
        // entities on hidden layers are excluded from rendering/picking; they
        // must not remain selected either.
        Selection.Prune(id => Document.GetEntityById(id) is { } e && Document.IsVisibleForInteraction(e));
        StatusText = L(visible ? LocalizationKeys.StatusLayerShown : LocalizationKeys.StatusLayerHidden, layerName);
        RefreshStatusCounts();
    }

    /// <summary>Makes every layer visible again.</summary>
    [RelayCommand]
    private void ShowAllLayers()
    {
        Document.ShowAllLayers();
        Selection.Prune(id => Document.GetEntityById(id) is { } e && Document.IsVisibleForInteraction(e));
        foreach (LayerRowViewModel row in LayerRows)
        {
            row.IsVisible = true;
        }

        StatusText = L(LocalizationKeys.StatusAllShown);
        RefreshStatusCounts();
    }

    /// <summary>Hides every known layer.</summary>
    [RelayCommand]
    private void HideAllLayers()
    {
        Document.HideAllLayers();
        Selection.Prune(id => Document.GetEntityById(id) is { } e && Document.IsVisibleForInteraction(e));
        foreach (LayerRowViewModel row in LayerRows)
        {
            row.IsVisible = false;
        }

        StatusText = L(LocalizationKeys.StatusAllHidden);
        RefreshStatusCounts();
    }

    // ---- viewport events (called by MainWindow code-behind) --------------------

    // ---- viewport events (called by MainWindow code-behind) --------------------

    /// <summary>True while an auto-fit is waiting for a real viewport size.</summary>
    private bool _fitPending;

    /// <summary>
    /// Called by the window after every viewport resize. Fits the document once
    /// a real pixel size is known (a pending auto-fit set by <see cref="OpenFile"/>);
    /// skips when the size is still unset (avoiding a zero-size fit).
    /// </summary>
    public void NotifyViewportSized()
    {
        if (!_fitPending || ViewWidth <= 0 || ViewHeight <= 0)
        {
            return;
        }

        _fitPending = false;
        if (Document.OverallBounds is not { } bounds)
        {
            return;
        }

        Viewport.ZoomToFit(bounds, ViewWidth, ViewHeight);
        RefreshZoomStatus();
    }

    /// <summary>Handles a click-pick: plain click selects one, Ctrl+click toggles.</summary>
    public void OnEntityClick(long id, bool additive) => Selection.ApplyClickPick(id, additive);

    /// <summary>Clears the selection (click on empty space / Esc).</summary>
    public void OnEmptyClick() => Selection.Clear();

    /// <summary>
    /// Commits a completed box drag: window (left→right, fully contained) or
    /// crossing (right→left, touched) selection; Ctrl = additive.
    /// </summary>
    public void OnBoxSelectionCommitted(Bounds box, bool additive, bool crossing)
    {
        IReadOnlyList<long> ids = BoxSelector.SelectIds(Document, box, crossing);
        if (additive)
        {
            Selection.AddRange(ids);
        }
        else
        {
            Selection.ReplaceWith(ids);
        }

        RefreshStatusCounts();
    }

    /// <summary>Commits a completed move gesture as exactly one undoable command.</summary>
    public void OnMoveCommitted(IReadOnlyList<long> ids, Vector2 delta)
    {
        History.Execute(new MoveEntitiesCommand(Document, ids, delta));
        StatusText = L(LocalizationKeys.StatusMoved, ids.Count);
        RefreshStatusCounts();
    }

    /// <summary>Updates the cursor status line and the snap candidate with the current world position.</summary>
    public void OnCursorChanged(Point2 p)
    {
        if (IsNodeEditing)
        {
            // While dragging a grip the same motion feeds the drag preview
            // (snap with self-exclusion) instead of the hover pipeline.
            NodeEditDrag(p);
            return;
        }

        RefreshCursorStatus(p);
        UpdateSnap(p);
        UpdateHover(p);
        // In tool modes the hover preview is driven by the viewport control
        // (it owns the pick tolerance and the mouse events); the VM only
        // keeps the snap pipeline alive so the session sees the candidate.
    }

    /// <summary>
    /// Select-mode hover highlight with stability tolerance (v0.3.0): the
    /// highlighted entity only changes when another candidate wins by a
    /// clear margin, so jitter at a shared edge does not flicker the glow.
    /// </summary>
    private void UpdateHover(Point2 p)
    {
        bool toolActive = CurrentTool is ToolMode.Join or ToolMode.Break or ToolMode.Trim or ToolMode.Extend;
        double hitTolerancePx = InteractionSettings.Default.HitTolerancePx;
        IGeometryEntity? best = !toolActive && Document.Entities.Count > 0
            ? HitTester.PickClosest(Document, p, hitTolerancePx, Viewport)
            : null;

        long? next = best?.Id;
        if (next is null
            && _hoverPrevId is not null
            && Document.GetEntityById(_hoverPrevId.Value) is { } prev
            && p.DistanceTo(_hoverPrevCursor) <= Viewport.PixelsToWorld(hitTolerancePx + HoverStabilityPx))
        {
            // Cursor jittered just outside the pick radius while still close
            // to the previous hover — keep the highlight until clearly away.
            next = _hoverPrevId;
        }

        if (HoveredEntityId == next)
        {
            _hoverPrevCursor = p;
            return;
        }

        HoveredEntityId = next;
        _hoverPrevId = next;
        _hoverPrevCursor = p;
        HoverChanged?.Invoke();
    }

    // ---- internals -----------------------------------------------------------

    private void OnSelectionChanged()
    {
        RefreshSelectedCount();
        RefreshPropertyRows();
    }

    private void OnHistoryChanged()
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        PruneSelectionToDocument();
        if (AnalysisResult is not null)
        {
            // Product semantics: any geometry change (undo/redo, move, delete,
            // node edit, tool commands) makes the previous analysis results
            // stale. The result object is kept (re-analysis is one click),
            // but every panel row and viewport overlay marker is dropped and
            // the stale banner appears. Gap repair is the exception — it
            // re-runs the analysis itself (see RepairSelectedGap).
            MarkAnalysisStale();
        }
    }

    /// <summary>Marks the current analysis as out of date and clears its overlays/rows.</summary>
    private void MarkAnalysisStale()
    {
        if (AnalysisResult is null)
        {
            return;
        }

        IsAnalysisStale = true;
        SelectedDiagnostic = null;
        ContourItems.Clear();
        DiagnosticItems.Clear();
        CurrentDiagnostics = null;
        CurrentGeometryDiagnostics = null;
        OnPropertyChanged(nameof(CurrentDiagnostics));
        OnPropertyChanged(nameof(CurrentGeometryDiagnostics));
        OnPropertyChanged(nameof(ContoursEmptyVisible));
        OnPropertyChanged(nameof(DiagnosticsEmptyVisible));
        RepairSelectedGapCommand.NotifyCanExecuteChanged();
        RepairAllSafeGapsCommand.NotifyCanExecuteChanged();
    }

    private void PruneSelectionToDocument()
    {
        Selection.Prune(id => Document.GetEntityById(id) is { } e && Document.IsVisibleForInteraction(e));
        RefreshStatusCounts();
    }

    private void RefreshStatusCounts()
    {
        EntityCountStatus = $"{L(LocalizationKeys.StatusEntities)} {DisplayFormat.Count(Document.Entities.Count)}";
        RefreshSelectedCount();
        RefreshZoomStatus();
        DeleteCommand.NotifyCanExecuteChanged();
        JoinSelectedCommand.NotifyCanExecuteChanged();
        BreakSelectedCommand.NotifyCanExecuteChanged();
        TrimSelectedStartCommand.NotifyCanExecuteChanged();
        TrimSelectedEndCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        SelectAllCommand.NotifyCanExecuteChanged();
        AnalyzeCommand.NotifyCanExecuteChanged();
        RepairSelectedGapCommand.NotifyCanExecuteChanged();
        RepairAllSafeGapsCommand.NotifyCanExecuteChanged();
        SaveProjectCommand.NotifyCanExecuteChanged();
        SaveProjectAsCommand.NotifyCanExecuteChanged();
        ExportCleanDxfCommand.NotifyCanExecuteChanged();
        ExtendSelectedCommand.NotifyCanExecuteChanged();
        NextAnomalyCommand.NotifyCanExecuteChanged();
        PrevAnomalyCommand.NotifyCanExecuteChanged();
    }

    private void RefreshSelectedCount()
    {
        SelectedCountStatus = $"{L(LocalizationKeys.StatusSelected)} {DisplayFormat.Count(Selection.Count)}";
    }

    private void RefreshZoomStatus()
    {
        ZoomStatus = $"{L(LocalizationKeys.StatusZoom)} {DisplayFormat.ZoomPercent(Viewport.PixelsPerWorld)}";
    }

    private void RefreshUnitStatus()
    {
        LengthUnit unit = _lastReport?.InterpretedUnits ?? LengthUnit.Millimeter;
        UnitStatus = $"{L(LocalizationKeys.StatusUnit)} {L(ImportReportBuilder.LocalizedUnit(unit))}";
    }

    private void RefreshCursorStatus(Point2 p)
    {
        CursorStatus = $"{L(LocalizationKeys.StatusX)} {DisplayFormat.Coordinate(p.X)}  " +
                       $"{L(LocalizationKeys.StatusY)} {DisplayFormat.Coordinate(p.Y)}";
    }

    private void RefreshPropertyRows()
    {
        PropertyGroups.Clear();        IReadOnlyCollection<IGeometryEntity>? selected = Selection.Count == 0
            ? null
            : Selection.Ids
                .Select(Document.GetEntityById)
                .Where(e => e is not null)
                .Cast<IGeometryEntity>()
                .ToList();

        // order: 基本信息 → 几何 → 边界 → 汇总; empty groups are skipped.
        string[] groupOrder =
        [
            LocalizationKeys.GroupBasic,
            LocalizationKeys.GroupGeometry,
            LocalizationKeys.GroupBounds,
            LocalizationKeys.GroupMulti,
        ];

        foreach (string group in groupOrder)
        {
            var rows = EntityPropertyBuilder.Build(Document, selected)
                .Where(r => r.GroupKey == group)
                .Select(r => new PropertyRowItem(
                    L(r.NameKey),
                    r.ValueKey is null ? r.Value : L(r.ValueKey)))
                .ToList();
            if (rows.Count == 0)
            {
                continue;
            }

            PropertyGroups.Add(new PropertyGroupItem(L(GroupTitleKey(group)), rows));
        }

        OnPropertyChanged(nameof(PropsEmptyVisible));
    }

    private static string GroupTitleKey(string group) => group switch
    {
        LocalizationKeys.GroupBasic => LocalizationKeys.PropsGroupBasic,
        LocalizationKeys.GroupGeometry => LocalizationKeys.PropsGroupGeometry,
        LocalizationKeys.GroupBounds => LocalizationKeys.PropsGroupBounds,
        LocalizationKeys.GroupMulti => LocalizationKeys.PropsGroupMulti,
        _ => LocalizationKeys.PropsGroupDocument,
    };

    private void RefreshImportReport()
    {
        ImportReportItems.Clear();
        EntityStatItems.Clear();

        if (_lastReport is null)
        {
            return;
        }

        foreach (ReportRow row in ImportReportBuilder.Build(_lastReport))
        {
            ImportReportItems.Add(new ImportReportItem(L(row.NameKey), row.Value));
        }

        foreach (EntityStatRow row in ImportReportBuilder.BuildStatistics(_lastReport))
        {
            EntityStatItems.Add(new EntityStatItem(L(row.TypeKey), row.Count));
        }

        OnPropertyChanged(nameof(ReportEmptyVisible));
    }

    /// <summary>Viewport pixel size — set by the view when the control resizes.</summary>
    public double ViewWidth { get; set; } = 1000;

    public double ViewHeight { get; set; } = 800;

    // ---- dirty tracking ------------------------------------------------------

    private void OnDocumentDataChanged()
    {
        UpdateWindowTitle();
        // Geometry may have moved — a stale snap candidate is meaningless.
        ClearSnap();
        // A pending tool gesture may reference entities that just changed.
        ToolSession.OnDocumentChanged();
    }

    private void UpdateWindowTitle()
    {
        string baseTitle = L(LocalizationKeys.AppName);
        string suffix = Document.IsDirty ? " *" : "";
        WindowTitle = FileName is null ? baseTitle + suffix : $"{baseTitle} - {FileName}{suffix}";
    }
}

/// <summary>
/// The production unsaved-changes prompt: a WPF message box with
/// Save / Discard / Cancel. The Application layer only sees the result.
/// </summary>
internal sealed class UnsavedPromptBox : IUnsavedChangesPrompt
{
    public UnsavedPromptResult Ask(string context)
    {
        var result = System.Windows.MessageBox.Show(
            $"The document has unsaved changes. Save before {context}?",
            "Unsaved changes",
            System.Windows.MessageBoxButton.YesNoCancel,
            System.Windows.MessageBoxImage.Warning);
        return result switch
        {
            System.Windows.MessageBoxResult.Yes => UnsavedPromptResult.Save,
            System.Windows.MessageBoxResult.No => UnsavedPromptResult.Discard,
            _ => UnsavedPromptResult.Cancel,
        };
    }
}
