using LECG.Core.Rename;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public sealed class FormulaUpdateService : IFormulaUpdateService
    {
        public string UpdateFormula(string formula, string oldName, string newName)
        {
            return FormulaNameUpdater.UpdateFormula(formula, oldName, newName);
        }
    }
}
