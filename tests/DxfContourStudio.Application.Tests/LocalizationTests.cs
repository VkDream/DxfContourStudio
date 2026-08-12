#nullable enable

using System.Reflection;
using DxfContourStudio.Application.Localization;

namespace DxfContourStudio.Application.Tests;

/// <summary>
/// Localization architecture tests:
/// - the zh-CN resource defines every key the code references (via
///   LocalizationKeys constants — one source of truth),
/// - en-US is aligned with zh-CN (same key set, no empty values),
/// - important UI keys (menu, properties, status, report) exist,
/// - formatted messages and the deterministic fallback behave as specified.
/// These tests keep the localization base honest without a UI.
/// </summary>
[Collection("LocalizationShared")]
public class LocalizationTests
{
    private static readonly string[] AllCodeKeys = typeof(LocalizationKeys)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        // Group* constants are structural group identifiers, not dictionary
        // keys; they are deliberately excluded from the key-set check.
        .Where(k => k is not (
            LocalizationKeys.GroupBasic or
            LocalizationKeys.GroupGeometry or
            LocalizationKeys.GroupBounds or
            LocalizationKeys.GroupDocument or
            LocalizationKeys.GroupMulti))
        .ToArray();

    [Fact]
    public void ZhCn_DefinesEveryReferencedKey()
    {
        var missing = AllCodeKeys.Where(k => !LocalizedStringsZhCn.All.ContainsKey(k)).ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void EnUs_KeySet_MatchesZhCn_Exactly()
    {
        var zhKeys = LocalizedStringsZhCn.All.Keys.OrderBy(k => k).ToList();
        var enKeys = LocalizedStringsEn.All.Keys.OrderBy(k => k).ToList();

        Assert.Equal(zhKeys, enKeys);
    }

    [Fact]
    public void NoEmptyTranslations_InEitherLanguage()
    {
        foreach (var (key, value) in LocalizedStringsZhCn.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(value), $"zh-CN empty for {key}");
        }

        foreach (var (key, value) in LocalizedStringsEn.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(value), $"en-US empty for {key}");
        }
    }

    [Fact]
    public void ImportantMenuAndPropertyKeys_Exist()
    {
        string[] required =
        [
            LocalizationKeys.MenuFile, LocalizationKeys.MenuEdit, LocalizationKeys.MenuView,
            LocalizationKeys.MenuAnalyze, LocalizationKeys.MenuTools, LocalizationKeys.MenuHelp,
            LocalizationKeys.MenuFileOpen, LocalizationKeys.MenuFileSave, LocalizationKeys.MenuFileExit,
            LocalizationKeys.MenuEditUndo, LocalizationKeys.MenuEditRedo, LocalizationKeys.MenuEditDelete,
            LocalizationKeys.MenuEditSelectAll, LocalizationKeys.MenuEditClearSelection,
            LocalizationKeys.MenuViewFitAll, LocalizationKeys.MenuViewZoomIn, LocalizationKeys.MenuViewZoomOut,
            LocalizationKeys.MenuViewShowAllLayers, LocalizationKeys.MenuViewHideAllLayers,
            LocalizationKeys.MenuAnalyzeContours, LocalizationKeys.MenuAnalyzeDiagnostics,
            LocalizationKeys.MenuToolsSettings, LocalizationKeys.MenuToolsLanguage,
            LocalizationKeys.MenuHelpAbout,
            LocalizationKeys.PanelLayers, LocalizationKeys.PanelContours,
            LocalizationKeys.PanelProperties, LocalizationKeys.PanelDiagnostics, LocalizationKeys.PanelImport,
            LocalizationKeys.ContoursEmpty, LocalizationKeys.DiagEmpty,
            LocalizationKeys.PropsEmptyTitle, LocalizationKeys.PropsEmptyHint,
            LocalizationKeys.PropertyType, LocalizationKeys.PropertyLayer, LocalizationKeys.PropertyLength,
            LocalizationKeys.PropertyRadius, LocalizationKeys.PropertyCenterX, LocalizationKeys.PropertyStartX,
            LocalizationKeys.PropertyTotalLength, LocalizationKeys.PropertyCount,
            LocalizationKeys.StatusMoved, LocalizationKeys.StatusOpened, LocalizationKeys.StatusSelectedCount,
            LocalizationKeys.ReportFile, LocalizationKeys.ReportTotalEntities,
        ];

        Assert.All(required, key => Assert.True(
            LocalizedStringsZhCn.All.ContainsKey(key) && LocalizedStringsEn.All.ContainsKey(key),
            $"missing key: {key}"));
    }

    [Fact]
    public void ChineseEntityNames_UseDxfNameSuffix_Format()
    {
        Assert.Equal("直线（LINE）", LocalizationService.Instance.Get(LocalizationKeys.TypeLine));
        Assert.Equal("圆（CIRCLE）", LocalizationService.Instance.Get(LocalizationKeys.TypeCircle));
        Assert.Equal("圆弧（ARC）", LocalizationService.Instance.Get(LocalizationKeys.TypeArc));
        Assert.Equal("轻量多段线（LWPOLYLINE）", LocalizationService.Instance.Get(LocalizationKeys.TypePolyline));
    }

    [Fact]
    public void StatusMessages_FormatWithArguments()
    {
        var loc = LocalizationService.Instance;

        Assert.Equal("已移动 1 个图元", loc.Get(LocalizationKeys.StatusMoved, 1));
        Assert.Equal("已选择 3 个图元", loc.Get(LocalizationKeys.StatusSelectedCount, 3));
        Assert.Equal("已隐藏图层 \"CUT\"", loc.Get(LocalizationKeys.StatusLayerHidden, "CUT"));
        Assert.Contains("basic-scene.dxf", loc.Get(LocalizationKeys.StatusOpened, "C:\\drawing\\basic-scene.dxf"));
    }

    [Fact]
    public void UnknownKey_FallsBackToKeyItself_Deterministically()
    {
        var loc = LocalizationService.Instance;

        Assert.Equal("Definitely.Not.A.Key", loc.Get("Definitely.Not.A.Key"));
        Assert.False(loc.HasKey("Definitely.Not.A.Key"));
        Assert.True(loc.HasKey(LocalizationKeys.StatusReady));
    }

    [Fact]
    public void SetCulture_SwitchesLookup_AndRaisesChange()
    {
        var loc = LocalizationService.Instance;
        int changes = 0;
        loc.CultureChanged += () => changes++;

        string zh = loc.Get(LocalizationKeys.StatusReady);
        loc.SetCulture(LocalizationService.EnUsName);
        string en = loc.Get(LocalizationKeys.StatusReady);
        loc.SetCulture(LocalizationService.EnUsName); // no-op, must not re-raise
        loc.SetCulture(LocalizationService.ZhCnName);

        Assert.Equal(2, changes);
        Assert.Equal("Ready. Open a DXF file.", en);
        Assert.NotEqual(zh, en);
        Assert.Equal("就绪，请打开 DXF 文件", zh);
    }

    [Fact]
    public void EnUs_ProvidesAlignedSkeleton_ForKnownCulture()
    {
        var loc = LocalizationService.Instance;
        loc.SetCulture(LocalizationService.EnUsName);

        Assert.True(LocalizationService.IsKnownCulture(LocalizationService.EnUsName));
        Assert.Equal(LocalizationService.EnUsName, loc.CurrentCulture);
        Assert.Equal("Open DXF file (Ctrl+O)", loc.Get(LocalizationKeys.TooltipOpen));

        loc.SetCulture(LocalizationService.ZhCnName); // restore for other tests
    }
}
