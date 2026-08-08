#nullable enable

using System.Collections.Generic;

namespace DxfContourStudio.Application.Localization;

/// <summary>
/// zh-CN (Simplified Chinese) — the default, complete product resource.
/// Every key the UI needs must exist here. Keep this table authoritative:
/// en-US mirrors its key set (checked by tests) and en-US may add wording.
///
/// Naming convention: "Area.Subject" dots, e.g. "Menu.File.Open",
/// "Panel.Properties", "Status.Moved", "Property.Length".
/// </summary>
public static class LocalizedStringsZhCn
{
    public static IReadOnlyDictionary<string, string> All { get; } = Build();

    private static IReadOnlyDictionary<string, string> Build()
    {
        var d = new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            // ---- application ----
            ["App.Name"] = "DxfContourStudio 轮廓工作台",

            // ---- menu: file ----
            ["Menu.File"] = "文件",
            ["Menu.File.Open"] = "打开 DXF...",
            ["Menu.File.OpenProject"] = "打开工程...",
            ["Menu.File.Save"] = "保存工程",
            ["Menu.File.SaveAs"] = "另存为...",
            ["Menu.File.Export"] = "导出",
            ["Menu.File.ExportClean"] = "导出清理后的 DXF...",
            ["Menu.File.Exit"] = "退出",

            // ---- menu: edit ----
            ["Menu.Edit"] = "编辑",
            ["Menu.Edit.Undo"] = "撤销",
            ["Menu.Edit.Redo"] = "重做",
            ["Menu.Edit.Delete"] = "删除",
            ["Menu.Edit.SelectAll"] = "全选",
            ["Menu.Edit.ClearSelection"] = "取消选择",

            // ---- menu: view ----
            ["Menu.View"] = "视图",
            ["Menu.View.FitAll"] = "适合窗口",
            ["Menu.View.ZoomIn"] = "放大",
            ["Menu.View.ZoomOut"] = "缩小",
            ["Menu.View.ShowAllLayers"] = "显示全部图层",
            ["Menu.View.HideAllLayers"] = "隐藏全部图层",

            // ---- menu: analyze ----
            ["Menu.Analyze"] = "分析",
            ["Menu.Analyze.Contours"] = "分析轮廓",
            ["Menu.Analyze.Diagnostics"] = "轮廓诊断",

            // ---- menu: tools ----
            ["Menu.Tools"] = "工具",
            ["Menu.Tools.Settings"] = "设置",
            ["Menu.Tools.Language"] = "语言",
            ["Menu.Tools.Language.ZhCn"] = "简体中文",
            ["Menu.Tools.Language.Us"] = "English",

            // ---- menu: help ----
            ["Menu.Help"] = "帮助",
            ["Menu.Help.About"] = "关于 DxfContourStudio",
            ["About.Text"] = "DxfContourStudio — CAD 轮廓基础工具。\n\n.NET 10 / WPF / ACadSharp",

            // ---- toolbar ----
            ["Toolbar.Open"] = "打开",
            ["Toolbar.OpenProject"] = "打开工程",
            ["Toolbar.Save"] = "保存",
            ["Toolbar.SaveProject"] = "保存工程",
            ["Toolbar.ExportClean"] = "导出清理 DXF",
            ["Toolbar.RepairAll"] = "全部安全修复",
            ["Toolbar.Undo"] = "撤销",
            ["Toolbar.Redo"] = "重做",
            ["Toolbar.Select"] = "选择",
            ["Toolbar.Delete"] = "删除",
            ["Toolbar.FitAll"] = "适合窗口",
            ["Toolbar.ZoomIn"] = "放大",
            ["Toolbar.ZoomOut"] = "缩小",
            ["Toolbar.Analyze"] = "分析轮廓",
            ["Toolbar.Repair"] = "修复间隙",

            // ---- toolbar tooltips ----
            ["Tooltip.Open"] = "打开 DXF 文件（Ctrl+O）",
            ["Tooltip.Save"] = "保存工程",
            ["Tooltip.Undo"] = "撤销（Ctrl+Z）",
            ["Tooltip.Redo"] = "重做（Ctrl+Y）",
            ["Tooltip.Delete"] = "删除选中图元（Del）",
            ["Tooltip.Select"] = "选择 / 移动图元",
            ["Tooltip.FitAll"] = "缩放至适合窗口（F）",
            ["Tooltip.ZoomIn"] = "放大视图",
            ["Tooltip.ZoomOut"] = "缩小视图",
            ["Tooltip.Analyze"] = "分析轮廓：拓扑构链、间隙诊断、孔洞嵌套",
            ["Tooltip.Repair"] = "修复选中的小间隙（可撤销）",
            ["Tooltip.ExportClean"] = "导出清理后的 DXF（不覆盖源文件）",
            ["Tooltip.RepairAll"] = "全部安全修复（一次撤销可全部恢复）",

            // ---- panels ----
            ["Panel.Layers"] = "图层",
            ["Panel.Contours"] = "轮廓",
            ["Panel.Properties"] = "属性",
            ["Panel.Diagnostics"] = "诊断",
            ["Panel.Import"] = "导入报告",

            // ---- layers panel ----
            ["Layer.ShowAll"] = "全部显示",
            ["Layer.HideAll"] = "全部隐藏",
            ["Layer.ShowAllTip"] = "显示所有图层",
            ["Layer.HideAllTip"] = "隐藏所有图层",
            ["Layer.Col.Visible"] = "可见",
            ["Layer.Col.Layer"] = "图层",
            ["Layer.Col.Count"] = "数量",

            // ---- contours panel ----
            ["Contours.Empty"] = "尚未执行轮廓分析",
            ["Contours.Action"] = "分析轮廓",
            ["Contours.Total"] = "轮廓总数",
            ["Contours.Closed"] = "闭合",
            ["Contours.Open"] = "开口",
            ["Contours.Outer"] = "外轮廓",
            ["Contours.Hole"] = "孔",
            ["Contours.Island"] = "岛",
            ["Contours.GapCount"] = "小间隙",
            ["Contours.Anomaly"] = "异常",
            ["Contours.Col.Id"] = "编号",
            ["Contours.Col.Summary"] = "摘要",
            ["Contours.Row.Summary"] = "{0} · 长度 {1}",
            ["Contours.Row.Open"] = "开口 · 间隙 {0}",
            ["Contours.Repair"] = "修复间隙",
            ["Contours.NextAnomaly"] = "下一异常",
            ["Contours.PrevAnomaly"] = "上一异常",
            ["Contours.FitToSelection"] = "适合所选轮廓",
            ["Contours.Warning.Open"] = "轮廓未闭合",

            // ---- diagnostics panel ----
            ["Diag.Empty"] = "当前没有诊断项",
            ["Diag.SmallGap"] = "小间隙",
            ["Diag.OpenEnd"] = "开放端点",
            ["Diag.Branch"] = "分支节点",
            ["Diag.ZeroLength"] = "零长度图元",
            ["Diag.VerySmall"] = "过小图元",
            ["Diag.Duplicate"] = "重复图元",
            ["Diag.SelfIntersection"] = "自相交",
            ["Diag.InvalidGeometry"] = "无效几何（NaN/Infinity）",
            ["Diag.Severity.Info"] = "信息",
            ["Diag.Severity.Warning"] = "警告",
            ["Diag.Severity.Error"] = "错误",
            ["Diag.Col.Severity"] = "级别",
            ["Diag.Col.Type"] = "类型",
            ["Diag.Col.Position"] = "位置",
            ["Diag.Col.Distance"] = "距离",
            ["Diag.Col.Entity"] = "图元",
            ["Diag.Detail.Gap"] = "距离 {0} — 可修复",
            ["Diag.Detail.OpenEnd"] = "距离 {0} — 超出修复范围",
            ["Diag.Detail.NoMatch"] = "无可匹配端点",
            ["Diag.Detail.Branch"] = "分支交汇点（连接 {0} 条边）",
            ["Diag.Detail.ZeroLength"] = "长度 {0} — 低于零长度容差",
            ["Diag.Detail.VerySmall"] = "长度 {0} — 过小（警告）",
            ["Diag.Detail.Duplicate"] = "#{0} 与 #{1} 重复",
            ["Diag.Detail.SelfIntersection"] = "#{0} 与 #{1} 在交叉点相交",
            ["Diag.Collapse"] = "收起",
            ["Diag.CollapseTip"] = "折叠或展开底部诊断面板",

            ["Viewport.Empty"] = "点击“打开 DXF”开始。",

            // ---- about dialog ----
            ["About.Title"] = "关于 DxfContourStudio",
            ["About.Version"] = "版本",
            ["About.Technology"] = "技术",
            ["About.Description"] = "DXF 轮廓分析工作台：拓扑构链、轮廓闭合、间隙诊断与修复。",
            ["About.License"] = "开源许可",
            ["About.GitHub"] = "GitHub",
            ["About.GitHub.NotPublished"] = "仓库尚未发布",
            ["About.Copyright"] = "Copyright © 2026 DxfContourStudio 项目",
            ["About.Close"] = "关闭",
            ["About.TechStack"] = ".NET 10 / WPF / ACadSharp",
            ["About.LicenseName"] = "MIT",

            // ---- properties panel ----
            ["Props.Empty.Title"] = "未选择图元",
            ["Props.Empty.Hint"] = "在 CAD 视图中选择图元以查看属性。",
            ["Props.Group.Basic"] = "基本信息",
            ["Props.Group.Geometry"] = "几何",
            ["Props.Group.Bounds"] = "边界",
            ["Props.Group.Document"] = "文档",
            ["Props.Group.Multi"] = "汇总",

            // ---- single property names ----
            ["Property.Id"] = "编号",
            ["Property.Type"] = "类型",
            ["Property.Layer"] = "图层",
            ["Property.Visible"] = "可见",
            ["Property.Length"] = "长度",
            ["Property.CenterX"] = "圆心 X",
            ["Property.CenterY"] = "圆心 Y",
            ["Property.Radius"] = "半径",
            ["Property.Diameter"] = "直径",
            ["Property.Circumference"] = "周长",
            ["Property.StartX"] = "起点 X",
            ["Property.StartY"] = "起点 Y",
            ["Property.EndX"] = "终点 X",
            ["Property.EndY"] = "终点 Y",
            ["Property.StartAngle"] = "起始角度",
            ["Property.EndAngle"] = "终止角度",
            ["Property.SweepAngle"] = "圆弧角",
            ["Property.ArcLength"] = "弧长",
            ["Property.Closed"] = "闭合",
            ["Property.Segments"] = "线段数",
            ["Property.Yes"] = "是",
            ["Property.No"] = "否",

            // ---- multi-selection summary ----
            ["Property.Count"] = "数量",
            ["Property.TotalLength"] = "总长度",
            ["Property.Types"] = "类型",
            ["Property.LayerList"] = "图层",

            // ---- document summary ----
            ["Doc.Entities"] = "图元",
            ["Doc.Layers"] = "图层",
            ["Doc.Bounds"] = "包围框",
            ["Doc.Size"] = "大小",

            // ---- geometry / entity type display names ----
            ["Type.Line"] = "直线（LINE）",
            ["Type.Circle"] = "圆（CIRCLE）",
            ["Type.Arc"] = "圆弧（ARC）",
            ["Type.Polyline"] = "轻量多段线（LWPOLYLINE）",

            // ---- import report entity kinds ----
            ["Entity.Line"] = "直线（LINE）",
            ["Entity.Arc"] = "圆弧（ARC）",
            ["Entity.Circle"] = "圆（CIRCLE）",
            ["Entity.LwPolyline"] = "轻量多段线（LWPOLYLINE）",
            ["Entity.Polyline"] = "多段线（POLYLINE）",
            ["Entity.Spline"] = "样条曲线（SPLINE）",
            ["Entity.Ellipse"] = "椭圆（ELLIPSE）",
            ["Entity.Insert"] = "块参照（INSERT）",
            ["Entity.TextLike"] = "文字（TEXT/MTEXT）",
            ["Entity.Other"] = "其他",

            // ---- units ----
            ["Unit.Millimeter"] = "毫米",
            ["Unit.Inch"] = "英寸",
            ["Unit.Unitless"] = "无单位",
            ["Unit.Unknown"] = "未知",
            ["Unit.Centimeter"] = "厘米",
            ["Unit.Meter"] = "米",
            ["Unit.Foot"] = "英尺",
            ["Unit.MmAbbr"] = "mm",

            // ---- status bar ----
            ["Status.X"] = "X",
            ["Status.Y"] = "Y",
            ["Status.Zoom"] = "缩放",
            ["Status.Unit"] = "单位",
            ["Status.Entities"] = "图元",
            ["Status.Selected"] = "已选",

            // ---- status messages ----
            ["Status.Ready"] = "就绪，请打开 DXF 文件",
            ["Status.Opened"] = "DXF 文件已加载：{0}",
            ["Status.Summary"] = "图元 {0}，图层 {1}",
            ["Status.ImportFailed"] = "导入失败：{0}",
            ["Status.NothingToFit"] = "没有可适配的图元",
            ["Status.UndoMove"] = "已撤销移动",
            ["Status.RedoMove"] = "已重做移动",
            ["Status.UndoGeneric"] = "已撤销操作",
            ["Status.RedoGeneric"] = "已重做操作",
            ["Status.Deleted"] = "已删除 {0} 个图元",
            ["Status.Moved"] = "已移动 {0} 个图元",
            ["Status.Cleared"] = "已取消选择",
            ["Status.LayerShown"] = "已显示图层 {0}",
            ["Status.LayerHidden"] = "已隐藏图层 \"{0}\"",
            ["Status.AllShown"] = "已显示全部图层",
            ["Status.AllHidden"] = "已隐藏全部图层",
            ["Status.Fit"] = "已缩放至适合窗口",
            ["Status.SelectedCount"] = "已选择 {0} 个图元",
            ["Status.Analyzed"] = "分析完成：{0} 个轮廓，闭合 {1}，开口 {2}",
            ["Status.AnalyzeFailed"] = "轮廓分析失败，请查看诊断日志",
            ["Status.GapRepaired"] = "已修复 {0} 处小间隙",
            ["Status.GapRepairUndone"] = "已撤销间隙修复",
            ["Status.NoContours"] = "未找到闭合轮廓",
            ["Status.AnomalyLocated"] = "已定位异常：{0}",
            ["Status.BatchRepaired"] = "已批量修复 {0} 处间隙",
            ["Status.ProjectSaved"] = "工程已保存：{0}",
            ["Status.ProjectLoaded"] = "工程已加载：{0}",
            ["Status.ProjectSaveFailed"] = "工程保存失败：{0}",
            ["Status.ProjectLoadFailed"] = "工程加载失败：{0}",
            ["Status.ExportDone"] = "已导出：{0}（{1} 个图元）",
            ["Status.ExportFailed"] = "导出失败：{0}",
            ["Status.NothingToSave"] = "当前没有可保存的图元",
            ["Status.NothingToExport"] = "当前没有可导出的图元",
            ["Unsaved.Title"] = "未保存的更改",
            ["Unsaved.Message"] = "当前文档有未保存的更改。{0} 前要保存吗？",
            ["Unsaved.Save"] = "保存",
            ["Unsaved.Discard"] = "不保存",
            ["Unsaved.Cancel"] = "取消",
            ["Dialog.ProjectFilter"] = "DxfContourStudio 工程 (*.dxfstudio)|*.dxfstudio|所有文件 (*.*)|*.*",
            ["Dialog.ExportFilter"] = "DXF 文件 (*.dxf)|*.dxf|所有文件 (*.*)|*.*",

            // ---- import report rows ----
            ["Report.File"] = "文件",
            ["Report.Version"] = "DXF 版本",
            ["Report.DeclaredUnit"] = "声明单位",
            ["Report.InterpretedUnit"] = "解释单位",
            ["Report.LayerCount"] = "图层数量",
            ["Report.TotalEntities"] = "图元总数",
            ["Report.Imported"] = "成功导入",
            ["Report.Ignored"] = "忽略",
            ["Report.Unsupported"] = "不支持",
            ["Report.Warnings"] = "警告",
            ["Report.Errors"] = "错误",
            ["Report.Elapsed"] = "耗时",
            ["Report.Empty"] = "尚未打开 DXF 文件",
            ["Report.StatisticsTitle"] = "图元统计",

            // ---- shared ----
            ["Command.Later"] = "后续版本提供",
            ["Common.Yes"] = "是",
            ["Common.No"] = "否",
            ["Dialog.DxfFilter"] = "DXF 文件 (*.dxf)|*.dxf|所有文件 (*.*)|*.*",
        };

        return d;
    }
}