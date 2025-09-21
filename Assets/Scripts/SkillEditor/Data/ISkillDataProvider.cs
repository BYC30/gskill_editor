using System.Threading.Tasks;

namespace GSkill.SkillEditor.Data
{
    /// <summary>
    /// 定义技能数据的读写入口，便于适配 CSV、ScriptableObject 等多种来源。
    /// </summary>
    public interface ISkillDataProvider
    {
        SkillDatabase Load();
        Task<SkillDatabase> LoadAsync();
        void Save(SkillDatabase database);
        Task SaveAsync(SkillDatabase database);
    }
}
