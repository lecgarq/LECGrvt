namespace LECG.Services.Interfaces
{
    public interface IFormulaUpdateService
    {
        string UpdateFormula(string formula, string oldName, string newName);
    }
}
