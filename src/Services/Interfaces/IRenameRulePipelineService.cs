using LECG.ViewModels;

namespace LECG.Services.Interfaces
{
    public interface IRenameRulePipelineService
    {
        string ApplyRules(string text, SearchReplaceViewModel vm, int index);
    }
}
