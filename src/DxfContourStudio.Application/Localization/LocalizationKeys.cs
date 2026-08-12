#nullable enable

namespace DxfContourStudio.Application.Localization;

/// <summary>
/// The full set of resource keys used by the UI, as constants. Defining them
/// once in code (rather than free string literals spread through ViewModels)
/// is what lets the consistency tests and the property builder share one
/// source of truth, and makes "新增 UI 字符串必须走 Localization" enforceable.
/// </summary>
public static class LocalizationKeys
{
    // ---- application ----
    public const string AppName = "App.Name";

    // ---- menu ----
    public const string MenuFile = "Menu.File";
    public const string MenuFileOpen = "Menu.File.Open";
    public const string MenuFileOpenProject = "Menu.File.OpenProject";
    public const string MenuFileSave = "Menu.File.Save";
    public const string MenuFileSaveAs = "Menu.File.SaveAs";
    public const string MenuFileExport = "Menu.File.Export";
    public const string MenuFileExportClean = "Menu.File.ExportClean";
    public const string MenuFileExit = "Menu.File.Exit";
    public const string MenuEdit = "Menu.Edit";
    public const string MenuEditUndo = "Menu.Edit.Undo";
    public const string MenuEditRedo = "Menu.Edit.Redo";
    public const string MenuEditDelete = "Menu.Edit.Delete";
    public const string MenuEditSelectAll = "Menu.Edit.SelectAll";
    public const string MenuEditClearSelection = "Menu.Edit.ClearSelection";
    public const string MenuEditJoin = "Menu.Edit.Join";
    public const string MenuEditBreak = "Menu.Edit.Break";
    public const string MenuEditTrimStart = "Menu.Edit.TrimStart";
    public const string MenuEditTrimEnd = "Menu.Edit.TrimEnd";
    public const string MenuEditExtend = "Menu.Edit.Extend";
    public const string MenuEditToolsSelect = "Menu.Edit.Tools.Select";
    public const string MenuEditToolsNodeEdit = "Menu.Edit.Tools.NodeEdit";
    public const string MenuEditToolsJoin = "Menu.Edit.Tools.Join";
    public const string MenuEditToolsBreak = "Menu.Edit.Tools.Break";
    public const string MenuEditToolsTrim = "Menu.Edit.Tools.Trim";
    public const string MenuEditToolsExtend = "Menu.Edit.Tools.Extend";
    public const string MenuToolsSnap = "Menu.Tools.Snap";
    public const string MenuToolsSnapSettings = "Menu.Tools.SnapSettings";
    public const string SnapKindEndpoint = "Snap.Kind.Endpoint";
    public const string SnapKindMidpoint = "Snap.Kind.Midpoint";
    public const string SnapKindCenter = "Snap.Kind.Center";
    public const string SnapKindIntersection = "Snap.Kind.Intersection";
    public const string SnapKindNearest = "Snap.Kind.Nearest";
    public const string StatusExtended = "Status.Extended";
    public const string StatusNoUniqueExtendBoundary = "Status.NoUniqueExtendBoundary";
    public const string StatusNodeEditUpdated = "Status.NodeEditUpdated";
    public const string StatusNodeEditInvalid = "Status.NodeEditInvalid";
    public const string StatusSnapOff = "Status.SnapOff";
    public const string StatusSnapActive = "Status.SnapActive";
    public const string StatusJoined = "Status.Joined";
    public const string StatusBroken = "Status.Broken";
    public const string StatusTrimmed = "Status.Trimmed";
    public const string StatusEditNeedTwo = "Status.EditNeedTwo";
    public const string StatusEditNeedOne = "Status.EditNeedOne";
    public const string StatusEditNeedLine = "Status.EditNeedLine";
    public const string MenuView = "Menu.View";
    public const string MenuViewFitAll = "Menu.View.FitAll";
    public const string MenuViewZoomIn = "Menu.View.ZoomIn";
    public const string MenuViewZoomOut = "Menu.View.ZoomOut";
    public const string MenuViewShowAllLayers = "Menu.View.ShowAllLayers";
    public const string MenuViewHideAllLayers = "Menu.View.HideAllLayers";
    public const string MenuAnalyze = "Menu.Analyze";
    public const string MenuAnalyzeContours = "Menu.Analyze.Contours";
    public const string MenuAnalyzeDiagnostics = "Menu.Analyze.Diagnostics";
    public const string MenuTools = "Menu.Tools";
    public const string MenuToolsSettings = "Menu.Tools.Settings";
    public const string MenuToolsLanguage = "Menu.Tools.Language";
    public const string MenuToolsLanguageZhCn = "Menu.Tools.Language.ZhCn";
    public const string MenuToolsLanguageUs = "Menu.Tools.Language.Us";
    public const string MenuHelp = "Menu.Help";
    public const string MenuHelpAbout = "Menu.Help.About";
    public const string AboutText = "About.Text";

