
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using GSkill.SkillEditor.Data;
using GSkill.SkillEditor.Graph;

namespace GSkill.SkillEditor.Windows
{
    /// <summary>
    /// 技能蓝图编辑器窗口
    /// </summary>
    public sealed class SkillBlueprintEditorWindow : EditorWindow
    {
        private ISkillDataProvider _dataProvider;
        private SkillDatabase _database;
        private SkillGraphBuilder _graphBuilder;
        private SkillGraphView _graphView;
        private IMGUIContainer _skillListGui;
        private IMGUIContainer _buffListGui;
        private IMGUIContainer _detailGui;
        private HelpBox _errorBox;
        private Label _configPathLabel;
        private Vector2 _skillScrollPosition;
        private Vector2 _buffScrollPosition;
        private Vector2 _detailScrollPosition;
        private SelectionContext _rootSelection;
        private SelectionContext _detailSelection;
        private readonly Dictionary<int, Dictionary<int, int>> _timelineOverrides = new();
        private bool _showGraphDetails = true;
        private string _configPath;
        private string _loadError;
        [MenuItem("Tools/Skill Blueprint Editor", priority = 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<SkillBlueprintEditorWindow>("技能蓝图编辑器");
            window.minSize = new Vector2(1024f, 640f);
            window.Show();
        }

        private void OnEnable()
        {
            TryInitializeProvider();
            ReloadDatabase();
        }

        private void OnDisable()
        {
            if (_graphView != null)
            {
                _graphView.NodeSelected -= OnGraphNodeSelected;
                _graphView.TimelineSlotChanged -= OnTimelineSlotChanged;
            }
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();

            BuildToolbar(rootVisualElement);
            BuildContent(rootVisualElement);

            RefreshAll();
            RebuildGraph();
        }

        #region UI 构建
        private void BuildToolbar(VisualElement root)
        {
            var toolbar = new Toolbar();

            var reloadButton = new ToolbarButton(ReloadDatabase)
            {
                text = "刷新数据"
            };
            toolbar.Add(reloadButton);

            var detailToggle = new ToolbarToggle
            {
                text = "详细视图",
                value = _showGraphDetails
            };
            detailToggle.RegisterValueChangedCallback(evt =>
            {
                if (_showGraphDetails == evt.newValue)
                {
                    return;
                }

                _showGraphDetails = evt.newValue;
                RebuildGraph();
            });
            toolbar.Add(detailToggle);

            toolbar.Add(new ToolbarSpacer());

            _configPathLabel = new Label
            {
                style =
                {
                    unityTextAlign = TextAnchor.MiddleRight,
                    flexGrow = 1f
                }
            };
            toolbar.Add(_configPathLabel);

            root.Add(toolbar);

            _errorBox = new HelpBox(string.Empty, HelpBoxMessageType.Error)
            {
                visible = false
            };
            root.Add(_errorBox);
        }
        private void BuildContent(VisualElement root)
        {
            var mainSplit = new TwoPaneSplitView(0, 320f, TwoPaneSplitViewOrientation.Horizontal);
            root.Add(mainSplit);

            var leftSplit = new TwoPaneSplitView(0, 240f, TwoPaneSplitViewOrientation.Vertical)
            {
                style = { minWidth = 300f }
            };
            mainSplit.Add(leftSplit);

            _skillListGui = new IMGUIContainer(DrawSkillColumn);
            leftSplit.Add(_skillListGui);

            _buffListGui = new IMGUIContainer(DrawBuffColumn);
            leftSplit.Add(_buffListGui);

            var rightSplit = new TwoPaneSplitView(0, 420f, TwoPaneSplitViewOrientation.Vertical)
            {
                style = { flexGrow = 1f }
            };
            mainSplit.Add(rightSplit);

            _graphView = new SkillGraphView();
            _graphView.NodeSelected += OnGraphNodeSelected;
            _graphView.TimelineSlotChanged += OnTimelineSlotChanged;
            rightSplit.Add(_graphView);

            _detailGui = new IMGUIContainer(DrawDetailPanel);
            rightSplit.Add(_detailGui);
        }

        #endregion
        #region 列表渲染

        private void DrawSkillColumn()
        {
            EditorGUILayout.LabelField("技能", EditorStyles.boldLabel);
            EditorGUILayout.Space(2f);

            _skillScrollPosition = EditorGUILayout.BeginScrollView(_skillScrollPosition);
            if (_database == null)
            {
                EditorGUILayout.HelpBox("尚未加载技能数据。", MessageType.Info);
            }
            else
            {
                foreach (var pair in _database.Skills.OrderBy(p => p.Key.SkillId))
                {
                    DrawSkillRow(pair.Value);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawBuffColumn()
        {
            EditorGUILayout.LabelField("Buff", EditorStyles.boldLabel);
            EditorGUILayout.Space(2f);

            _buffScrollPosition = EditorGUILayout.BeginScrollView(_buffScrollPosition);
            if (_database == null)
            {
                EditorGUILayout.HelpBox("尚未加载 Buff 数据。", MessageType.Info);
            }
            else
            {
                foreach (var pair in _database.Buffs.OrderBy(p => p.Key.BuffId))
                {
                    DrawBuffRow(pair.Value);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSkillRow(SkillDefinition skill)
        {
            if (skill == null)
            {
                return;
            }

            var displayName = !string.IsNullOrEmpty(skill.DebugName) ? skill.DebugName : skill.Name;
            var isSelected = _rootSelection.SelectionType == SelectionKind.Skill && _rootSelection.SkillKey.HasValue && _rootSelection.SkillKey.Value.Equals(skill.Key);
            var pressed = GUILayout.Toggle(isSelected, displayName, "Button");
            if (pressed && !isSelected)
            {
                SelectRootSkill(skill);
            }
        }

        private void DrawBuffRow(BuffDefinition buff)
        {
            if (buff == null)
            {
                return;
            }

            var displayName = !string.IsNullOrEmpty(buff.DebugName) ? buff.DebugName : buff.Name;
            var isSelected = _rootSelection.SelectionType == SelectionKind.Buff && _rootSelection.BuffKey.HasValue && _rootSelection.BuffKey.Value.Equals(buff.Key);
            var pressed = GUILayout.Toggle(isSelected, displayName, "Button");
            if (pressed && !isSelected)
            {
                SelectRootBuff(buff);
            }
        }

        #endregion
        #region 详情面板

        private void DrawDetailPanel()
        {
            if (!string.IsNullOrEmpty(_loadError))
            {
                EditorGUILayout.HelpBox(_loadError, MessageType.Error);
                return;
            }

            if (_database == null)
            {
                EditorGUILayout.HelpBox("尚未加载数据，请点击刷新。", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("详情", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _detailScrollPosition = EditorGUILayout.BeginScrollView(_detailScrollPosition);

            if (!_detailSelection.HasSelection)
            {
                EditorGUILayout.HelpBox("请选择技能或 Buff 查看详情。", MessageType.Info);
            }
            else
            {
                switch (_detailSelection.SelectionType)
                {
                    case SelectionKind.Skill:
                        DrawSkillDetail(_detailSelection.Skill);
                        break;
                    case SelectionKind.Buff:
                        DrawBuffDetail(_detailSelection.Buff);
                        break;
                    case SelectionKind.Sequence:
                        DrawSequenceDetail(_detailSelection);
                        break;
                    case SelectionKind.Trigger:
                        DrawTriggerDetail(_detailSelection);
                        break;
                    case SelectionKind.Effect:
                        DrawEffectDetail(_detailSelection);
                        break;
                }
            }

            EditorGUILayout.EndScrollView();
        }
        private void DrawSkillDetail(SkillDefinition skill)
        {
            if (skill == null)
            {
                EditorGUILayout.HelpBox("技能数据已失效。", MessageType.Warning);
                return;
            }

            var displayName = !string.IsNullOrEmpty(skill.DebugName) ? skill.DebugName : skill.Name;
            EditorGUILayout.LabelField("技能", displayName);
            EditorGUILayout.LabelField("等级", skill.Level.ToString());
            if (!string.IsNullOrEmpty(skill.Name))
            {
                EditorGUILayout.LabelField("策划名称", skill.Name);
            }
            EditorGUILayout.LabelField("技能 ID", skill.SkillId.ToString());
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("动作序列", EditorStyles.boldLabel);
            if (skill.ActionSequenceIds.Count == 0)
            {
                EditorGUILayout.HelpBox("未配置动作序列。", MessageType.Info);
            }
            else
            {
                foreach (var sequenceId in skill.ActionSequenceIds)
                {
                    DrawSequenceSummary(sequenceId);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("触发器", EditorStyles.boldLabel);
            if (skill.TriggerIds.Count == 0)
            {
                EditorGUILayout.HelpBox("未绑定触发器。", MessageType.Info);
            }
            else
            {
                foreach (var triggerId in skill.TriggerIds)
                {
                    DrawTriggerSummary(triggerId);
                }
            }
        }

        private void DrawBuffDetail(BuffDefinition buff)
        {
            if (buff == null)
            {
                EditorGUILayout.HelpBox("Buff 数据已失效。", MessageType.Warning);
                return;
            }

            var displayName = !string.IsNullOrEmpty(buff.DebugName) ? buff.DebugName : buff.Name;
            EditorGUILayout.LabelField("Buff", displayName);
            EditorGUILayout.LabelField("等级", buff.Level.ToString());
            EditorGUILayout.LabelField("最大叠加", buff.MaxStack.ToString());
            if (!string.IsNullOrEmpty(buff.Name))
            {
                EditorGUILayout.LabelField("策划名称", buff.Name);
            }
            EditorGUILayout.LabelField("Buff ID", buff.BuffId.ToString());
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("动作序列", EditorStyles.boldLabel);
            if (buff.ActionSequenceIds.Count == 0)
            {
                EditorGUILayout.HelpBox("未配置动作序列。", MessageType.Info);
            }
            else
            {
                foreach (var sequenceId in buff.ActionSequenceIds)
                {
                    DrawSequenceSummary(sequenceId);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("触发器", EditorStyles.boldLabel);
            if (buff.TriggerIds.Count == 0)
            {
                EditorGUILayout.HelpBox("未绑定触发器。", MessageType.Info);
            }
            else
            {
                foreach (var triggerId in buff.TriggerIds)
                {
                    DrawTriggerSummary(triggerId);
                }
            }
        }
        private void DrawSequenceSummary(int sequenceId)
        {
            var context = CreateSequenceContext(sequenceId);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (context.IsMissingReference)
                {
                    EditorGUILayout.LabelField("动作", $"动作 {sequenceId}");
                    EditorGUILayout.HelpBox("CSV 中缺失该动作。", MessageType.Warning);
                    if (GUILayout.Button("定位节点", EditorStyles.miniButton))
                    {
                        FocusSequenceNode(sequenceId);
                    }
                    return;
                }

                var sequence = context.Sequence;
                var displayName = !string.IsNullOrEmpty(sequence.DebugName) ? sequence.DebugName : $"动作 {sequence.ActionId}";
                EditorGUILayout.LabelField("动作", displayName);
                EditorGUILayout.LabelField("循环次数", sequence.RepeatCount.ToString());
                EditorGUILayout.LabelField("行为锁", sequence.BehaviorLock.ToString());

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("查看详情", EditorStyles.miniButton))
                    {
                        _detailSelection = context;
                        RefreshDetail();
                        FocusTimelineNode(sequence.ActionId);
                    }

                    if (GUILayout.Button("定位动作", EditorStyles.miniButton))
                    {
                        FocusSequenceNode(sequence.ActionId);
                    }

                    if (GUILayout.Button("定位时间轴", EditorStyles.miniButton))
                    {
                        FocusTimelineNode(sequence.ActionId);
                    }
                }
            }
        }

        private void DrawTriggerSummary(int triggerId)
        {
            var context = CreateTriggerContext(triggerId);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (context.IsMissingReference)
                {
                    EditorGUILayout.LabelField("触发器", $"触发器 {triggerId}");
                    EditorGUILayout.HelpBox("CSV 中缺失该触发器。", MessageType.Warning);
                    if (GUILayout.Button("定位节点", EditorStyles.miniButton))
                    {
                        FocusTriggerNode(triggerId);
                    }
                    return;
                }

                var trigger = context.Trigger;
                var displayName = !string.IsNullOrEmpty(trigger.DebugName) ? trigger.DebugName : $"触发器 {trigger.TriggerId}";
                EditorGUILayout.LabelField("触发器", displayName);
                EditorGUILayout.LabelField("类型", trigger.TriggerType.ToString());
                EditorGUILayout.LabelField("目标标签", trigger.TargetTag.ToString());
                EditorGUILayout.LabelField("阵营", trigger.Camp.ToString());
                if (trigger.Parameters.Count > 0)
                {
                    EditorGUILayout.LabelField("参数", string.Join(", ", trigger.Parameters));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (trigger.ActionId != 0 && GUILayout.Button("查看动作", EditorStyles.miniButton))
                    {
                        SelectSequenceById(trigger.ActionId);
                    }

                    if (GUILayout.Button("定位触发器", EditorStyles.miniButton))
                    {
                        FocusTriggerNode(trigger.TriggerId);
                    }
                }
            }
        }
        private void DrawSequenceDetail(SelectionContext selection)
        {
            if (selection.IsMissingReference)
            {
                var sequenceId = selection.SequenceId ?? 0;
                EditorGUILayout.HelpBox($"动作 {sequenceId} 在 CSV 中缺失。", MessageType.Warning);
                if (selection.SequenceId.HasValue && GUILayout.Button("定位节点", EditorStyles.miniButton))
                {
                    FocusSequenceNode(selection.SequenceId.Value);
                }
                return;
            }

            var sequence = selection.Sequence;
            if (sequence == null)
            {
                EditorGUILayout.HelpBox("动作数据已失效。", MessageType.Warning);
                return;
            }

            var displayName = !string.IsNullOrEmpty(sequence.DebugName) ? sequence.DebugName : $"动作 {sequence.ActionId}";
            EditorGUILayout.LabelField("动作", displayName);
            EditorGUILayout.LabelField("动作 ID", sequence.ActionId.ToString());
            EditorGUILayout.LabelField("循环次数", sequence.RepeatCount.ToString());
            EditorGUILayout.LabelField("行为锁", sequence.BehaviorLock.ToString());
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("效果时间轴", EditorStyles.boldLabel);
            if (sequence.Effects.Count == 0)
            {
                EditorGUILayout.HelpBox("未配置任何效果。", MessageType.Info);
            }
            else
            {
                for (var index = 0; index < sequence.Effects.Count; index++)
                {
                    var slot = sequence.Effects[index];
                    if (slot.EffectId == 0)
                    {
                        continue;
                    }

                    var delay = GetTimelineDelay(sequence.ActionId, index, slot.DelayMilliseconds);
                    var effectContext = CreateEffectContext(slot.EffectId, sequence.ActionId, index);
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField($"槽位 {index + 1}", EditorStyles.miniBoldLabel);
                        EditorGUILayout.LabelField("延迟", $"{delay} ms");
                        if (effectContext.IsMissingReference)
                        {
                            EditorGUILayout.HelpBox($"效果 {slot.EffectId} 缺失。", MessageType.Warning);
                        }
                        else
                        {
                            EditorGUILayout.LabelField("效果", !string.IsNullOrEmpty(effectContext.Effect.DebugName)
                                ? effectContext.Effect.DebugName
                                : $"效果 {effectContext.Effect.EffectId}");
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("查看效果", EditorStyles.miniButton))
                            {
                                SelectEffectFromSequence(slot.EffectId, sequence.ActionId, index);
                            }

                            if (GUILayout.Button("定位节点", EditorStyles.miniButton))
                            {
                                FocusEffectNode(sequence.ActionId, index);
                            }
                        }
                    }
                }
            }
        }

        private void DrawTriggerDetail(SelectionContext selection)
        {
            if (selection.IsMissingReference)
            {
                var id = selection.TriggerId ?? 0;
                EditorGUILayout.HelpBox($"触发器 {id} 在 CSV 中缺失。", MessageType.Warning);
                if (selection.TriggerId.HasValue && GUILayout.Button("定位节点", EditorStyles.miniButton))
                {
                    FocusTriggerNode(selection.TriggerId.Value);
                }
                return;
            }

            var trigger = selection.Trigger;
            if (trigger == null)
            {
                EditorGUILayout.HelpBox("触发器数据已失效。", MessageType.Warning);
                return;
            }

            var displayName = !string.IsNullOrEmpty(trigger.DebugName) ? trigger.DebugName : $"触发器 {trigger.TriggerId}";
            EditorGUILayout.LabelField("触发器", displayName);
            EditorGUILayout.LabelField("触发器 ID", trigger.TriggerId.ToString());
            EditorGUILayout.LabelField("类型", trigger.TriggerType.ToString());
            EditorGUILayout.LabelField("目标标签", trigger.TargetTag.ToString());
            EditorGUILayout.LabelField("阵营", trigger.Camp.ToString());
            if (trigger.Parameters.Count > 0)
            {
                EditorGUILayout.LabelField("参数", string.Join(", ", trigger.Parameters));
            }

            if (trigger.ActionId != 0)
            {
                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("查看动作", EditorStyles.miniButton))
                    {
                        SelectSequenceById(trigger.ActionId);
                    }

                    if (GUILayout.Button("定位动作节点", EditorStyles.miniButton))
                    {
                        FocusSequenceNode(trigger.ActionId);
                    }

                    if (GUILayout.Button("定位时间轴", EditorStyles.miniButton))
                    {
                        FocusTimelineNode(trigger.ActionId);
                    }
                }
            }
        }

        private void DrawEffectDetail(SelectionContext selection)
        {
            if (selection.IsMissingReference)
            {
                var id = selection.EffectId ?? 0;
                EditorGUILayout.HelpBox($"效果 {id} 在 CSV 中缺失。", MessageType.Warning);
                if (selection.SequenceId.HasValue && selection.EffectSlotIndex.HasValue && GUILayout.Button("定位节点", EditorStyles.miniButton))
                {
                    FocusEffectNode(selection.SequenceId.Value, selection.EffectSlotIndex.Value);
                }
                return;
            }

            var effect = selection.Effect;
            if (effect == null)
            {
                EditorGUILayout.HelpBox("效果数据已失效。", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("效果", !string.IsNullOrEmpty(effect.DebugName) ? effect.DebugName : $"效果 {effect.EffectId}");
            EditorGUILayout.LabelField("效果 ID", effect.EffectId.ToString());
            EditorGUILayout.LabelField("目标类型", effect.TargetType.ToString());
            EditorGUILayout.LabelField("操作枚举", effect.OperationEnum.ToString());

            if (selection.SequenceId.HasValue && selection.EffectSlotIndex.HasValue)
            {
                var originalDelay = 0;
                if (_database != null && _database.Sequences.TryGetValue(selection.SequenceId.Value, out var seq) && selection.EffectSlotIndex.Value < seq.Effects.Count)
                {
                    originalDelay = seq.Effects[selection.EffectSlotIndex.Value].DelayMilliseconds;
                }
                var delay = GetTimelineDelay(selection.SequenceId.Value, selection.EffectSlotIndex.Value, originalDelay);
                EditorGUILayout.LabelField("当前延迟", $"{delay} ms");
            }

            DrawDoubleList("参数", effect.Parameters);
            DrawDoubleList("数组参数1", effect.ArrayParam1);
            DrawDoubleList("数组参数2", effect.ArrayParam2);
            DrawStringList("字符串数组", effect.StringArray);
        }

        private static void DrawDoubleList(string label, IReadOnlyList<double> values)
        {
            if (values == null || values.Count == 0)
            {
                return;
            }

            var text = string.Join(", ", values.Select(v => v.ToString("0.###")));
            EditorGUILayout.LabelField(label, text);
        }

        private static void DrawStringList(string label, IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return;
            }

            var text = string.Join(", ", values);
            EditorGUILayout.LabelField(label, text);
        }

        #endregion
        #region 选择与 Graph 交互

        private void SelectRootSkill(SkillDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            if (_rootSelection.SelectionType == SelectionKind.Skill && _rootSelection.SkillKey.HasValue && _rootSelection.SkillKey.Value.Equals(definition.Key))
            {
                _detailSelection = SelectionContext.ForSkill(definition);
                RefreshDetail();
                return;
            }

            _rootSelection = SelectionContext.ForSkill(definition);
            _detailSelection = _rootSelection;
            RebuildGraph();
            RefreshLists();
            RefreshDetail();
        }

        private void SelectRootBuff(BuffDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            if (_rootSelection.SelectionType == SelectionKind.Buff && _rootSelection.BuffKey.HasValue && _rootSelection.BuffKey.Value.Equals(definition.Key))
            {
                _detailSelection = SelectionContext.ForBuff(definition);
                RefreshDetail();
                return;
            }

            _rootSelection = SelectionContext.ForBuff(definition);
            _detailSelection = _rootSelection;
            RebuildGraph();
            RefreshLists();
            RefreshDetail();
        }

        private void SelectSequenceById(int sequenceId)
        {
            var context = CreateSequenceContext(sequenceId);
            if (!context.HasSelection && !context.IsMissingReference)
            {
                return;
            }

            _detailSelection = context;
            RefreshDetail();
            FocusTimelineNode(sequenceId);
        }

        private void SelectEffectFromSequence(int effectId, int sequenceId, int slotIndex)
        {
            var context = CreateEffectContext(effectId, sequenceId, slotIndex);
            if (!context.HasSelection && !context.IsMissingReference)
            {
                return;
            }

            _detailSelection = context;
            RefreshDetail();
            FocusEffectNode(sequenceId, slotIndex);
        }

        private void OnGraphNodeSelected(SkillGraphNode node)
        {
            if (node == null || _database == null)
            {
                return;
            }

            SelectionContext context = node.NodeType switch
            {
                SkillGraphNodeType.Skill when node.Reference.SkillKey.HasValue && _database.Skills.TryGetValue(node.Reference.SkillKey.Value, out var skill) => SelectionContext.ForSkill(skill),
                SkillGraphNodeType.Buff when node.Reference.BuffKey.HasValue && _database.Buffs.TryGetValue(node.Reference.BuffKey.Value, out var buff) => SelectionContext.ForBuff(buff),
                SkillGraphNodeType.Sequence when node.Reference.SequenceId.HasValue => CreateSequenceContext(node.Reference.SequenceId.Value),
                SkillGraphNodeType.Timeline when node.Reference.SequenceId.HasValue => CreateSequenceContext(node.Reference.SequenceId.Value),
                SkillGraphNodeType.Trigger when node.Reference.TriggerId.HasValue => CreateTriggerContext(node.Reference.TriggerId.Value),
                SkillGraphNodeType.Effect when node.Reference.EffectId.HasValue => CreateEffectContext(node.Reference.EffectId.Value, node.Reference.SequenceId, node.Reference.EffectSlotIndex),
                SkillGraphNodeType.Placeholder => CreatePlaceholderContext(node.Reference),
                _ => SelectionContext.None
            };

            if (!context.HasSelection && !context.IsMissingReference)
            {
                return;
            }

            switch (context.SelectionType)
            {
                case SelectionKind.Skill when context.Skill != null:
                    SelectRootSkill(context.Skill);
                    break;
                case SelectionKind.Buff when context.Buff != null:
                    SelectRootBuff(context.Buff);
                    break;
                default:
                    _detailSelection = context;
                    RefreshDetail();
                    break;
            }
        }

        private SelectionContext CreateSequenceContext(int sequenceId)
        {
            if (_database == null)
            {
                return SelectionContext.None;
            }

            return _database.Sequences.TryGetValue(sequenceId, out var sequence)
                ? SelectionContext.ForSequence(sequence)
                : SelectionContext.ForMissingSequence(sequenceId);
        }

        private SelectionContext CreateTriggerContext(int triggerId)
        {
            if (_database == null)
            {
                return SelectionContext.None;
            }

            return _database.Triggers.TryGetValue(triggerId, out var trigger)
                ? SelectionContext.ForTrigger(trigger)
                : SelectionContext.ForMissingTrigger(triggerId);
        }

        private SelectionContext CreateEffectContext(int effectId, int? sequenceId = null, int? slotIndex = null)
        {
            if (_database == null)
            {
                return SelectionContext.None;
            }

            return _database.Effects.TryGetValue(effectId, out var effect)
                ? SelectionContext.ForEffect(effect, sequenceId, slotIndex)
                : SelectionContext.ForMissingEffect(effectId, sequenceId, slotIndex);
        }

        private SelectionContext CreatePlaceholderContext(SkillGraphNodeReference reference)
        {
            return reference.NodeType switch
            {
                SkillGraphNodeType.Sequence when reference.SequenceId.HasValue => SelectionContext.ForMissingSequence(reference.SequenceId.Value),
                SkillGraphNodeType.Timeline when reference.SequenceId.HasValue => SelectionContext.ForMissingSequence(reference.SequenceId.Value),
                SkillGraphNodeType.Trigger when reference.TriggerId.HasValue => SelectionContext.ForMissingTrigger(reference.TriggerId.Value),
                SkillGraphNodeType.Effect when reference.EffectId.HasValue => SelectionContext.ForMissingEffect(reference.EffectId.Value, reference.SequenceId, reference.EffectSlotIndex),
                _ => SelectionContext.None
            };
        }

        private void FocusSequenceNode(int sequenceId)
        {
            _graphView?.FocusNode($"sequence:{sequenceId}");
        }

        private void FocusTimelineNode(int sequenceId)
        {
            _graphView?.FocusNode($"timeline:{sequenceId}");
        }

        private void FocusTriggerNode(int triggerId)
        {
            _graphView?.FocusNode($"trigger:{triggerId}");
        }

        private void FocusEffectNode(int sequenceId, int slotIndex)
        {
            _graphView?.FocusNode($"effect:{sequenceId}:{slotIndex}");
        }

        #endregion
        #region 数据加载与刷新

        private void ReloadDatabase()
        {
            if (_dataProvider == null)
            {
                TryInitializeProvider();
                if (_dataProvider == null)
                {
                    RefreshAll();
                    _graphView?.ClearGraph();
                    return;
                }
            }

            try
            {
                _database = _dataProvider.Load();
                _graphBuilder = _database != null ? new SkillGraphBuilder(_database) : null;
                _loadError = string.Empty;
                RebindSelections();
                RebuildGraph();
            }
            catch (Exception ex)
            {
                _database = null;
                _graphBuilder = null;
                _rootSelection = SelectionContext.None;
                _detailSelection = SelectionContext.None;
                _loadError = $"读取 CSV 数据失败: {ex.Message}";
                _graphView?.ClearGraph();
            }
            finally
            {
                RefreshAll();
            }
        }

        private void TryInitializeProvider()
        {
            if (_dataProvider != null)
            {
                return;
            }

            try
            {
                _configPath = EnsureConfigPath();
                _dataProvider = new CsvSkillDataProvider(_configPath);
                _loadError = string.Empty;
            }
            catch (Exception ex)
            {
                _loadError = $"初始化数据提供器失败: {ex.Message}";
            }
            finally
            {
                RefreshHeader();
            }
        }

        private static string EnsureConfigPath()
        {
            var assetPath = Application.dataPath;
            if (string.IsNullOrEmpty(assetPath))
            {
                throw new InvalidOperationException("无法获取 Assets 目录，请在 Unity 项目中打开编辑器窗口。");
            }

            var projectRoot = Directory.GetParent(assetPath)?.FullName ?? assetPath;
            var configPath = Path.Combine(projectRoot, "Config");
            if (!Directory.Exists(configPath))
            {
                Directory.CreateDirectory(configPath);
            }

            return configPath;
        }

        private void RebindSelections()
        {
            if (_database == null)
            {
                _rootSelection = SelectionContext.None;
                _detailSelection = SelectionContext.None;
                return;
            }

            _rootSelection = _rootSelection.Rebind(_database);
            if (!_rootSelection.HasSelection)
            {
                _detailSelection = SelectionContext.None;
                return;
            }

            var reboundDetail = _detailSelection.Rebind(_database);
            _detailSelection = reboundDetail.HasSelection ? reboundDetail : _rootSelection;
        }

        private void RebuildGraph()
        {
            if (_graphView == null)
            {
                return;
            }

            if (_graphBuilder == null || !_rootSelection.HasSelection)
            {
                _graphView.ClearGraph();
                return;
            }

            SkillGraph graph = _rootSelection.SelectionType switch
            {
                SelectionKind.Skill when _rootSelection.Skill != null => _graphBuilder.BuildForSkill(_rootSelection.Skill, _timelineOverrides, includeEffects: _showGraphDetails),
                SelectionKind.Buff when _rootSelection.Buff != null => _graphBuilder.BuildForBuff(_rootSelection.Buff, _timelineOverrides, includeEffects: _showGraphDetails),
                _ => null
            };

            if (graph == null)
            {
                _graphView.ClearGraph();
                return;
            }

            _graphView.BuildGraph(graph, _showGraphDetails);
        }

        private void RefreshAll()
        {
            RefreshHeader();
            RefreshLists();
            RefreshDetail();
        }

        private void RefreshHeader()
        {
            if (_configPathLabel != null)
            {
                _configPathLabel.text = string.IsNullOrEmpty(_configPath) ? "配置目录: 未初始化" : $"配置目录: {_configPath}";
            }

            if (_errorBox != null)
            {
                if (string.IsNullOrEmpty(_loadError))
                {
                    _errorBox.visible = false;
                }
                else
                {
                    _errorBox.visible = true;
                    _errorBox.text = _loadError;
                }
            }
        }

        private void RefreshLists()
        {
            _skillListGui?.MarkDirtyRepaint();
            _buffListGui?.MarkDirtyRepaint();
        }

        private void RefreshDetail()
        {
            _detailGui?.MarkDirtyRepaint();
        }

        #endregion
        #region 时间轴覆写

        private void OnTimelineSlotChanged(int sequenceId, int slotIndex, int delay)
        {
            if (!_timelineOverrides.TryGetValue(sequenceId, out var slotDict))
            {
                slotDict = new Dictionary<int, int>();
                _timelineOverrides[sequenceId] = slotDict;
            }

            slotDict[slotIndex] = delay;
            RefreshDetail();
        }

        private int GetTimelineDelay(int sequenceId, int slotIndex, int originalDelay)
        {
            if (_timelineOverrides.TryGetValue(sequenceId, out var slotDict) && slotDict.TryGetValue(slotIndex, out var overrideDelay))
            {
                return overrideDelay;
            }

            return originalDelay;
        }

        #endregion
        #region 选择上下文定义

        private readonly struct SelectionContext
        {
            private SelectionContext(
                SelectionKind selectionType,
                SkillDefinition skill,
                BuffDefinition buff,
                SequenceDefinition sequence,
                TriggerDefinition trigger,
                EffectDefinition effect,
                SkillKey? skillKey,
                BuffKey? buffKey,
                int? sequenceId,
                int? triggerId,
                int? effectId,
                int? effectSlotIndex,
                bool isMissing)
            {
                SelectionType = selectionType;
                Skill = skill;
                Buff = buff;
                Sequence = sequence;
                Trigger = trigger;
                Effect = effect;
                SkillKey = skillKey;
                BuffKey = buffKey;
                SequenceId = sequenceId;
                TriggerId = triggerId;
                EffectId = effectId;
                EffectSlotIndex = effectSlotIndex;
                IsMissingReference = isMissing;
            }

            public SelectionKind SelectionType { get; }
            public SkillDefinition Skill { get; }
            public BuffDefinition Buff { get; }
            public SequenceDefinition Sequence { get; }
            public TriggerDefinition Trigger { get; }
            public EffectDefinition Effect { get; }
            public SkillKey? SkillKey { get; }
            public BuffKey? BuffKey { get; }
            public int? SequenceId { get; }
            public int? TriggerId { get; }
            public int? EffectId { get; }
            public int? EffectSlotIndex { get; }
            public bool IsMissingReference { get; }
            public bool HasSelection => SelectionType != SelectionKind.None && !IsMissingReference;

            public SelectionContext Rebind(SkillDatabase database)
            {
                if (database == null)
                {
                    return None;
                }

                return SelectionType switch
                {
                    SelectionKind.Skill when SkillKey.HasValue && database.Skills.TryGetValue(SkillKey.Value, out var skill) => ForSkill(skill),
                    SelectionKind.Buff when BuffKey.HasValue && database.Buffs.TryGetValue(BuffKey.Value, out var buff) => ForBuff(buff),
                    SelectionKind.Sequence when SequenceId.HasValue && database.Sequences.TryGetValue(SequenceId.Value, out var sequence) => ForSequence(sequence),
                    SelectionKind.Trigger when TriggerId.HasValue && database.Triggers.TryGetValue(TriggerId.Value, out var trigger) => ForTrigger(trigger),
                    SelectionKind.Effect when EffectId.HasValue && database.Effects.TryGetValue(EffectId.Value, out var effect) => ForEffect(effect, SequenceId, EffectSlotIndex),
                    _ => this
                };
            }

            public static SelectionContext ForSkill(SkillDefinition definition)
            {
                return new SelectionContext(SelectionKind.Skill, definition, null, null, null, null, definition.Key, null, null, null, null, null, false);
            }

            public static SelectionContext ForBuff(BuffDefinition definition)
            {
                return new SelectionContext(SelectionKind.Buff, null, definition, null, null, null, null, definition.Key, null, null, null, null, false);
            }

            public static SelectionContext ForSequence(SequenceDefinition definition)
            {
                return new SelectionContext(SelectionKind.Sequence, null, null, definition, null, null, null, null, definition.ActionId, null, null, null, false);
            }

            public static SelectionContext ForTrigger(TriggerDefinition definition)
            {
                return new SelectionContext(SelectionKind.Trigger, null, null, null, definition, null, null, null, null, definition.TriggerId, null, null, false);
            }

            public static SelectionContext ForEffect(EffectDefinition definition, int? sequenceId, int? slotIndex)
            {
                return new SelectionContext(SelectionKind.Effect, null, null, null, null, definition, null, null, sequenceId, null, definition.EffectId, slotIndex, false);
            }

            public static SelectionContext ForMissingSequence(int sequenceId)
            {
                return new SelectionContext(SelectionKind.Sequence, null, null, null, null, null, null, null, sequenceId, null, null, null, true);
            }

            public static SelectionContext ForMissingTrigger(int triggerId)
            {
                return new SelectionContext(SelectionKind.Trigger, null, null, null, null, null, null, null, null, triggerId, null, null, true);
            }

            public static SelectionContext ForMissingEffect(int effectId, int? sequenceId, int? slotIndex)
            {
                return new SelectionContext(SelectionKind.Effect, null, null, null, null, null, null, null, sequenceId, null, effectId, slotIndex, true);
            }

            public static SelectionContext None => default;
        }

        private enum SelectionKind
        {
            None,
            Skill,
            Buff,
            Sequence,
            Trigger,
            Effect
        }

        #endregion
    }
}






