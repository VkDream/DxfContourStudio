#nullable enable

using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DxfContourStudio.Application.Commands;
using DxfContourStudio.Application.Documents;
using DxfContourStudio.Application.Exports;
using DxfContourStudio.Application.Imports;
using DxfContourStudio.Application.Localization;
using DxfContourStudio.Application.Projects;
using DxfContourStudio.Application.Selection;
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
        Selection.SelectionChanged += OnSelectionChanged;
        History.Changed += OnHistoryChanged;
        Document.DataChanged += OnDocumentDataChanged;
        LocalizationService.Instance.CultureChanged += OnCultureChanged;
        RefreshAllLocalized();
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
        if (AnalysisResult is not null)
        {
            // Rebuild culture-dependent contour / diagnostics text.
            RefreshAnalysis();
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
        if (Selection.Count > 0)
        {
            StatusText = L(LocalizationKeys.StatusCleared);
        }

        Selection.Clear();
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

    /// <summary>Commits a completed move gesture as exactly one undoable command.</summary>
    public void OnMoveCommitted(IReadOnlyList<long> ids, Vector2 delta)
    {
        History.Execute(new MoveEntitiesCommand(Document, ids, delta));
        StatusText = L(LocalizationKeys.StatusMoved, ids.Count);
        RefreshStatusCounts();
    }

    /// <summary>Updates the cursor status line with the current world position.</summary>
    public void OnCursorChanged(Point2 p) => RefreshCursorStatus(p);

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
            // Undo/redo can change geometry → keep panels and overlay in sync.
            RefreshAnalysis();
        }
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
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        SelectAllCommand.NotifyCanExecuteChanged();
        AnalyzeCommand.NotifyCanExecuteChanged();
        RepairSelectedGapCommand.NotifyCanExecuteChanged();
        RepairAllSafeGapsCommand.NotifyCanExecuteChanged();
        SaveProjectCommand.NotifyCanExecuteChanged();
        SaveProjectAsCommand.NotifyCanExecuteChanged();
        ExportCleanDxfCommand.NotifyCanExecuteChanged();
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
