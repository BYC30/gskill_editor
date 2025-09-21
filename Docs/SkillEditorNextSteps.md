# 后续实现建议与测试校验方案

## 即将开展的功能迭代
1. **图形化节点画布（M1）**
   - 引入 GraphView，自定义 SkillNode、BuffNode、TriggerNode，支持节点拖拽、连线。
   - 设计 `SkillGraphBuilder`，将当前 SkillDatabase 映射为可视化节点结构。
   - 在节点 Inspector 中嵌入序列与触发器的快速编辑入口。
2. **时间轴编辑控件**
   - 基于 IMGUI/UITK 实现多轨道 Timeline，可拖拽 SequenceEffectSlot，显示延迟、效果名称。
   - 支持序列循环（repeat）与锁（behavior_lock）的可视化配置。
   - 提供对 EffectDefinition 关键参数的快速编辑表单。
3. **触发器编排与校验**
   - 触发器配置界面支持条件参数表格化编辑，实时提示数据缺失。
   - 与技能/序列联动，确保动作引用一致。
4. **导出与差异对比**
   - 实现 `CsvExportService`，对修改后的数据进行写回。
   - 增加差异对比视图，展示修改前后的数值变化。

## 测试与验证
- **数据层单元测试**
  - 使用 Unity EditMode Tests，为 `CsvSkillDataProvider` 增加解析测试，覆盖技能、Buff、序列、触发器文件的关键字段。
  - 构造包含空字段、多参数、异常数据的测试 CSV，验证容错。
- **编辑器回归测试**
  - 编写 `SkillBlueprintEditorWindow` 的打开与刷新冒烟测试，确保窗口可在 CI 中自动拉起。
  - 模拟选择技能/Buff 的操作，验证详情区域显示是否与源数据一致。
- **性能与稳定性**
  - 针对超大 CSV（>5k 行）执行加载性能测试，记录解析耗时与 GC Alloc。
  - 监控窗口交互时的异常日志，确保 UI 操作不会抛出未捕获异常。

## 工作流建议
- 约定所有编辑器配置写入 `ProjectSettings/SkillEditorSettings.asset`（ScriptableObject），记录 Config 路径、导出策略。
- 在 Git 中启用钩子，导出 CSV 时自动格式化表头顺序，避免误差异。
- 后续提交前运行“数据一致性校验”命令（待实现 CLI 工具），阻止缺失 ID/空引用进入主干。
