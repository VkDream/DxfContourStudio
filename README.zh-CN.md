# DxfContourStudio

面向生产前几何处理的工业级 DXF 轮廓分析、诊断与修复工作台：C# / .NET 10 / WPF。

打开 DXF、检查与分析轮廓、修复小间隙、保存工程、导出清理后的 DXF——全部在
双语（简体中文 / English）桌面界面中完成。

> **状态：** v0.2.0 — 首次公开功能版本。

## 概述

DxfContourStudio 将原始 DXF 几何转换为拓扑图，识别开口与闭合轮廓，分类嵌套
（外轮廓 / 孔 / 岛），发现几何缺陷（间隙、开放端点、分支、重复、零长度、自相交），
并支持对安全缺陷的修复（完整撤销）。它是生产前几何处理工作台——**不是**完整
CAD/CAM 系统。

## 功能

- **DXF 导入**（ACadSharp 3.6.51, MIT）：LINE、ARC、CIRCLE、LWPOLYLINE（含
  bulge）、POLYLINE/Vertex2D；椭圆/样条/块/文字计入报告但不崩溃；坐标统一为毫米。
- **CAD 视口**：平移/缩放、点击选中、高亮、图层显隐、拖拽移动（可撤销）。
- **拓扑与轮廓**：拓扑图 → 链 → 轮廓（长度/包围框/有向面积/方向）→ 嵌套
  （外轮廓/孔/岛 + 深度）。
- **诊断**：可修小间隙、开放端点、分支节点、零长度图元、过小图元、重复图元
  （含反向）、直线段自相交；严重级别（信息/警告/错误）。
- **修复**：单间隙修复（中点策略）与"全部安全修复"——一次撤销恢复整批。
- **工程格式** `.dxfstudio`（JSON）：几何/图层/单位/容差无损保存加载；脏标记
  与未保存保护。
- **清理 DXF 导出**：LINE/ARC/CIRCLE/LWPOLYLINE(+bulge)，单位/版本可选，
  默认拒绝覆盖源文件；状态栏显示导出报告。
- **导出/重导入回归**：修复后的文件导出再导入后保持修复状态。
- **本地化**：简体中文 / English，持久化。
- **性能**：拓扑与诊断近线性（5 万图元数秒内完成）。

## 截图

首次公开版本后补充。

## 支持的 DXF 实体

| DXF 实体 | 支持 |
|---|---|
| LINE | 完整 |
| ARC | 完整 |
| CIRCLE | 完整 |
| LWPOLYLINE（含 bulge） | 完整 |
| POLYLINE / Vertex2D | 完整 |
| ELLIPSE / SPLINE / INSERT / TEXT/MTEXT | 计数并报告，几何跳过 |

## 轮廓分析

- 基于拓扑的链构建（重合端点合并为节点）。
- 闭合轮廓检测（含闭合多段线的隐式闭合边）。
- 开口/闭合分类；外轮廓/孔/岛嵌套与深度。
- 每个轮廓的长度、包围框、有向面积与方向。

## 诊断与修复

- 小间隙（≤ 修复容差）→ 可修复；两端移动到中点。
- 无匹配端点的开放端点 → "无可匹配端点"（绝不显示数值哨兵）。
- 分支节点、零长度图元、过小图元、重复图元（含反向线）、直线段自相交。
- 单修复与"全部安全修复"（一次撤销恢复整批）。

## 工程保存 / 加载

- `.dxfstudio` JSON 格式，schema v1。
- 几何无损往返（Line/Arc/Circle/Polyline 含弧段）、图层、单位与容差设置。
- 脏标记与打开/打开工程/退出时的未保存保护。

## 清理 DXF 导出

- LINE / ARC / CIRCLE / LWPOLYLINE (+ bulge) 输出。
- 可配置输出单位（默认源单位，支持 mm）与 DXF 版本
  （R12 / R2000 / R2010 / R2018，默认 R2018）。
- 除非明确允许，绝不覆盖源文件。

## 架构

```
src/DxfContourStudio.Core         纯几何/拓扑 — 无 WPF、无 DXF
src/DxfContourStudio.Dxf          ACadSharp 适配层（读+写），库无关契约
src/DxfContourStudio.Application  文档、导入/导出、工程、命令、选择
src/DxfContourStudio.Wpf          仅 WPF UI（MVVM, CommunityToolkit.Mvvm）
```

详见 `docs/ARCHITECTURE.md` 与 `docs/ADR/`。

## 快速开始

要求：

- Windows 10/11
- .NET 10 SDK（见 `global.json`）
- Visual Studio 2022（可选）

克隆并构建：

```sh
git clone <repo-url>
cd DxfContourStudio
dotnet restore DxfContourStudio.sln
dotnet build DxfContourStudio.sln -c Release
dotnet test  DxfContourStudio.sln -c Release
dotnet run --project src/DxfContourStudio.Wpf
```

## 构建

```sh
dotnet restore DxfContourStudio.sln
dotnet build DxfContourStudio.sln -c Debug      # 0 警告 / 0 错误
dotnet build DxfContourStudio.sln -c Release
```

## 测试

- `tests/DxfContourStudio.Core.Tests` — 几何、拓扑、轮廓、相交、诊断数学。
- `tests/DxfContourStudio.Dxf.Tests` — 导入映射、单位转换、bulge。
- `tests/DxfContourStudio.Application.Tests` — 文档、命令、工程、导出往返、
  golden corpus、视图模型集成、STA 离屏渲染回归。

当前基线：**232 个测试全部通过**（Debug + Release，0 警告 / 0 错误）。
详见 `docs/TESTING.md`。

## 测试夹具

`testdata/dxf/` 包含手写回归夹具（19 个文件）：矩形、嵌套、间隙、分支、重复、
自相交、含 bulge 的多段线、混合图层与无单位文件。详见 `docs/TEST_CORPUS.md`。

## 已知限制

- 自相交检测目前仅覆盖**直线段**；线-弧 / 弧-弧尚未支持。
- R12 导出已提供但尚未完整人工实测。
- 10 万级实体渲染仍是每帧全量重绘（无批处理）。
- 这不是完整 CAD/CAM 系统：无 Trim/Extend/Offset/Fillet，无设备/激光/PLC/
  GCode 集成。

## 路线图

见 `docs/ROADMAP.md`。

## 许可

Apache-2.0 — 见 [LICENSE](LICENSE)。
