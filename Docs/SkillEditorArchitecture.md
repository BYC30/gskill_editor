# 技能蓝图编辑器架构规划

## 项目定位
- 目标：在 Unity 内实现类 UE 蓝图的技能/BUFF 可视化编辑工具，直接驱动 `Config/*.csv` 数据生成。
- 用户：策划、数值与客户端程序，可在 Editor 模式下一起协作。
- 交付：以 EditorWindow 为主入口，支持技能、BUFF 的时间轴与触发器编排，可输出预览和校验报告。

## 核心模块
1. **数据访问层（Data Providers）**
   - 读取/写入 CSV，提供强类型数据模型（SkillDefinition、BuffDefinition、SequenceDefinition 等）。
   - 后续可替换为 Addressables/ScriptableObject 仓库，需定义接口 `ISkillDataProvider` 等。
2. **领域模型层（Domain Models）**
   - `SkillGraph`：由技能节点、Buff 节点、Trigger 节点组成的有向图。
   - `TimelineTrack`：描述技能或 Buff 上的子时间轴片段，存储帧/时间、引用的 Sequence 与 Effect。
   - `TriggerRule`：封装条件、参数及指向的动作序列。
3. **应用服务层（Application Services）**
   - `SkillGraphBuilder`：根据领域模型与数据层构建、刷新可视化节点。
   - `TimelineSimulationService`：提供秒表级预览、冲突检测、冷却计算等高级功能。
   - `CsvExportService`：负责与现有 CSV 结构对齐的落盘。
4. **表现层（Presentation / Editor UI）**
   - EditorWindow：`SkillBlueprintEditorWindow`
   - UI Toolkit 面板划分：左侧资源树，中部蓝图节点画布，底部时间轴 Inspector，右侧属性面板。
   - 使用 GraphView / UIToolkit 自定义控件表示节点连线与时间线片段。

## 迭代策略
- **MVP (里程碑 M0)**
  1. 完成 CSV 数据模型与接口定义；
  2. EditorWindow 雏形：左侧列表 + 右侧属性 Inspector；
  3. 支持读取技能、Buff 基础字段并展示。
- **M1**
  1. 引入 GraphView 节点画布，支持技能与 Buff 节点连线；
  2. 时间轴轨道可视化（Sequence/Effect 排布）及基础编辑；
  3. 触发器配置节点化，支持双向联动（技能/序列）。
- **M2**
  1. 预览/模拟：基于 Timeline 服务进行触发事件排程预演；
  2. 数据校验：冷却冲突、触发缺失提示；
  3. 批量导出与版本比对工具整合。

## 数据建模要点
- 时间单位：沿用 CSV 现有“延迟”字段（单位毫秒），领域模型内统一转换为秒 * float。
- 标识符：Skill/Buff/Sequence/Trigger 保留 int 型主键；节点引用使用 GUID 以便临时编辑。
- 容错：对 CSV 中缺失字段设置默认值，确保 Editor 在不完整数据下仍能打开。

## UI 技术选型
- 优先使用 `UnityEditor.Experimental.GraphView` + UI Toolkit，实现节点及连线；
- 时间轴部分采用 IMGUIContainer 过渡实现，后续替换为 UITK 可重用控件；
- 所有自定义控件封装在 `Assets/Editor/SkillEditor` 命名空间内，便于包化。

## 自动化与测试
- 数据层：使用 Unity Test Framework（EditMode）为 CSV 解析与导出编写单元测试。
- 表现层：提供 `SkillBlueprintEditorWindow` 静态入口便于自动化打开；
- CI：后续集成命令行批量导出校验。

## 后续需要的资源
- 自定义 USS/ UXML 资源，用于 GraphView 样式；
- 图标占位符（SVG/PNG），用于技能、Buff、Trigger 节点。
- 配置文件 ScriptableObject，用于持久化编辑器偏好（如默认路径、过滤器）。
