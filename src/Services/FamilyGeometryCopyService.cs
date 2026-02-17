using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System.Collections.Generic;

namespace LECG.Services
{
    public class FamilyGeometryCopyService : IFamilyGeometryCopyService
    {
        private readonly IFamilyGeometryCollectionService _geometryCollectionService;
        private readonly IFamilyParameterSetupService _familyParameterSetupService;

        public FamilyGeometryCopyService(
            IFamilyGeometryCollectionService geometryCollectionService,
            IFamilyParameterSetupService familyParameterSetupService)
        {
            _geometryCollectionService = geometryCollectionService;
            _familyParameterSetupService = familyParameterSetupService;
        }

        public int CopyGeometry(Document sourceFamilyDoc, Document targetFamilyDoc)
        {
            List<ElementId> idsToCopy = _geometryCollectionService.CollectGeometryElementIds(sourceFamilyDoc);

            using (Transaction tTarget = new Transaction(targetFamilyDoc, "Copy Geometry"))
            {
                tTarget.Start();

                _familyParameterSetupService.ConfigureTargetFamilyParameters(targetFamilyDoc);

                if (idsToCopy.Count > 0)
                {
                    CopyPasteOptions options = new CopyPasteOptions();
                    ElementTransformUtils.CopyElements(sourceFamilyDoc, idsToCopy, targetFamilyDoc, Transform.Identity, options);
                }

                tTarget.Commit();
            }

            return idsToCopy.Count;
        }
    }
}
