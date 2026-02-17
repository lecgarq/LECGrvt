using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadFamilyLoadPlacementService : ICadFamilyLoadPlacementService
    {
        private readonly ICadFamilySymbolService _familySymbolService;
        private readonly ICadPlacementViewService _placementViewService;
        private readonly IFamilyLoadOptionsFactory _familyLoadOptionsFactory;

        public CadFamilyLoadPlacementService(
            ICadFamilySymbolService familySymbolService,
            ICadPlacementViewService placementViewService,
            IFamilyLoadOptionsFactory familyLoadOptionsFactory)
        {
            _familySymbolService = familySymbolService;
            _placementViewService = placementViewService;
            _familyLoadOptionsFactory = familyLoadOptionsFactory;
        }

        public ElementId LoadOnly(Document doc, string path)
        {
            ElementId createdId = ElementId.InvalidElementId;
            using (Transaction t = new Transaction(doc, "Load Family"))
            {
                t.Start();
                Family? f;
                doc.LoadFamily(path, _familyLoadOptionsFactory.Create(), out f);
                if (f != null)
                {
                    FamilySymbol? s = _familySymbolService.GetPrimarySymbol(doc, f);
                    if (s != null) createdId = s.Id;
                }
                t.Commit();
            }
            CleanupFile(path);
            return createdId;
        }

        public ElementId LoadAndPlace(Document doc, string path, XYZ location, ElementId deleteId)
        {
            ElementId createdId = ElementId.InvalidElementId;
            using (Transaction t = new Transaction(doc, "Load and Place Detail Item"))
            {
                t.Start();
                Family? family = null;
                doc.LoadFamily(path, _familyLoadOptionsFactory.Create(), out family);

                if (family != null)
                {
                    FamilySymbol? symbol = _familySymbolService.GetPrimarySymbol(doc, family);
                    if (symbol != null)
                    {
                        createdId = symbol.Id;
                        View? placementView = _placementViewService.ResolvePlacementView(doc, doc.ActiveView);

                        if (placementView != null)
                        {
                            doc.Create.NewFamilyInstance(location, symbol, placementView);

                            if (deleteId != null && deleteId != ElementId.InvalidElementId)
                            {
                                Element e = doc.GetElement(deleteId);
                                if (e != null)
                                {
                                    if (e.Pinned) e.Pinned = false;
                                    doc.Delete(deleteId);
                                }
                            }
                        }
                        else
                        {
                            doc.Create.NewFamilyInstance(location, symbol, StructuralType.NonStructural);
                        }
                    }
                }
                t.Commit();
            }
            CleanupFile(path);
            return createdId;
        }

        private void CleanupFile(string path)
        {
            try
            {
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
            catch
            {
            }
        }

    }
}
