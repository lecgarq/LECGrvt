using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Analysis;
using Autodesk.Revit.DB.Structure;

namespace LECG.RevitCopilot.SmokeTests;

internal static class CoverageFixtures
{
    // Only called after SampleBenchmark verifies a detached, unloaded-link COPY. Never saved back to a sample.
    internal static object[] Create(Document doc)
    {
        List<object> outcomes = [];
        void CreateOne(string name, Action create)
        {
            using var tx = new Transaction(doc, "Expanded coverage fixture: " + name);
            tx.Start();
            tx.SetFailureHandlingOptions(tx.GetFailureHandlingOptions().SetClearAfterRollback(true).SetFailuresPreprocessor(new RollbackFailures()));
            try
            {
                create();
                doc.Regenerate();
                if (tx.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Fixture transaction did not commit.");
                outcomes.Add(new { fixture = name, status = "created" });
            }
            catch (Exception ex)
            {
                if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack();
                outcomes.Add(new { fixture = name, status = "unavailable", error = ex.Message });
            }
            if (doc.IsModifiable) throw new InvalidOperationException("Fixture left a transaction open.");
        }
        string suffix = Guid.NewGuid().ToString("N")[..8];
        CreateOne("DirectShape and type", () =>
        {
            var category = new ElementId(BuiltInCategory.OST_GenericModel);
            DirectShape shape = DirectShape.CreateElement(doc, category);
            shape.ApplicationId = "LECG-disposable-fixture";
            shape.ApplicationDataId = suffix;
            DirectShapeType.Create(doc, "LECG coverage " + suffix, category);
        });
        CreateOne("SpatialFieldManager", () =>
        {
            var type = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().First(t => t.ViewFamily == ViewFamily.ThreeDimensional);
            View3D view = View3D.CreateIsometric(doc, type.Id);
            view.Name = "LECG analysis fixture " + suffix;
            var manager = SpatialFieldManager.CreateSpatialFieldManager(view, 2);
            manager.SetMeasurementNames(new List<string> { "Fixture A", "Fixture B" });
            int schema = manager.RegisterResult(new AnalysisResultSchema("LECG fixture", "Disposable two-measurement analysis"));
            int primitive = manager.AddSpatialFieldPrimitive();
            manager.UpdateSpatialFieldPrimitive(primitive, new FieldDomainPointsByXYZ(new List<XYZ> { new(2000, 2000, 100) }),
                new FieldValues(new List<ValueAtPoint> { new(new List<double> { 1, 2 }) }), schema);
        });
        CreateOne("Analytical line and point loads", () =>
        {
            var curve = Line.CreateBound(new XYZ(2000, 2000, 100), new XYZ(2010, 2000, 100));
            AnalyticalMember member = AnalyticalMember.Create(doc, curve);
            LineLoad.Create(doc, member.Id, XYZ.BasisZ, XYZ.BasisX, null);
            PointLoad.Create(doc, member.Id, AnalyticalElementSelector.StartOrBase, XYZ.BasisZ, XYZ.BasisX, null);
        });
        CreateOne("LoadCombination", () => LoadCombination.Create(doc, "LECG load fixture " + suffix));
        CreateOne("IFC parameter mapping", () => IFCParameterTemplate.Create(doc, "LECG parameter fixture " + suffix));
        CreateOne("IFC category mapping", () => IFCCategoryTemplate.Create(doc, "LECG category fixture " + suffix));
        CreateOne("Sheet view position", () => ViewPosition.Create(doc, "LECG position fixture " + suffix, new XYZ(1, 1, 0), Enum.GetValues<ViewAnchor>().First()));
        CreateOne("Schedule with adjustable row height", () =>
        {
            var schedule = ViewSchedule.CreateSchedule(doc, new ElementId(BuiltInCategory.OST_Walls));
            schedule.Name = "LECG row-height fixture " + suffix;
            schedule.Definition.AddField(ScheduleFieldType.Instance, new ElementId(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS));
            schedule.RowHeightOverride = RowHeightOverrideOptions.All;
        });
        return outcomes.ToArray();
    }
    private sealed class RollbackFailures : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor) => failuresAccessor.GetFailureMessages().Count > 0
            ? FailureProcessingResult.ProceedWithRollBack : FailureProcessingResult.Continue;
    }
}