    // ---- toolbar ----
    public const string ToolbarOpen = "Toolbar.Open";
    public const string ToolbarOpenProject = "Toolbar.OpenProject";
    public const string ToolbarSave = "Toolbar.Save";
    public const string ToolbarSaveProject = "Toolbar.SaveProject";
    public const string ToolbarExportClean = "Toolbar.ExportClean";
    public const string ToolbarRepairAll = "Toolbar.RepairAll";
    public const string ToolbarUndo = "Toolbar.Undo";
    public const string ToolbarRedo = "Toolbar.Redo";
    public const string ToolbarSelect = "Toolbar.Select";
    public const string ToolbarDelete = "Toolbar.Delete";
    public const string ToolbarFitAll = "Toolbar.FitAll";
    public const string ToolbarZoomIn = "Toolbar.ZoomIn";
    public const string ToolbarZoomOut = "Toolbar.ZoomOut";
    public const string ToolbarAnalyze = "Toolbar.Analyze";
    public const string ToolbarRepair = "Toolbar.Repair";
    public const string TooltipOpen = "Tooltip.Open";
    public const string TooltipExportClean = "Tooltip.ExportClean";
    public const string TooltipRepairAll = "Tooltip.RepairAll";
    public const string TooltipSelect = "Tooltip.Select";
    public const string TooltipNodeEdit = "Tooltip.NodeEdit";
    public const string TooltipJoin = "Tooltip.Join";
    public const string TooltipBreak = "Tooltip.Break";
    public const string TooltipTrim = "Tooltip.Trim";
    public const string TooltipExtend = "Tooltip.Extend";

    // ---- editing tools (D17) ----
    public const string ToolNameSelect = "Tool.Name.Select";
    public const string ToolNameNodeEdit = "Tool.Name.NodeEdit";
    public const string ToolNameJoin = "Tool.Name.Join";
    public const string ToolNameBreak = "Tool.Name.Break";
    public const string ToolNameTrim = "Tool.Name.Trim";
    public const string ToolNameExtend = "Tool.Name.Extend";
    public const string StatusAnalysisStale = "Status.AnalysisStale";
    public const string EditToolsJoinNoEndpoint = "EditTools.Join.NoEndpoint";
    public const string EditToolsJoinPickSecond = "EditTools.Join.PickSecond";
    public const string EditToolsJoinSameEndpoint = "EditTools.Join.SameEndpoint";
    public const string EditToolsJoinNotConnected = "EditTools.Join.NotConnected";
    public const string EditToolsJoinAmbiguous = "EditTools.Join.Ambiguous";
    public const string EditToolsJoinDifferentLayers = "EditTools.Join.DifferentLayers";
    public const string EditToolsJoinUnsupported = "EditTools.Join.Unsupported";
    public const string EditToolsBreakNoTarget = "EditTools.Break.NoTarget";
    public const string EditToolsBreakNotOnTarget = "EditTools.Break.NotOnTarget";
    public const string EditToolsBreakEndpointGuard = "EditTools.Break.EndpointGuard";
    public const string EditToolsTrimNoTarget = "EditTools.Trim.NoTarget";
    public const string EditToolsTrimUnsupportedTarget = "EditTools.Trim.UnsupportedTarget";
    public const string EditToolsTrimPickSection = "EditTools.Trim.PickSection";
    public const string EditToolsTrimNotOnTarget = "EditTools.Trim.NotOnTarget";
    public const string EditToolsTrimNoBoundary = "EditTools.Trim.NoBoundary";
    public const string EditToolsTrimInvalidSection = "EditTools.Trim.InvalidSection";
    public const string EditToolsTrimTinyKeptPiece = "EditTools.Trim.TinyKeptPiece";
    public const string EditToolsExtendNoTarget = "EditTools.Extend.NoTarget";
    public const string EditToolsExtendPickEnd = "EditTools.Extend.PickEnd";
    public const string EditToolsExtendNoBoundary = "EditTools.Extend.NoBoundary";

