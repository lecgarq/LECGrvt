using LECG.Services.Interfaces;
using LECG.ViewModels;

namespace LECG.Services
{
    public class RenameRulePipelineService : IRenameRulePipelineService
    {
        public string ApplyRules(string text, SearchReplaceViewModel vm, int index)
        {
            string result = text;
            result = vm.RemoveRule.Apply(result, index);
            result = vm.ReplaceRule.Apply(result, index);
            result = vm.CaseRule.Apply(result, index);
            result = vm.AddRule.Apply(result, index);
            result = vm.NumberingRule.Apply(result, index);
            return result;
        }
    }
}
