using System;
using System.Collections.Generic;
using GSkill.SkillEditor.Data;

namespace GSkill.SkillEditor.Graph
{
    /// <summary>
    /// 蓝图节点类型
    /// </summary>
    public enum SkillGraphNodeType
    {
        Skill,
        Buff,
        Sequence,
        Timeline,
        Trigger,
        Effect,
        Placeholder
    }

    /// <summary>
    /// 端口分类，方便 GraphView 建立连接
    /// </summary>
    public enum SkillGraphPortCategory
    {
        Default,
        SequenceOutput,
        TriggerOutput,
        TriggerActionOutput,
        TimelineEffectOutput
    }

    /// <summary>
    /// 时间轴上的槽位信息
    /// </summary>
    public sealed class SkillGraphTimelineSlot
    {
        public SkillGraphTimelineSlot(int slotIndex, int delay, int effectId, string effectTitle, string summary)
        {
            SlotIndex = slotIndex;
            Delay = delay;
            EffectId = effectId;
            EffectTitle = effectTitle ?? string.Empty;
            Summary = summary ?? string.Empty;
        }

        public int SlotIndex { get; }
        public int Delay { get; }
        public int EffectId { get; }
        public string EffectTitle { get; }
        public string Summary { get; }
    }

    /// <summary>
    /// 节点的引用信息，指向原始数据
    /// </summary>
    public readonly struct SkillGraphNodeReference
    {
        public SkillGraphNodeReference(
            SkillGraphNodeType nodeType,
            SkillKey? skillKey = null,
            BuffKey? buffKey = null,
            int? sequenceId = null,
            int? triggerId = null,
            int? effectId = null,
            int? effectSlotIndex = null)
        {
            NodeType = nodeType;
            SkillKey = skillKey;
            BuffKey = buffKey;
            SequenceId = sequenceId;
            TriggerId = triggerId;
            EffectId = effectId;
            EffectSlotIndex = effectSlotIndex;
        }

        public SkillGraphNodeType NodeType { get; }
        public SkillKey? SkillKey { get; }
        public BuffKey? BuffKey { get; }
        public int? SequenceId { get; }
        public int? TriggerId { get; }
        public int? EffectId { get; }
        public int? EffectSlotIndex { get; }

        public static SkillGraphNodeReference ForSkill(SkillDefinition definition)
        {
            return new SkillGraphNodeReference(SkillGraphNodeType.Skill, skillKey: definition?.Key);
        }

        public static SkillGraphNodeReference ForBuff(BuffDefinition definition)
        {
            return new SkillGraphNodeReference(SkillGraphNodeType.Buff, buffKey: definition?.Key);
        }

        public static SkillGraphNodeReference ForSequence(int sequenceId)
        {
            return new SkillGraphNodeReference(SkillGraphNodeType.Sequence, sequenceId: sequenceId);
        }

        public static SkillGraphNodeReference ForTimeline(int sequenceId)
        {
            return new SkillGraphNodeReference(SkillGraphNodeType.Timeline, sequenceId: sequenceId);
        }

        public static SkillGraphNodeReference ForTrigger(int triggerId)
        {
            return new SkillGraphNodeReference(SkillGraphNodeType.Trigger, triggerId: triggerId);
        }

        public static SkillGraphNodeReference ForEffect(int effectId, int? sequenceId = null, int? slotIndex = null)
        {
            return new SkillGraphNodeReference(SkillGraphNodeType.Effect, sequenceId: sequenceId, effectId: effectId, effectSlotIndex: slotIndex);
        }
    }

    /// <summary>
    /// 蓝图节点实体
    /// </summary>
    public sealed class SkillGraphNode
    {
        public SkillGraphNode(
            string id,
            SkillGraphNodeType nodeType,
            string title,
            string subtitle,
            string description,
            SkillGraphNodeReference reference,
            IReadOnlyList<string> detailLines = null,
            IReadOnlyList<SkillGraphTimelineSlot> timelineSlots = null,
            bool isMissingReference = false)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new System.ArgumentException("节点 ID 不能为空", nameof(id));
            }

            Id = id;
            NodeType = nodeType;
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            Description = description ?? string.Empty;
            Reference = reference;
            DetailLines = detailLines ?? System.Array.Empty<string>();
            TimelineSlots = timelineSlots ?? System.Array.Empty<SkillGraphTimelineSlot>();
            IsMissingReference = isMissingReference;
        }

        public string Id { get; }
        public SkillGraphNodeType NodeType { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public string Description { get; }
        public SkillGraphNodeReference Reference { get; }
        public IReadOnlyList<string> DetailLines { get; }
        public IReadOnlyList<SkillGraphTimelineSlot> TimelineSlots { get; }
        public bool IsMissingReference { get; }
    }

    /// <summary>
    /// 边信息
    /// </summary>
    public sealed class SkillGraphEdge
    {
        public SkillGraphEdge(string fromNodeId, string toNodeId, SkillGraphPortCategory fromPort = SkillGraphPortCategory.Default, SkillGraphPortCategory toPort = SkillGraphPortCategory.Default)
        {
            if (string.IsNullOrWhiteSpace(fromNodeId))
            {
                throw new System.ArgumentException("源节点 ID 不能为空", nameof(fromNodeId));
            }

            if (string.IsNullOrWhiteSpace(toNodeId))
            {
                throw new System.ArgumentException("目标节点 ID 不能为空", nameof(toNodeId));
            }

            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            FromPort = fromPort;
            ToPort = toPort;
        }

        public string FromNodeId { get; }
        public string ToNodeId { get; }
        public SkillGraphPortCategory FromPort { get; }
        public SkillGraphPortCategory ToPort { get; }
    }

    /// <summary>
    /// 最终图数据
    /// </summary>
    public sealed class SkillGraph
    {
        public SkillGraph(IReadOnlyList<SkillGraphNode> nodes, IReadOnlyList<SkillGraphEdge> edges)
        {
            Nodes = nodes ?? System.Array.Empty<SkillGraphNode>();
            Edges = edges ?? System.Array.Empty<SkillGraphEdge>();
        }

        public IReadOnlyList<SkillGraphNode> Nodes { get; }
        public IReadOnlyList<SkillGraphEdge> Edges { get; }
    }
}

