using System;
using System.Collections.Generic;
using System.Linq;
using GSkill.SkillEditor.Data;

namespace GSkill.SkillEditor.Graph
{
    /// <summary>
    /// 负责从 SkillDatabase 构建图形化数据
    /// </summary>
    public sealed class SkillGraphBuilder
    {
        private readonly SkillDatabase _database;

        public SkillGraphBuilder(SkillDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public SkillGraph BuildForSkill(
            SkillDefinition skill,
            IReadOnlyDictionary<int, Dictionary<int, int>> timelineOverrides = null,
            bool includeEffects = true)
        {
            if (skill == null)
            {
                throw new ArgumentNullException(nameof(skill));
            }

            var nodes = new Dictionary<string, SkillGraphNode>();
            var edges = new List<SkillGraphEdge>();

            var rootId = EnsureSkillNode(skill, nodes);
            BuildSharedGraph(skill.ActionSequenceIds, skill.TriggerIds, rootId, nodes, edges, timelineOverrides, includeEffects);

            return new SkillGraph(nodes.Values.ToList(), edges);
        }

        public SkillGraph BuildForBuff(
            BuffDefinition buff,
            IReadOnlyDictionary<int, Dictionary<int, int>> timelineOverrides = null,
            bool includeEffects = true)
        {
            if (buff == null)
            {
                throw new ArgumentNullException(nameof(buff));
            }

            var nodes = new Dictionary<string, SkillGraphNode>();
            var edges = new List<SkillGraphEdge>();

            var rootId = EnsureBuffNode(buff, nodes);
            BuildSharedGraph(buff.ActionSequenceIds, buff.TriggerIds, rootId, nodes, edges, timelineOverrides, includeEffects);

            return new SkillGraph(nodes.Values.ToList(), edges);
        }

        private void BuildSharedGraph(
            IReadOnlyList<int> sequenceIds,
            IReadOnlyList<int> triggerIds,
            string rootNodeId,
            Dictionary<string, SkillGraphNode> nodes,
            List<SkillGraphEdge> edges,
            IReadOnlyDictionary<int, Dictionary<int, int>> timelineOverrides,
            bool includeEffects)
        {
            var visitedTimelines = new HashSet<int>();

            foreach (var sequenceId in sequenceIds)
            {
                var timelineNodeId = EnsureTimelineNode(sequenceId, nodes, timelineOverrides, out var isMissingTimeline);
                edges.Add(new SkillGraphEdge(rootNodeId, timelineNodeId, SkillGraphPortCategory.SequenceOutput));

                if (!isMissingTimeline && visitedTimelines.Add(sequenceId) && includeEffects)
                {
                    AddSequenceEffects(sequenceId, timelineNodeId, nodes, edges, timelineOverrides);
                }
            }

            foreach (var triggerId in triggerIds)
            {
                var triggerNodeId = EnsureTriggerNode(triggerId, nodes, out var triggerDefinition);
                edges.Add(new SkillGraphEdge(rootNodeId, triggerNodeId, SkillGraphPortCategory.TriggerOutput));

                if (triggerDefinition == null || triggerDefinition.ActionId == 0)
                {
                    continue;
                }

                var timelineNodeId = EnsureTimelineNode(triggerDefinition.ActionId, nodes, timelineOverrides, out var isMissingTimeline);
                edges.Add(new SkillGraphEdge(triggerNodeId, timelineNodeId, SkillGraphPortCategory.TriggerActionOutput));

                if (!isMissingTimeline && visitedTimelines.Add(triggerDefinition.ActionId) && includeEffects)
                {
                    AddSequenceEffects(triggerDefinition.ActionId, timelineNodeId, nodes, edges, timelineOverrides);
                }
            }
        }

        private string EnsureSkillNode(SkillDefinition definition, Dictionary<string, SkillGraphNode> nodes)
        {
            var nodeId = $"skill:{definition.SkillId}:{definition.Level}";
            if (nodes.ContainsKey(nodeId))
            {
                return nodeId;
            }

            var displayName = !string.IsNullOrEmpty(definition.DebugName) ? definition.DebugName : definition.Name;
            var subtitle = $"等级 {definition.Level}";
            var description = $"技能 ID: {definition.SkillId}";
            var detail = new[]
            {
                $"动作数量: {definition.ActionSequenceIds.Count}",
                $"触发器数量: {definition.TriggerIds.Count}"
            };

            nodes[nodeId] = new SkillGraphNode(
                nodeId,
                SkillGraphNodeType.Skill,
                displayName,
                subtitle,
                description,
                SkillGraphNodeReference.ForSkill(definition),
                detail);
            return nodeId;
        }

        private string EnsureBuffNode(BuffDefinition definition, Dictionary<string, SkillGraphNode> nodes)
        {
            var nodeId = $"buff:{definition.BuffId}:{definition.Level}";
            if (nodes.ContainsKey(nodeId))
            {
                return nodeId;
            }

            var displayName = !string.IsNullOrEmpty(definition.DebugName) ? definition.DebugName : definition.Name;
            var subtitle = $"等级 {definition.Level} / 叠加 {definition.MaxStack}";
            var description = $"Buff ID: {definition.BuffId}";
            var detail = new[]
            {
                $"动作数量: {definition.ActionSequenceIds.Count}",
                $"触发器数量: {definition.TriggerIds.Count}"
            };

            nodes[nodeId] = new SkillGraphNode(
                nodeId,
                SkillGraphNodeType.Buff,
                displayName,
                subtitle,
                description,
                SkillGraphNodeReference.ForBuff(definition),
                detail);
            return nodeId;
        }

        private string EnsureTimelineNode(
            int sequenceId,
            Dictionary<string, SkillGraphNode> nodes,
            IReadOnlyDictionary<int, Dictionary<int, int>> overrides,
            out bool isMissing)
        {
            var nodeId = $"timeline:{sequenceId}";
            if (nodes.TryGetValue(nodeId, out var existing))
            {
                isMissing = existing.IsMissingReference;
                return nodeId;
            }

            if (!_database.Sequences.TryGetValue(sequenceId, out var sequence))
            {
                nodes[nodeId] = new SkillGraphNode(
                    nodeId,
                    SkillGraphNodeType.Placeholder,
                    $"动作 {sequenceId} 时间轴",
                    "CSV 中缺失",
                    "动作数据不存在，无法生成时间轴",
                    SkillGraphNodeReference.ForTimeline(sequenceId),
                    detailLines: Array.Empty<string>(),
                    timelineSlots: Array.Empty<SkillGraphTimelineSlot>(),
                    isMissingReference: true);
                isMissing = true;
                return nodeId;
            }

            var slots = new List<SkillGraphTimelineSlot>();
            for (var index = 0; index < sequence.Effects.Count; index++)
            {
                var slot = sequence.Effects[index];
                if (slot.EffectId == 0)
                {
                    continue;
                }

                var delay = ResolveDelay(sequenceId, index, slot.DelayMilliseconds, overrides);
                var title = $"效果 {slot.EffectId}";
                var summary = string.Empty;
                if (_database.Effects.TryGetValue(slot.EffectId, out var effect))
                {
                    title = !string.IsNullOrEmpty(effect.DebugName) ? effect.DebugName : title;
                    summary = BuildEffectSummary(effect);
                }

                slots.Add(new SkillGraphTimelineSlot(index, delay, slot.EffectId, title, summary));
            }

            var timelineTitle = (!string.IsNullOrEmpty(sequence.DebugName) ? sequence.DebugName : $"动作 {sequence.ActionId}") + " 时间轴";
            var timelineSubtitle = slots.Count > 0 ? $"关键帧: {slots.Count}" : "暂未绑定效果";
            var detailLines = slots.Select(s => $"槽 {s.SlotIndex + 1}: {s.EffectTitle} ({s.Delay}ms)").ToArray();

            nodes[nodeId] = new SkillGraphNode(
                nodeId,
                SkillGraphNodeType.Timeline,
                timelineTitle,
                timelineSubtitle,
                "拖动滑块即可调整触发时间",
                SkillGraphNodeReference.ForTimeline(sequenceId),
                detailLines,
                slots);

            isMissing = false;
            return nodeId;
        }

        private void AddSequenceEffects(
            int sequenceId,
            string timelineNodeId,
            Dictionary<string, SkillGraphNode> nodes,
            List<SkillGraphEdge> edges,
            IReadOnlyDictionary<int, Dictionary<int, int>> overrides)
        {
            if (!_database.Sequences.TryGetValue(sequenceId, out var sequence))
            {
                return;
            }

            for (var index = 0; index < sequence.Effects.Count; index++)
            {
                var slot = sequence.Effects[index];
                if (slot.EffectId == 0)
                {
                    continue;
                }

                var delay = ResolveDelay(sequenceId, index, slot.DelayMilliseconds, overrides);
                var effectNodeId = EnsureEffectNode(sequenceId, index, slot, delay, nodes, out _);
                edges.Add(new SkillGraphEdge(timelineNodeId, effectNodeId, SkillGraphPortCategory.TimelineEffectOutput));
            }
        }

        private string EnsureEffectNode(
            int sequenceId,
            int slotIndex,
            SequenceEffectSlot slot,
            int delay,
            Dictionary<string, SkillGraphNode> nodes,
            out EffectDefinition effectDefinition)
        {
            var nodeId = $"effect:{sequenceId}:{slotIndex}";
            if (nodes.TryGetValue(nodeId, out var existing))
            {
                effectDefinition = _database.Effects.TryGetValue(slot.EffectId, out var cached) ? cached : null;
                return nodeId;
            }

            if (_database.Effects.TryGetValue(slot.EffectId, out var effect))
            {
                effectDefinition = effect;
                var title = !string.IsNullOrEmpty(effect.DebugName) ? effect.DebugName : $"效果 {effect.EffectId}";
                var subtitle = $"延迟 {delay}ms";
                var detailLines = BuildEffectDetailLines(effect);
                nodes[nodeId] = new SkillGraphNode(
                    nodeId,
                    SkillGraphNodeType.Effect,
                    title,
                    subtitle,
                    $"效果 ID: {effect.EffectId}",
                    SkillGraphNodeReference.ForEffect(effect.EffectId, sequenceId, slotIndex),
                    detailLines);
            }
            else
            {
                effectDefinition = null;
                nodes[nodeId] = new SkillGraphNode(
                    nodeId,
                    SkillGraphNodeType.Placeholder,
                    $"效果 {slot.EffectId}",
                    $"延迟 {delay}ms",
                    "效果数据缺失",
                    SkillGraphNodeReference.ForEffect(slot.EffectId, sequenceId, slotIndex),
                    detailLines: Array.Empty<string>(),
                    timelineSlots: null,
                    isMissingReference: true);
            }

            return nodeId;
        }

        private string EnsureTriggerNode(int triggerId, Dictionary<string, SkillGraphNode> nodes, out TriggerDefinition triggerDefinition)
        {
            var nodeId = $"trigger:{triggerId}";
            if (nodes.TryGetValue(nodeId, out var existing))
            {
                triggerDefinition = _database.Triggers.TryGetValue(triggerId, out var cached) ? cached : null;
                return nodeId;
            }

            if (_database.Triggers.TryGetValue(triggerId, out var trigger))
            {
                triggerDefinition = trigger;
                var title = !string.IsNullOrEmpty(trigger.DebugName) ? trigger.DebugName : $"触发器 {trigger.TriggerId}";
                var subtitle = $"类型 {trigger.TriggerType}";
                var detail = new List<string>
                {
                    $"目标标签: {trigger.TargetTag}",
                    $"阵营: {trigger.Camp}"
                };
                if (trigger.Parameters.Count > 0)
                {
                    detail.Add("参数: " + string.Join(", ", trigger.Parameters));
                }
                if (trigger.ActionId != 0)
                {
                    detail.Add($"指向动作: {trigger.ActionId}");
                }

                nodes[nodeId] = new SkillGraphNode(
                    nodeId,
                    SkillGraphNodeType.Trigger,
                    title,
                    subtitle,
                    $"触发器 ID: {trigger.TriggerId}",
                    SkillGraphNodeReference.ForTrigger(trigger.TriggerId),
                    detail);
            }
            else
            {
                triggerDefinition = null;
                nodes[nodeId] = new SkillGraphNode(
                    nodeId,
                    SkillGraphNodeType.Placeholder,
                    $"触发器 {triggerId}",
                    "CSV 中缺失",
                    "触发器数据缺失",
                    SkillGraphNodeReference.ForTrigger(triggerId),
                    detailLines: Array.Empty<string>(),
                    timelineSlots: null,
                    isMissingReference: true);
            }

            return nodeId;
        }

        private static int ResolveDelay(int sequenceId, int slotIndex, int defaultValue, IReadOnlyDictionary<int, Dictionary<int, int>> overrides)
        {
            if (overrides != null && overrides.TryGetValue(sequenceId, out var slotDict) && slotDict != null && slotDict.TryGetValue(slotIndex, out var newDelay))
            {
                return newDelay;
            }

            return defaultValue;
        }

        private static string BuildEffectSummary(EffectDefinition effect)
        {
            if (effect == null)
            {
                return string.Empty;
            }

            if (effect.Parameters.Count > 0)
            {
                return "参数: " + string.Join(", ", effect.Parameters.Select(v => v.ToString("0.###")));
            }

            if (effect.ArrayParam1.Count > 0)
            {
                return "数组1: " + string.Join(", ", effect.ArrayParam1.Select(v => v.ToString("0.###")));
            }

            if (effect.StringArray.Count > 0)
            {
                return "字符串: " + string.Join(", ", effect.StringArray);
            }

            return "无额外参数";
        }

        private static IReadOnlyList<string> BuildEffectDetailLines(EffectDefinition effect)
        {
            var lines = new List<string>
            {
                $"目标类型: {effect.TargetType}",
                $"操作: {effect.OperationEnum}"
            };

            if (effect.Parameters.Count > 0)
            {
                lines.Add("参数: " + string.Join(", ", effect.Parameters.Select(v => v.ToString("0.###"))));
            }

            if (effect.ArrayParam1.Count > 0)
            {
                lines.Add("数组1: " + string.Join(", ", effect.ArrayParam1.Select(v => v.ToString("0.###"))));
            }

            if (effect.ArrayParam2.Count > 0)
            {
                lines.Add("数组2: " + string.Join(", ", effect.ArrayParam2.Select(v => v.ToString("0.###"))));
            }

            if (effect.StringArray.Count > 0)
            {
                lines.Add("字符串: " + string.Join(", ", effect.StringArray));
            }

            return lines;
        }
    }
}
