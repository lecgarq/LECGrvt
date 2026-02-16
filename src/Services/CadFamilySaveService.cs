using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadFamilySaveService : ICadFamilySaveService
    {
        public string Save(Document familyDoc, string name)
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), name + ".rfa");
            SaveAsOptions opt = new SaveAsOptions { OverwriteExistingFile = true };
            familyDoc.SaveAs(path, opt);
            familyDoc.Close(false);
            return path;
        }
    }
}
