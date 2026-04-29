using LECG.Services.Interfaces;
using LECG.Models;

namespace LECG.Services
{
    public class RenameRulePipelineService : IRenameRulePipelineService
    {
        public string ApplyRules(string text, RenameRuleContext context, int index)
        {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentNullException.ThrowIfNull(context);

            string result = text;
            result = context.RemoveRule.Apply(result, index);
            result = context.ReplaceRule.Apply(result, index);
            result = context.CaseRule.Apply(result, index);
            result = context.AddRule.Apply(result, index);
            result = context.NumberingRule.Apply(result, index);
            return result;
        }
    }
}
