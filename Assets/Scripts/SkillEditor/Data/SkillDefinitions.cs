using System;
using System.Collections.Generic;

namespace GSkill.SkillEditor.Data
{
    /// <summary>
    /// 技能键值（技能 ID + 等级）。
    /// </summary>
    public readonly struct SkillKey : IEquatable<SkillKey>
    {
        public SkillKey(int skillId, int level)
        {
            SkillId = skillId;
            Level = level;
        }

        public int SkillId { get; }
        public int Level { get; }

        public bool Equals(SkillKey other) => SkillId == other.SkillId && Level == other.Level;

        public override bool Equals(object obj) => obj is SkillKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(SkillId, Level);

        public override string ToString() => $"{SkillId}:{Level}";
    }

    /// <summary>
    /// 技能定义基础数据。
    /// </summary>
    public sealed class SkillDefinition
    {
        public SkillDefinition(int skillId, int level, string name, string debugName, IReadOnlyList<int> actionSequenceIds, IReadOnlyList<int> triggerIds)
        {
            Key = new SkillKey(skillId, level);
            Name = name ?? string.Empty;
            DebugName = debugName ?? string.Empty;
            ActionSequenceIds = actionSequenceIds ?? Array.Empty<int>();
            TriggerIds = triggerIds ?? Array.Empty<int>();
        }

        public SkillKey Key { get; }
        public int SkillId => Key.SkillId;
        public int Level => Key.Level;
        public string Name { get; }
        public string DebugName { get; }
        public IReadOnlyList<int> ActionSequenceIds { get; }
        public IReadOnlyList<int> TriggerIds { get; }
    }

    /// <summary>
    /// Buff 键值（Buff ID + 等级）。
    /// </summary>
    public readonly struct BuffKey : IEquatable<BuffKey>
    {
        public BuffKey(int buffId, int level)
        {
            BuffId = buffId;
            Level = level;
        }

        public int BuffId { get; }
        public int Level { get; }

        public bool Equals(BuffKey other) => BuffId == other.BuffId && Level == other.Level;

        public override bool Equals(object obj) => obj is BuffKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(BuffId, Level);

        public override string ToString() => $"{BuffId}:{Level}";
    }

    /// <summary>
    /// Buff 定义基础数据。
    /// </summary>
    public sealed class BuffDefinition
    {
        public BuffDefinition(int buffId, int level, string name, string debugName, IReadOnlyList<int> actionSequenceIds, IReadOnlyList<int> triggerIds, int maxStack)
        {
            Key = new BuffKey(buffId, level);
            Name = name ?? string.Empty;
            DebugName = debugName ?? string.Empty;
            ActionSequenceIds = actionSequenceIds ?? Array.Empty<int>();
            TriggerIds = triggerIds ?? Array.Empty<int>();
            MaxStack = maxStack;
        }

        public BuffKey Key { get; }
        public int BuffId => Key.BuffId;
        public int Level => Key.Level;
        public string Name { get; }
        public string DebugName { get; }
        public IReadOnlyList<int> ActionSequenceIds { get; }
        public IReadOnlyList<int> TriggerIds { get; }
        public int MaxStack { get; }
    }

    /// <summary>
    /// 序列定义，描述时间轴中的多个效果槽。
    /// </summary>
    public sealed class SequenceDefinition
    {
        public SequenceDefinition(int actionId, string debugName, int repeatCount, int behaviorLock, IReadOnlyList<SequenceEffectSlot> effects)
        {
            ActionId = actionId;
            DebugName = debugName ?? string.Empty;
            RepeatCount = repeatCount;
            BehaviorLock = behaviorLock;
            Effects = effects ?? Array.Empty<SequenceEffectSlot>();
        }

        public int ActionId { get; }
        public string DebugName { get; }
        public int RepeatCount { get; }
        public int BehaviorLock { get; }
        public IReadOnlyList<SequenceEffectSlot> Effects { get; }
    }

    /// <summary>
    /// 序列中的单个效果槽，记录延迟和效果标识。
    /// </summary>
    public readonly struct SequenceEffectSlot
    {
        public SequenceEffectSlot(int delayMilliseconds, int effectId)
        {
            DelayMilliseconds = delayMilliseconds;
            EffectId = effectId;
        }

        public int DelayMilliseconds { get; }
        public int EffectId { get; }
    }

    /// <summary>
    /// 触发器定义，描述触发条件与关联动作。
    /// </summary>
    public sealed class TriggerDefinition
    {
        public TriggerDefinition(int triggerId, string debugName, int triggerType, int targetTag, int camp, IReadOnlyList<int> parameters, int actionId)
        {
            TriggerId = triggerId;
            DebugName = debugName ?? string.Empty;
            TriggerType = triggerType;
            TargetTag = targetTag;
            Camp = camp;
            Parameters = parameters ?? Array.Empty<int>();
            ActionId = actionId;
        }

        public int TriggerId { get; }
        public string DebugName { get; }
        public int TriggerType { get; }
        public int TargetTag { get; }
        public int Camp { get; }
        public IReadOnlyList<int> Parameters { get; }
        public int ActionId { get; }
    }

    /// <summary>
    /// 效果定义，用于描述操作类型与参数。
    /// </summary>
    public sealed class EffectDefinition
    {
        public EffectDefinition(int effectId, string debugName, int targetType, int operationEnum, IReadOnlyList<double> parameters, IReadOnlyList<double> arrayParam1, IReadOnlyList<double> arrayParam2, IReadOnlyList<string> strArray)
        {
            EffectId = effectId;
            DebugName = debugName ?? string.Empty;
            TargetType = targetType;
            OperationEnum = operationEnum;
            Parameters = parameters ?? Array.Empty<double>();
            ArrayParam1 = arrayParam1 ?? Array.Empty<double>();
            ArrayParam2 = arrayParam2 ?? Array.Empty<double>();
            StringArray = strArray ?? Array.Empty<string>();
        }

        public int EffectId { get; }
        public string DebugName { get; }
        public int TargetType { get; }
        public int OperationEnum { get; }
        public IReadOnlyList<double> Parameters { get; }
        public IReadOnlyList<double> ArrayParam1 { get; }
        public IReadOnlyList<double> ArrayParam2 { get; }
        public IReadOnlyList<string> StringArray { get; }
    }
}