    // ---- panels ----
    public const string PanelLayers = "Panel.Layers";
    public const string PanelContours = "Panel.Contours";
    public const string PanelProperties = "Panel.Properties";
    public const string PanelDiagnostics = "Panel.Diagnostics";
    public const string PanelImport = "Panel.Import";

    // ---- layers ----
    public const string LayerShowAll = "Layer.ShowAll";
    public const string LayerHideAll = "Layer.HideAll";
    public const string LayerColVisible = "Layer.Col.Visible";
    public const string LayerColLayer = "Layer.Col.Layer";
    public const string LayerColCount = "Layer.Col.Count";

    // ---- contours / diagnostics ----
    public const string ContoursEmpty = "Contours.Empty";
    public const string ContoursAction = "Contours.Action";
    public const string ContoursTotal = "Contours.Total";
    public const string ContoursClosed = "Contours.Closed";
    public const string ContoursOpen = "Contours.Open";
    public const string ContoursOuter = "Contours.Outer";
    public const string ContoursHole = "Contours.Hole";
    public const string ContoursIsland = "Contours.Island";
    public const string ContoursGapCount = "Contours.GapCount";
    public const string ContoursAnomaly = "Contours.Anomaly";
    public const string ContoursColId = "Contours.Col.Id";
    public const string ContoursColSummary = "Contours.Col.Summary";
    public const string ContoursRowSummary = "Contours.Row.Summary";
    public const string ContoursRowOpen = "Contours.Row.Open";
    public const string ContoursRepair = "Contours.Repair";
    public const string ContoursNextAnomaly = "Contours.NextAnomaly";
    public const string ContoursPrevAnomaly = "Contours.PrevAnomaly";
    public const string ContoursFitToSelection = "Contours.FitToSelection";
    public const string ContoursWarningOpen = "Contours.Warning.Open";
    public const string DiagEmpty = "Diag.Empty";
    public const string DiagSmallGap = "Diag.SmallGap";
    public const string DiagOpenEnd = "Diag.OpenEnd";
    public const string DiagBranch = "Diag.Branch";
    public const string DiagZeroLength = "Diag.ZeroLength";
    public const string DiagVerySmall = "Diag.VerySmall";
    public const string DiagDuplicate = "Diag.Duplicate";
    public const string DiagSelfIntersection = "Diag.SelfIntersection";
    public const string DiagInvalidGeometry = "Diag.InvalidGeometry";
    public const string DiagSeverityInfo = "Diag.Severity.Info";
    public const string DiagSeverityWarning = "Diag.Severity.Warning";
    public const string DiagSeverityError = "Diag.Severity.Error";
    public const string DiagColSeverity = "Diag.Col.Severity";
    public const string DiagColType = "Diag.Col.Type";
    public const string DiagColPosition = "Diag.Col.Position";
    public const string DiagColDistance = "Diag.Col.Distance";
    public const string DiagColEntity = "Diag.Col.Entity";
    public const string DiagDetailGap = "Diag.Detail.Gap";
    public const string DiagDetailOpenEnd = "Diag.Detail.OpenEnd";
    public const string DiagDetailNoMatch = "Diag.Detail.NoMatch";
    public const string DiagDetailBranch = "Diag.Detail.Branch";
    public const string DiagDetailZeroLength = "Diag.Detail.ZeroLength";
    public const string DiagDetailVerySmall = "Diag.Detail.VerySmall";
    public const string DiagDetailDuplicate = "Diag.Detail.Duplicate";
    public const string DiagDetailSelfIntersection = "Diag.Detail.SelfIntersection";
    public const string DiagCollapse = "Diag.Collapse";
    public const string DiagCollapseTip = "Diag.CollapseTip";
    public const string ViewportEmpty = "Viewport.Empty";

