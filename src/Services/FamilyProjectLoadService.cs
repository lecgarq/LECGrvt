using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Services
{
    public class FamilyProjectLoadService : IFamilyProjectLoadService
    {
        private readonly IFamilyLoadOptionsFactory _familyLoadOptionsFactory;

        public FamilyProjectLoadService(IFamilyLoadOptionsFactory familyLoadOptionsFactory)
        {
            _familyLoadOptionsFactory = familyLoadOptionsFactory;
        }

        public void Load(Document doc, string tempFamilyPath)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(tempFamilyPath);

            using (Transaction tProject = new Transaction(doc, "Load Converted Family"))
            {
                tProject.Start();

                Family? loadedFamily = null;
                doc.LoadFamily(tempFamilyPath, _familyLoadOptionsFactory.Create(), out loadedFamily);

                if (loadedFamily != null)
                {
                    Logger.Instance.Log($"Success! Loaded family: {loadedFamily.Name}");
                    Logger.Instance.UpdateProgress(100, "Done");
                }
                else
                {
                    Logger.Instance.Log("Warning: Family loaded but returned null (already existed?).");
                }

                tProject.Commit();
            }
        }
    }
}
