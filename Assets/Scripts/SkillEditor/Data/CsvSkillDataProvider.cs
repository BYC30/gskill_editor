using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace GSkill.SkillEditor.Data
{
    /// <summary>
    /// 基于 CSV 的技能数据提供器，实现最基础的读操作。
    /// </summary>
    public sealed class CsvSkillDataProvider : ISkillDataProvider
    {
        private readonly string _configRoot;

        public CsvSkillDataProvider(string configRoot)
        {
            if (string.IsNullOrWhiteSpace(configRoot))
            {
                throw new ArgumentException("配置目录不能为空", nameof(configRoot));
            }

            _configRoot = configRoot;
        }

        public SkillDatabase Load()
        {
            var skills = LoadSkills();
            var buffs = LoadBuffs();
            var sequences = LoadSequences();
            var triggers = LoadTriggers();
            var effects = LoadEffects();
            return new SkillDatabase(skills, buffs, sequences, triggers, effects);
        }

        public Task<SkillDatabase> LoadAsync()
        {
            return Task.Run(Load);
        }

        public void Save(SkillDatabase database)
        {
            throw new NotSupportedException("MVP 阶段暂未实现 CSV 写回，请使用导出流水线");
        }

        public Task SaveAsync(SkillDatabase database)
        {
            return Task.Run(() => Save(database));
        }

        private Dictionary<SkillKey, SkillDefinition> LoadSkills()
        {
            var path = Path.Combine(_configRoot, "ECSSkill.csv");
            var table = SimpleCsvTable.Load(path, "skill_id");
            var dict = new Dictionary<SkillKey, SkillDefinition>();
            for (var i = 0; i < table.Rows.Count; i++)
            {
                var id = table.GetInt(i, "skill_id");
                if (id == 0)
                {
                    continue;
                }

                var level = table.GetInt(i, "skill_lev", 1);
                var name = table.GetString(i, "skill_name");
                var debugName = table.GetString(i, "debug_name");
                var actions = table.GetIntArray(i, "actions");
                var triggers = table.GetIntArray(i, "triggers");
                var definition = new SkillDefinition(id, level, name, debugName, actions, triggers);
                dict[definition.Key] = definition;
            }

            return dict;
        }

        private Dictionary<BuffKey, BuffDefinition> LoadBuffs()
        {
            var path = Path.Combine(_configRoot, "ECSBuff.csv");
            var table = SimpleCsvTable.Load(path, "buff_id");
            var dict = new Dictionary<BuffKey, BuffDefinition>();
            for (var i = 0; i < table.Rows.Count; i++)
            {
                var id = table.GetInt(i, "buff_id");
                if (id == 0)
                {
                    continue;
                }

                var level = table.GetInt(i, "lev", 1);
                var name = table.GetString(i, "buff_name");
                var debugName = table.GetString(i, "debug_name");
                var actions = table.GetIntArray(i, "actions");
                var triggers = table.GetIntArray(i, "triggers");
                var maxStack = table.GetInt(i, "max_stack", 1);
                var definition = new BuffDefinition(id, level, name, debugName, actions, triggers, maxStack);
                dict[definition.Key] = definition;
            }

            return dict;
        }

        private Dictionary<int, SequenceDefinition> LoadSequences()
        {
            var path = Path.Combine(_configRoot, "ECSSequence.csv");
            var table = SimpleCsvTable.Load(path, "action_id");
            var dict = new Dictionary<int, SequenceDefinition>();
            for (var i = 0; i < table.Rows.Count; i++)
            {
                var actionId = table.GetInt(i, "action_id");
                if (actionId == 0)
                {
                    continue;
                }

                var debugName = table.GetString(i, "debug_name");
                var repeat = table.GetInt(i, "repeat");
                var behaviorLock = table.GetInt(i, "behavior_lock");
                var effects = new List<SequenceEffectSlot>();
                for (var slotIndex = 0; slotIndex < 16; slotIndex++)
                {
                    var delayColumn = $"delay{slotIndex}";
                    var effectColumn = $"effect{slotIndex}";
                    if (!table.ContainsColumn(delayColumn) && !table.ContainsColumn(effectColumn))
                    {
                        continue;
                    }

                    var effectId = table.GetInt(i, effectColumn, 0);
                    if (effectId == 0)
                    {
                        continue;
                    }

                    var delay = table.GetInt(i, delayColumn, 0);
                    effects.Add(new SequenceEffectSlot(delay, effectId));
                }

                var definition = new SequenceDefinition(actionId, debugName, repeat, behaviorLock, effects);
                dict[actionId] = definition;
            }

            return dict;
        }

        private Dictionary<int, TriggerDefinition> LoadTriggers()
        {
            var path = Path.Combine(_configRoot, "ECSTrigger.csv");
            var table = SimpleCsvTable.Load(path, "trigger_id");
            var dict = new Dictionary<int, TriggerDefinition>();
            for (var i = 0; i < table.Rows.Count; i++)
            {
                var triggerId = table.GetInt(i, "trigger_id");
                if (triggerId == 0)
                {
                    continue;
                }

                var debugName = table.GetString(i, "debug_name");
                var triggerType = table.GetInt(i, "trigger_type");
                var targetTag = table.GetInt(i, "target_tag");
                var camp = table.GetInt(i, "camp");
                var parameters = new List<int>();
                for (var index = 1; index <= 4; index++)
                {
                    var column = $"para{index}";
                    if (!table.ContainsColumn(column))
                    {
                        continue;
                    }

                    var raw = table.GetString(i, column, string.Empty);
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        continue;
                    }

                    if (int.TryParse(raw, out var value))
                    {
                        parameters.Add(value);
                    }
                }

                var actionId = table.GetInt(i, "action");
                var definition = new TriggerDefinition(triggerId, debugName, triggerType, targetTag, camp, parameters, actionId);
                dict[triggerId] = definition;
            }

            return dict;
        }

        private Dictionary<int, EffectDefinition> LoadEffects()
        {
            var path = Path.Combine(_configRoot, "ECSEffect.csv");
            var table = SimpleCsvTable.Load(path, "effect_id");
            var dict = new Dictionary<int, EffectDefinition>();
            for (var i = 0; i < table.Rows.Count; i++)
            {
                var effectId = table.GetInt(i, "effect_id");
                if (effectId == 0)
                {
                    continue;
                }

                var debugName = table.GetString(i, "debug_effect");
                var targetType = table.GetInt(i, "target_type");
                var operationEnum = table.GetInt(i, "oper_enum");
                var parameters = new List<double>();
                for (var index = 0; index <= 10; index++)
                {
                    var column = $"para{index}";
                    if (!table.ContainsColumn(column))
                    {
                        continue;
                    }

                    var raw = table.GetString(i, column, string.Empty);
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        continue;
                    }

                    if (double.TryParse(raw, out var value))
                    {
                        parameters.Add(value);
                    }
                }

                var logicParam = table.GetDoubleArray(i, "logic_param");
                var strParam1 = table.GetString(i, "str_param1");
                var strParam2 = table.GetString(i, "str_param2");
                var strParam3 = table.GetString(i, "str_param3");
                var arrParam1 = table.GetDoubleArray(i, "arr_param1");
                var arrParam2 = table.GetDoubleArray(i, "arr_param2");
                var strArray = table.GetStringArray(i, "str_arr");

                var strParams = new List<string>();
                if (!string.IsNullOrWhiteSpace(strParam1))
                {
                    strParams.Add(strParam1);
                }

                if (!string.IsNullOrWhiteSpace(strParam2))
                {
                    strParams.Add(strParam2);
                }

                if (!string.IsNullOrWhiteSpace(strParam3))
                {
                    strParams.Add(strParam3);
                }

                var mergedParameters = new List<double>(parameters);
                foreach (var logicValue in logicParam)
                {
                    mergedParameters.Add(logicValue);
                }

                var definition = new EffectDefinition(effectId, debugName, targetType, operationEnum, mergedParameters, arrParam1, arrParam2, strArray.Count > 0 ? strArray : strParams.ToArray());
                dict[effectId] = definition;
            }

            return dict;
        }
    }
}