    // ---- about dialog ----
    public const string AboutTitle = "About.Title";
    public const string AboutVersion = "About.Version";
    public const string AboutTechnology = "About.Technology";
    public const string AboutDescription = "About.Description";
    public const string AboutLicense = "About.License";
    public const string AboutGitHub = "About.GitHub";
    public const string AboutGitHubNotPublished = "About.GitHub.NotPublished";
    public const string AboutCopyright = "About.Copyright";
    public const string AboutClose = "About.Close";
    public const string AboutTechStack = "About.TechStack";
    public const string AboutLicenseName = "About.LicenseName";

    // ---- properties ----
    public const string PropsEmptyTitle = "Props.Empty.Title";
    public const string PropsEmptyHint = "Props.Empty.Hint";
    public const string PropsGroupBasic = "Props.Group.Basic";
    public const string PropsGroupGeometry = "Props.Group.Geometry";
    public const string PropsGroupBounds = "Props.Group.Bounds";
    public const string PropsGroupDocument = "Props.Group.Document";
    public const string PropsGroupMulti = "Props.Group.Multi";

    // ---- single property // row name keys ----
    public const string PropertyId = "Property.Id";
    public const string PropertyType = "Property.Type";
    public const string PropertyLayer = "Property.Layer";
    public const string PropertyVisible = "Property.Visible";
    public const string PropertyLength = "Property.Length";
    public const string PropertyCenterX = "Property.CenterX";
    public const string PropertyCenterY = "Property.CenterY";
    public const string PropertyRadius = "Property.Radius";
    public const string PropertyDiameter = "Property.Diameter";
    public const string PropertyCircumference = "Property.Circumference";
    public const string PropertyStartX = "Property.StartX";
    public const string PropertyStartY = "Property.StartY";
    public const string PropertyEndX = "Property.EndX";
    public const string PropertyEndY = "Property.EndY";
    public const string PropertyStartAngle = "Property.StartAngle";
    public const string PropertyEndAngle = "Property.EndAngle";
    public const string PropertySweepAngle = "Property.SweepAngle";
    public const string PropertyArcLength = "Property.ArcLength";
    public const string PropertyClosed = "Property.Closed";
    public const string PropertySegments = "Property.Segments";
    public const string PropertyCount = "Property.Count";
    public const string PropertyTotalLength = "Property.TotalLength";
    public const string PropertyTypes = "Property.Types";
    public const string PropertyLayerList = "Property.LayerList";
    public const string PropertyBounds = "Doc.Bounds";
    public const string PropertySize = "Doc.Size";

    // ---- type display names ----
    public const string TypeLine = "Type.Line";
    public const string TypeCircle = "Type.Circle";
    public const string TypeArc = "Type.Arc";
    public const string TypePolyline = "Type.Polyline";

    // ---- entity statistics display names ----
    public const string EntityLine = "Entity.Line";
    public const string EntityArc = "Entity.Arc";
    public const string EntityCircle = "Entity.Circle";
    public const string EntityLwPolyline = "Entity.LwPolyline";
    public const string EntityPolyline = "Entity.Polyline";
    public const string EntitySpline = "Entity.Spline";
    public const string EntityEllipse = "Entity.Ellipse";
    public const string EntityInsert = "Entity.Insert";
    public const string EntityTextLike = "Entity.TextLike";
    public const string EntityOther = "Entity.Other";

    // ---- units ----
    public const string UnitMillimeter = "Unit.Millimeter";
    public const string UnitInch = "Unit.Inch";
    public const string UnitUnitless = "Unit.Unitless";
    public const string UnitUnknown = "Unit.Unknown";
    public const string UnitCentimeter = "Unit.Centimeter";
    public const string UnitMeter = "Unit.Meter";
    public const string UnitFoot = "Unit.Foot";
    public const string UnitMmAbbr = "Unit.MmAbbr";

