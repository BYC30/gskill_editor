using System.Collections.Generic;

namespace GSkill.SkillEditor.Data
{
    /// <summary>
    /// 统一封装技能相关定义集合，便于在编辑器中缓存。
    /// </summary>
    public sealed class SkillDatabase
    {
        public SkillDatabase(
            IReadOnlyDictionary<SkillKey, SkillDefinition> skills,
            IReadOnlyDictionary<BuffKey, BuffDefinition> buffs,
            IReadOnlyDictionary<int, SequenceDefinition> sequences,
            IReadOnlyDictionary<int, TriggerDefinition> triggers,
            IReadOnlyDictionary<int, EffectDefinition> effects)
        {
            Skills = skills ?? new Dictionary<SkillKey, SkillDefinition>();
            Buffs = buffs ?? new Dictionary<BuffKey, BuffDefinition>();
            Sequences = sequences ?? new Dictionary<int, SequenceDefinition>();
            Triggers = triggers ?? new Dictionary<int, TriggerDefinition>();
            Effects = effects ?? new Dictionary<int, EffectDefinition>();
        }

        public IReadOnlyDictionary<SkillKey, SkillDefinition> Skills { get; }
        public IReadOnlyDictionary<BuffKey, BuffDefinition> Buffs { get; }
        public IReadOnlyDictionary<int, SequenceDefinition> Sequences { get; }
        public IReadOnlyDictionary<int, TriggerDefinition> Triggers { get; }
        public IReadOnlyDictionary<int, EffectDefinition> Effects { get; }
    }
}
