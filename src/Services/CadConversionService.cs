#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using LECG.Core;
using LECG.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Services
{
    public class CadConversionService : ICadConversionService
    {
        private readonly ICadPlacementViewService _placementViewService;

        public CadConversionService(ICadPlacementViewService placementViewService)
        {
            _placementViewService = placementViewService;
        }

        private class CadData
        {
            public List<Curve> Curves { get; } = new List<Curve>();
            public List<HatchData> Hatches { get; } = new List<HatchData>();
        }

        private class HatchData
        {
            public Color Color { get; set; }
            public List<CurveLoop> Loops { get; set; }
        }

        public string GetDefaultTemplatePath()
        {
            return @"C:\ProgramData\Autodesk\RVT 2026\Family Templates\English\LECG\-\LECG_046_DETAIL-ITEM.rft";
        }

        public ElementId ConvertCadToFamily(Document doc, ImportInstance cadInstance, string familyName, string templatePath, string lineStyleName, Color lineColor, int lineWeight, Action<double, string>? progress = null)
        {
            if (cadInstance == null) return ElementId.InvalidElementId;

            progress?.Invoke(10, "Extracting geometry from CAD...");
            CadData data = ExtractGeometry(doc, cadInstance);
            
            progress?.Invoke(30, "Optimizing geometry...");
            CadData optimizedData = OptimizeGeometry(data);

            if (!optimizedData.Curves.Any() && !optimizedData.Hatches.Any())
                throw new Exception("No suitable geometry found in the selected CAD.");

            BoundingBoxXYZ box = cadInstance.get_BoundingBox(null);
            XYZ center = (box.Min + box.Max) * 0.5;

            progress?.Invoke(50, "Creating family document...");
            Document familyDoc = doc.Application.NewFamilyDocument(templatePath);
            using (Transaction t = new Transaction(familyDoc, "Create Detail Item Content"))
            {
                t.Start();
                DrawData(familyDoc, optimizedData, center, lineStyleName, lineColor, lineWeight, progress, 50, 80);
                t.Commit();
            }

            progress?.Invoke(90, "Saving and loading family...");
            string path = SaveFamily(familyDoc, familyName);
            return LoadAndPlace(doc, path, center, cadInstance.Id);
        }

        public ElementId ConvertDwgToFamily(Document doc, string dwgPath, string familyName, string templatePath, string lineStyleName, Color lineColor, int lineWeight, Action<double, string>? progress = null)
        {
            progress?.Invoke(5, "Initializing temporary document...");
            Document tempDoc = doc.Application.NewFamilyDocument(templatePath);
            CadData data;
            
            using (Transaction t = new Transaction(tempDoc, "Temp Import"))
            {
                t.Start();
                DWGImportOptions opt = new DWGImportOptions { Placement = ImportPlacement.Centered, ColorMode = ImportColorMode.Preserved, Unit = ImportUnit.Default };
                View importView = new FilteredElementCollector(tempDoc).OfClass(typeof(View)).Cast<View>().FirstOrDefault(v => v.ViewType == ViewType.FloorPlan && !v.IsTemplate);

                progress?.Invoke(15, "Importing DWG file...");
                ElementId impId;
                bool success = tempDoc.Import(dwgPath, opt, importView, out impId);
                if (!success || impId == ElementId.InvalidElementId) throw new Exception("DWG Import failed.");

                ImportInstance imp = tempDoc.GetElement(impId) as ImportInstance;
                progress?.Invoke(30, "Extracting geometry...");
                data = ExtractGeometry(tempDoc, imp);
                t.RollBack(); 
            }
            tempDoc.Close(false);

            if (data == null || (!data.Curves.Any() && !data.Hatches.Any()))
                 throw new Exception("No geometry found in DWG.");

            progress?.Invoke(50, "Creating final family...");
            Document familyDoc = doc.Application.NewFamilyDocument(templatePath);
            using (Transaction t = new Transaction(familyDoc, "Create Detail Item"))
            {
                t.Start();
                DrawData(familyDoc, data, XYZ.Zero, lineStyleName, lineColor, lineWeight, progress, 50, 90);
                t.Commit();
            }

            progress?.Invoke(95, "Loading into project...");
            string path = SaveFamily(familyDoc, familyName);
            return LoadOnly(doc, path);
        }
        
        private CadData OptimizeGeometry(CadData input)
        {
            CadData output = new CadData();
            output.Hatches.AddRange(input.Hatches); 

            // 1. Flatten all curves first to ensure 2D processing
            List<Curve> flatCurves = new List<Curve>();
            foreach(var c in input.Curves)
            {
                IEnumerable<Curve> flats = FlattenCurve(c);
                if (flats != null) flatCurves.AddRange(flats);
            }

            // 2. Separate Lines and Others
            List<Line> lines = flatCurves.OfType<Line>().ToList();
            List<Curve> others = flatCurves.Where(c => !(c is Line)).ToList();

            // 3. Overkill (Merge Collinear Lines)
            List<Line> mergedLines = MergeCollinearLines(lines);

            output.Curves.AddRange(mergedLines);
            output.Curves.AddRange(others);
            
            return output;
        }

        private List<Line> MergeCollinearLines(List<Line> sourceLines)
        {
            if (sourceLines.Count == 0) return new List<Line>();
            
            var merged = new List<Line>();
            var groupedByDir = sourceLines.GroupBy(l => RoundVector(l.Direction));

            foreach (var dirGroup in groupedByDir)
            {
                XYZ dir = dirGroup.Key; // Unit vector
                XYZ normal = new XYZ(-dir.Y, dir.X, 0);
                
                var groupedByIntercept = dirGroup.GroupBy(l => Math.Round(l.GetEndPoint(0).DotProduct(normal), 4));

                foreach (var interceptGroup in groupedByIntercept)
                {
                    var intervals = interceptGroup.Select(l => 
                    {
                        double s = l.GetEndPoint(0).DotProduct(dir);
                        double e = l.GetEndPoint(1).DotProduct(dir);
                        return s < e ? (Start: s, End: e) : (Start: e, End: s);
                    }).OrderBy(i => i.Start).ToList();

                    if (intervals.Count == 0) continue;

                    double currentStart = intervals[0].Start;
                    double currentEnd = intervals[0].End;

                    for (int i = 1; i < intervals.Count; i++)
                    {
                        if (intervals[i].Start <= currentEnd + 0.001) // Overlap or touching
                        {
                            currentEnd = Math.Max(currentEnd, intervals[i].End);
                        }
                        else
                        {
                            merged.Add(CreateLineFromProjection(dir, normal, interceptGroup.Key, currentStart, currentEnd));
                            currentStart = intervals[i].Start;
                            currentEnd = intervals[i].End;
                        }
                    }
                    merged.Add(CreateLineFromProjection(dir, normal, interceptGroup.Key, currentStart, currentEnd));
                }
            }
            return merged;
        }

        private Line CreateLineFromProjection(XYZ dir, XYZ normal, double intercept, double startProj, double endProj)
        {
            XYZ p1 = (normal * intercept) + (dir * startProj);
            XYZ p2 = (normal * intercept) + (dir * endProj);
            return Line.CreateBound(p1, p2);
        }

        private XYZ RoundVector(XYZ v)
        {
            return new XYZ(Math.Round(v.X, 4), Math.Round(v.Y, 4), Math.Round(v.Z, 4)).Normalize();
        }

        private ElementId LoadOnly(Document doc, string path)
        {
            ElementId createdId = ElementId.InvalidElementId;
            using (Transaction t = new Transaction(doc, "Load Family"))
            {
                t.Start();
                Family f;
                doc.LoadFamily(path, new FamilyOption(), out f);
                if (f != null) 
                { 
                    FamilySymbol s = ActivateSymbol(doc, f); 
                    if (s != null) createdId = s.Id;
                }
                t.Commit();
            }
            CleanupFile(path);
            return createdId;
        }

        private ElementId LoadAndPlace(Document doc, string path, XYZ location, ElementId deleteId)
        {
            ElementId createdId = ElementId.InvalidElementId;
            using (Transaction t = new Transaction(doc, "Load and Place Detail Item"))
            {
                t.Start();
                Family family = null;
                doc.LoadFamily(path, new FamilyOption(), out family);

                if (family != null)
                {
                    FamilySymbol symbol = ActivateSymbol(doc, family);
                    if (symbol != null)
                    {
                        createdId = symbol.Id;
                        // Find a valid placement view (Detail Items MUST be in 2D)
                        View placementView = _placementViewService.ResolvePlacementView(doc, doc.ActiveView);

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
                             // Fallback to global creation if no view is active/valid
                             doc.Create.NewFamilyInstance(location, symbol, StructuralType.NonStructural);
                        }
                    }
                }
                t.Commit();
            }
            CleanupFile(path);
            return createdId;
        }

        private FamilySymbol ActivateSymbol(Document doc, Family family)
        {
            ElementId id = family.GetFamilySymbolIds().FirstOrDefault();
            if (id == null) return null;
            FamilySymbol symbol = doc.GetElement(id) as FamilySymbol;
            if (symbol != null && !symbol.IsActive) symbol.Activate();
            return symbol;
        }

        private void CleanupFile(string path) { try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); } catch { } }

        private string SaveFamily(Document familyDoc, string name)
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), name + ".rfa");
            SaveAsOptions opt = new SaveAsOptions { OverwriteExistingFile = true };
            familyDoc.SaveAs(path, opt);
            familyDoc.Close(false);
            return path;
        }

        private void DrawData(Document familyDoc, CadData data, XYZ offset, string styleName, Color color, int weight, Action<double, string>? progress = null, double startPct = 50, double endPct = 90)
        {
            Category detailCat = familyDoc.Settings.Categories.get_Item(BuiltInCategory.OST_DetailComponents);
            Category subCat = detailCat.SubCategories.Contains(styleName) ? detailCat.SubCategories.get_Item(styleName) : familyDoc.Settings.Categories.NewSubcategory(detailCat, styleName);
            subCat.LineColor = color;
            subCat.SetLineWeight(weight, GraphicsStyleType.Projection);
            GraphicsStyle lineStyle = subCat.GetGraphicsStyle(GraphicsStyleType.Projection);

            View? planView = new FilteredElementCollector(familyDoc)
                   .OfClass(typeof(View)).Cast<View>()
                   .FirstOrDefault(v => !v.IsTemplate && v.ViewType == ViewType.FloorPlan) 
                   ?? new FilteredElementCollector(familyDoc).OfClass(typeof(View)).Cast<View>().FirstOrDefault(v => !v.IsTemplate && v.CanBePrinted);

            if (planView == null)
                throw new Exception("Could not find a valid plan view in the family template to draw geometry.");

            Transform toOrigin = Transform.CreateTranslation(-offset);
            
            int total = data.Curves.Count + data.Hatches.Count;
            int current = 0;

            foreach (Curve c in data.Curves)
            {
                current++;
                if (total > 0 && (current % Math.Max(1, total / 20) == 0 || current == total)) progress?.Invoke(startPct + (endPct - startPct) * current / total, $"Drawing curves... ({current}/{data.Curves.Count})");
                try
                {
                    Curve transCurve = c.CreateTransformed(toOrigin);
                    IEnumerable<Curve> flats = FlattenCurve(transCurve);
                    
                    if (flats != null)
                    {
                        foreach(var flat in flats)
                        {
                             DetailCurve dc = familyDoc.FamilyCreate.NewDetailCurve(planView, flat);
                             if (dc != null) dc.LineStyle = lineStyle;
                        }
                    }
                }
                catch { }
            }

            foreach (var hatch in data.Hatches)
            {
                current++;
                if (total > 0 && (current % Math.Max(1, total / 20) == 0 || current == total)) progress?.Invoke(startPct + (endPct - startPct) * current / total, $"Drawing hatches... ({current - data.Curves.Count}/{data.Hatches.Count})");
                try
                {
                    FilledRegionType frType = GetOrCreateFilledRegionType(familyDoc, hatch.Color);
                    List<CurveLoop> validLoops = new List<CurveLoop>();
                    
                    foreach (var loop in hatch.Loops)
                    {
                        CurveLoop newLoop = new CurveLoop();
                        foreach (Curve c in loop)
                        {
                            Curve transCurve = c.CreateTransformed(toOrigin);
                            IEnumerable<Curve> flats = FlattenCurve(transCurve);
                            if (flats != null) 
                            {
                                foreach(var flat in flats) newLoop.Append(flat);
                            }
                        }
                        
                        if (!newLoop.IsOpen() && newLoop.GetExactLength() > 0.001)
                            validLoops.Add(newLoop);
                    }

                    if (validLoops.Any())
                        FilledRegion.Create(familyDoc, frType.Id, planView.Id, validLoops);
                }
                catch { }
            }
        }

        private CadData ExtractGeometry(Document doc, ImportInstance imp)
        {
            CadData data = new CadData();
            GeometryElement geoElem = imp.get_Geometry(new Options());

            if (geoElem != null)
            {
                foreach (GeometryObject obj in geoElem)
                {
                    ProcessGeometryObject(obj, data, doc, Transform.Identity);
                }
            }
            return data;
        }

        private void ProcessGeometryObject(GeometryObject obj, CadData data, Document doc, Transform currentTransform)
        {
            if (obj is GeometryInstance geoInst)
            {
                Transform instTransform = currentTransform.Multiply(geoInst.Transform);
                GeometryElement symbolGeo = geoInst.GetSymbolGeometry();
                foreach (GeometryObject childObj in symbolGeo)
                {
                    ProcessGeometryObject(childObj, data, doc, instTransform);
                }
            }
            else if (obj is Curve crv)
            {
                data.Curves.Add(crv.CreateTransformed(currentTransform));
            }
            else if (obj is PolyLine poly)
            {
                IList<XYZ> points = poly.GetCoordinates();
                for (int i = 0; i < points.Count - 1; i++)
                {
                    XYZ p1 = currentTransform.OfPoint(points[i]);
                    XYZ p2 = currentTransform.OfPoint(points[i+1]);
                    data.Curves.Add(Line.CreateBound(p1, p2));
                }
            }
            else if (obj is Solid solid && !solid.Faces.IsEmpty)
            {
                Color c = GetColor(doc, obj.GraphicsStyleId);
                foreach (Face face in solid.Faces)
                {
                    if (face is PlanarFace pf)
                    {
                        XYZ normal = currentTransform.OfVector(pf.FaceNormal).Normalize();
                        if (normal.IsAlmostEqualTo(XYZ.BasisZ) || normal.IsAlmostEqualTo(-XYZ.BasisZ))
                        {
                            var loops = pf.GetEdgesAsCurveLoops();
                            List<CurveLoop> transformedLoops = new List<CurveLoop>();
                            foreach(CurveLoop loop in loops)
                            {
                                CurveLoop tLoop = new CurveLoop();
                                foreach(Curve loopCrv in loop)
                                {
                                    tLoop.Append(loopCrv.CreateTransformed(currentTransform));
                                }
                                transformedLoops.Add(tLoop);
                            }
                            if (transformedLoops.Count > 0)
                               data.Hatches.Add(new HatchData { Color = c, Loops = transformedLoops });
                        }
                    }
                }
            }
        }

        private Color GetColor(Document doc, ElementId gsId)
        {
            if (gsId == ElementId.InvalidElementId) return new Color(0,0,0);
            if (doc.GetElement(gsId) is GraphicsStyle gs && gs.GraphicsStyleCategory != null) return gs.GraphicsStyleCategory.LineColor;
            return new Color(0,0,0);
        }

        private FilledRegionType GetOrCreateFilledRegionType(Document doc, Color color)
        {
            string name = $"Solid_{color.Red}_{color.Green}_{color.Blue}";
            FilledRegionType existing = new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType)).Cast<FilledRegionType>().FirstOrDefault(x => x.Name == name);
            if (existing != null) return existing;

            FilledRegionType newType = null;
            var anyType = new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType)).FirstElement() as FilledRegionType;
            if (anyType != null)
            {
                newType = anyType.Duplicate(name) as FilledRegionType;
                newType.ForegroundPatternColor = color;
                newType.BackgroundPatternColor = color; 

                FillPatternElement solidPattern = FillPatternElement.GetFillPatternElementByName(doc, FillPatternTarget.Drafting, "<Solid fill>"); 
                if (solidPattern == null) solidPattern = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>().FirstOrDefault(p => p.GetFillPattern().IsSolidFill);

                if (solidPattern != null)
                {
                    newType.ForegroundPatternId = solidPattern.Id;
                    newType.BackgroundPatternId = solidPattern.Id;
                }
            }
            return newType;
        }

        private IEnumerable<Curve> FlattenCurve(Curve c)
        {
            try
            {
                if (c is Line l)
                {
                    XYZ p0 = Flatten(l.GetEndPoint(0));
                    XYZ p1 = Flatten(l.GetEndPoint(1));
                    if (p0.IsAlmostEqualTo(p1)) return null;
                    return new List<Curve> { Line.CreateBound(p0, p1) };
                }
                else if (c is Arc a)
                {
                    XYZ p0 = Flatten(a.GetEndPoint(0));
                    XYZ p1 = Flatten(a.GetEndPoint(1));
                    XYZ mid = Flatten(a.Evaluate(0.5, true));
                    
                    if (p0.IsAlmostEqualTo(p1))
                    {
                         XYZ center = Flatten(a.Center);
                         if (a.Radius < 0.001) return null;
                         return new List<Curve> { Arc.Create(center, a.Radius, 0, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY) };
                    }
                    return new List<Curve> { Arc.Create(p0, p1, mid) };
                }
                else if (c is Ellipse e)
                {
                    return new List<Curve> { Ellipse.CreateCurve(Flatten(e.Center), e.RadiusX, e.RadiusY, XYZ.BasisX, XYZ.BasisY, e.GetEndParameter(0), e.GetEndParameter(1)) };
                }
                else if (c is HermiteSpline hs)
                {
                     IList<XYZ> pts = hs.ControlPoints.Select(p => Flatten(p)).ToList();
                     var cleanPts = new List<XYZ> { pts[0] };
                     for (int i = 1; i < pts.Count; i++)
                     {
                         if (!pts[i].IsAlmostEqualTo(cleanPts.Last(), 0.001)) cleanPts.Add(pts[i]);
                     }
                     if (cleanPts.Count >= 2) 
                         return new List<Curve> { HermiteSpline.Create(cleanPts, hs.IsPeriodic) };
                     else 
                         return Tessellate(c);
                }
                else if (c is NurbSpline ns)
                {
                    // Attempt to maintain Nurb structure first?
                    // Flat control points
                    IList<XYZ> ctrls = ns.CtrlPoints.Select(p => Flatten(p)).ToList();
                    try 
                    {
                        return new List<Curve> { NurbSpline.CreateCurve(ns.Degree, ToList(ns.Knots), ctrls, ToList(ns.Weights)) };
                    }
                    catch 
                    {
                        return Tessellate(c);
                    }
                }
                else
                {
                    // Fallback (Helix, etc.)
                    return Tessellate(c);
                }
            }
            catch 
            {
                return Tessellate(c);
            }
        }

        private IList<double> ToList(DoubleArray da)
        {
            var list = new List<double>();
            for (int i = 0; i < da.Size; i++)
            {
                list.Add(da.get_Item(i));
            }
            return list;
        }
        
        private IEnumerable<Curve> Tessellate(Curve c)
        {
             IList<XYZ> points = c.Tessellate();
             if (points.Count < 2) return null;
             
             List<Curve> lines = new List<Curve>();
             for (int i = 0; i < points.Count - 1; i++)
             {
                 XYZ p1 = Flatten(points[i]);
                 XYZ p2 = Flatten(points[i+1]);
                 if (!p1.IsAlmostEqualTo(p2))
                     lines.Add(Line.CreateBound(p1, p2));
             }
             return lines;
        }

        private XYZ Flatten(XYZ p) { return new XYZ(p.X, p.Y, 0); }

        class FamilyOption : IFamilyLoadOptions
        {
            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues) { overwriteParameterValues = true; return true; }
            public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues) { source = FamilySource.Family; overwriteParameterValues = true; return true; }
        }
    }
}