    // ---- status bar + status messages ----
    public const string StatusX = "Status.X";
    public const string StatusY = "Status.Y";
    public const string StatusZoom = "Status.Zoom";
    public const string StatusUnit = "Status.Unit";
    public const string StatusEntities = "Status.Entities";
    public const string StatusSelected = "Status.Selected";
    public const string StatusReady = "Status.Ready";
    public const string StatusOpened = "Status.Opened";
    public const string StatusSummary = "Status.Summary";
    public const string StatusImportFailed = "Status.ImportFailed";
    public const string StatusNothingToFit = "Status.NothingToFit";
    public const string StatusUndoMove = "Status.UndoMove";
    public const string StatusRedoMove = "Status.RedoMove";
    public const string StatusUndoGeneric = "Status.UndoGeneric";
    public const string StatusRedoGeneric = "Status.RedoGeneric";
    public const string StatusDeleted = "Status.Deleted";
    public const string StatusMoved = "Status.Moved";
    public const string StatusCleared = "Status.Cleared";
    public const string StatusLayerShown = "Status.LayerShown";
    public const string StatusLayerHidden = "Status.LayerHidden";
    public const string StatusAllShown = "Status.AllShown";
    public const string StatusAllHidden = "Status.AllHidden";
    public const string StatusFit = "Status.Fit";
    public const string StatusSelectedCount = "Status.SelectedCount";
    public const string StatusAnalyzed = "Status.Analyzed";
    public const string StatusAnalyzeFailed = "Status.AnalyzeFailed";
    public const string StatusGapRepaired = "Status.GapRepaired";
    public const string StatusGapRepairUndone = "Status.GapRepairUndone";
    public const string StatusNoContours = "Status.NoContours";
    public const string StatusAnomalyLocated = "Status.AnomalyLocated";
    public const string StatusBatchRepaired = "Status.BatchRepaired";
    public const string StatusProjectSaved = "Status.ProjectSaved";
    public const string StatusProjectLoaded = "Status.ProjectLoaded";
    public const string StatusProjectSaveFailed = "Status.ProjectSaveFailed";
    public const string StatusProjectLoadFailed = "Status.ProjectLoadFailed";
    public const string StatusExportDone = "Status.ExportDone";
    public const string StatusExportFailed = "Status.ExportFailed";
    public const string StatusNothingToSave = "Status.NothingToSave";
    public const string StatusNothingToExport = "Status.NothingToExport";
    public const string UnsavedPromptTitle = "Unsaved.Title";
    public const string UnsavedPromptMessage = "Unsaved.Message";
    public const string UnsavedPromptSave = "Unsaved.Save";
    public const string UnsavedPromptDiscard = "Unsaved.Discard";
    public const string UnsavedPromptCancel = "Unsaved.Cancel";
    public const string DialogProjectFilter = "Dialog.ProjectFilter";
    public const string DialogExportFilter = "Dialog.ExportFilter";

    // ---- import report ----
    public const string ReportFile = "Report.File";
    public const string ReportVersion = "Report.Version";
    public const string ReportDeclaredUnit = "Report.DeclaredUnit";
    public const string ReportInterpretedUnit = "Report.InterpretedUnit";
    public const string ReportLayerCount = "Report.LayerCount";
    public const string ReportTotalEntities = "Report.TotalEntities";
    public const string ReportImported = "Report.Imported";
    public const string ReportIgnored = "Report.Ignored";
    public const string ReportUnsupported = "Report.Unsupported";
    public const string ReportWarnings = "Report.Warnings";
    public const string ReportErrors = "Report.Errors";
    public const string ReportElapsed = "Report.Elapsed";
    public const string ReportEmpty = "Report.Empty";
    public const string ReportStatisticsTitle = "Report.StatisticsTitle";

    // ---- shared ----
    public const string CommandLater = "Command.Later";
    public const string CommonYes = "Common.Yes";
    public const string CommonNo = "Common.No";
    public const string DialogDxfFilter = "Dialog.DxfFilter";

    // ---- property row groups (structural, not localized) ----
    public const string GroupBasic = "basic";
    public const string GroupGeometry = "geometry";
    public const string GroupBounds = "bounds";
    public const string GroupDocument = "document";
    public const string GroupMulti = "multi";
}