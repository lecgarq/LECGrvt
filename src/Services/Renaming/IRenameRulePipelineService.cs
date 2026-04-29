using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface IRenameRulePipelineService
    {
        string ApplyRules(string text, RenameRuleContext context, int index);
    }
}
