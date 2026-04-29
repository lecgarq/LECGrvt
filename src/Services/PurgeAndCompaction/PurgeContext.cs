using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public sealed class PurgeContext
    {
        private PurgeContext(
            IReadOnlyList<Level> levels,
            HashSet<ElementId> parameterReferencedIds,
            HashSet<ElementId> usedLineStyleIds,
            HashSet<ElementId> usedLinePatternIds,
            HashSet<ElementId> usedFillPatternIds,
            HashSet<ElementId> usedMaterialIds,
            HashSet<ElementId> placedLevelIds)
        {
            Levels = levels;
            ParameterReferencedIds = parameterReferencedIds;
            UsedLineStyleIds = usedLineStyleIds;
            UsedLinePatternIds = usedLinePatternIds;
            UsedFillPatternIds = usedFillPatternIds;
            UsedMaterialIds = usedMaterialIds;
            PlacedLevelIds = placedLevelIds;
        }

        public IReadOnlyList<Level> Levels { get; }
        public HashSet<ElementId> ParameterReferencedIds { get; }
        public HashSet<ElementId> UsedLineStyleIds { get; }
        public HashSet<ElementId> UsedLinePatternIds { get; }
        public HashSet<ElementId> UsedFillPatternIds { get; }
        public HashSet<ElementId> UsedMaterialIds { get; }
        public HashSet<ElementId> PlacedLevelIds { get; }

        public static PurgeContext Create(Document doc)
        {
            ArgumentNullException.ThrowIfNull(doc);

            var parameterReferencedIds = new HashSet<ElementId>();
            var usedLineStyleIds = new HashSet<ElementId>();
            var usedLinePatternIds = new HashSet<ElementId>();
            var usedFillPatternIds = new HashSet<ElementId>();
            var usedMaterialIds = new HashSet<ElementId>();
            var placedLevelIds = new HashSet<ElementId>();

            // Scan element types for parameter references — types are few (hundreds, not hundreds
            // of thousands) so the per-parameter native calls are safe. Instance elements are NOT
            // scanned for parameters: doing so on 200K-500K elements causes native access violations
            // in Revit 2026.4 (same root cause as the CompactingStyles crash).
            CollectTypeReferences(doc, parameterReferencedIds, usedMaterialIds);

            // Targeted instance collection — collect specific properties without scanning all parameters.
            CollectInstanceReferences(doc, usedMaterialIds, usedLineStyleIds, placedLevelIds);

            // Targeted instance material parameter scan — catches materials stored in
            // parameters but not applied to geometry (e.g. scheduling-only material params).
            // Uses direct get_Parameter() and ElementParameterFilter to avoid iterating
            // element.Parameters on instances (which crashes Revit 2026.4 on large models).
            CollectInstanceMaterialParameterReferences(doc, usedMaterialIds);

            // Scan Object Styles categories and subcategories. Import families assign
            // materials, line patterns, and line styles to their subcategories via Category
            // objects — not Elements — so the element-based scans above never see them.
            CollectObjectStyleReferences(doc, usedMaterialIds, usedLineStyleIds, usedLinePatternIds, usedFillPatternIds);

            CollectFillPatternReferences(doc, usedFillPatternIds);

            IReadOnlyList<Level> levels = CollectLevels(doc);

            return new PurgeContext(
                levels,
                parameterReferencedIds,
                usedLineStyleIds,
                usedLinePatternIds,
                usedFillPatternIds,
                usedMaterialIds,
                placedLevelIds);
        }

        private static void AddIfValid(HashSet<ElementId> ids, ElementId id)
        {
            if (id != ElementId.InvalidElementId)
            {
                ids.Add(id);
            }
        }

        private static void CollectTypeReferences(
            Document doc,
            HashSet<ElementId> parameterReferencedIds,
            HashSet<ElementId> usedMaterialIds)
        {
            foreach (Element element in new FilteredElementCollector(doc).WhereElementIsElementType())
            {
                CollectParameterReferences(element, parameterReferencedIds);
                CollectMaterialIds(element, usedMaterialIds);
            }
        }

        private static void CollectInstanceReferences(
            Document doc,
            HashSet<ElementId> usedMaterialIds,
            HashSet<ElementId> usedLineStyleIds,
            HashSet<ElementId> placedLevelIds)
        {
            foreach (Element element in new FilteredElementCollector(doc).WhereElementIsNotElementType())
            {
                CollectMaterialIds(element, usedMaterialIds);
                CollectLineStyleId(element, usedLineStyleIds);
                CollectLevelId(element, placedLevelIds);
            }
        }

        private static void CollectFillPatternReferences(Document doc, HashSet<ElementId> usedFillPatternIds)
        {
            foreach (Material material in new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>())
            {
                AddIfValid(usedFillPatternIds, material.SurfaceForegroundPatternId);
                AddIfValid(usedFillPatternIds, material.SurfaceBackgroundPatternId);
                AddIfValid(usedFillPatternIds, material.CutForegroundPatternId);
                AddIfValid(usedFillPatternIds, material.CutBackgroundPatternId);
            }

            foreach (FilledRegionType filledRegionType in new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType)).Cast<FilledRegionType>())
            {
                AddIfValid(usedFillPatternIds, filledRegionType.ForegroundPatternId);
                AddIfValid(usedFillPatternIds, filledRegionType.BackgroundPatternId);
            }
        }

        private static IReadOnlyList<Level> CollectLevels(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();
        }

        private static void CollectParameterReferences(Element element, HashSet<ElementId> parameterReferencedIds)
        {
            RunGuarded(
                () =>
                {
                    foreach (Parameter parameter in element.Parameters)
                    {
                        if (parameter.StorageType != StorageType.ElementId)
                        {
                            continue;
                        }

                        AddIfValid(parameterReferencedIds, parameter.AsElementId());
                    }
                },
                $"[PurgeContext] Failed to collect parameter references for element {element.Id}");
        }

        private static void CollectMaterialIds(Element element, HashSet<ElementId> usedMaterialIds)
        {
            try
            {
                foreach (ElementId materialId in element.GetMaterialIds(false))
                {
                    AddIfValid(usedMaterialIds, materialId);
                }

                // Paint materials — applied via the Paint tool to individual faces.
                foreach (ElementId materialId in element.GetMaterialIds(true))
                {
                    AddIfValid(usedMaterialIds, materialId);
                }
            }
            catch (ArgumentException ex)
            {
                Logging.Logger.Instance.LogWarning($"[PurgeContext] Failed to collect material references for element {element.Id}: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Logging.Logger.Instance.LogWarning($"[PurgeContext] Failed to collect material references for element {element.Id}: {ex.Message}");
            }
            catch (RevitExceptions.InvalidOperationException ex)
            {
                Logging.Logger.Instance.LogWarning($"[PurgeContext] Failed to collect material references for element {element.Id}: {ex.Message}");
            }
        }

        private static void CollectLineStyleId(Element element, HashSet<ElementId> usedLineStyleIds)
        {
            if (element is not CurveElement curveElement)
            {
                return;
            }

            try
            {
                if (curveElement.LineStyle is GraphicsStyle graphicsStyle && graphicsStyle.GraphicsStyleCategory != null)
                {
                    AddIfValid(usedLineStyleIds, graphicsStyle.GraphicsStyleCategory.Id);
                }
            }
            catch (ArgumentException ex)
            {
                Logging.Logger.Instance.LogWarning($"[PurgeContext] Failed to collect line style for curve {element.Id}: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Logging.Logger.Instance.LogWarning($"[PurgeContext] Failed to collect line style for curve {element.Id}: {ex.Message}");
            }
            catch (RevitExceptions.InvalidOperationException ex)
            {
                Logging.Logger.Instance.LogWarning($"[PurgeContext] Failed to collect line style for curve {element.Id}: {ex.Message}");
            }
        }

        private static void CollectObjectStyleReferences(
            Document doc,
            HashSet<ElementId> usedMaterialIds,
            HashSet<ElementId> usedLineStyleIds,
            HashSet<ElementId> usedLinePatternIds,
            HashSet<ElementId> usedFillPatternIds)
        {
            try
            {
                foreach (Category category in doc.Settings.Categories)
                {
                    CollectCategoryReferences(category, usedMaterialIds, usedLineStyleIds, usedLinePatternIds, usedFillPatternIds);

                    CategoryNameMap subCategories = category.SubCategories;
                    if (subCategories == null) continue;

                    foreach (Category sub in subCategories)
                    {
                        CollectCategoryReferences(sub, usedMaterialIds, usedLineStyleIds, usedLinePatternIds, usedFillPatternIds);
                    }
                }
            }
            catch (ArgumentException ex)
            {
                Logging.Logger.Instance.LogWarning($"[PurgeContext] Failed to collect Object Style references: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Logging.Logger.Instance.LogWarning($"[PurgeContext] Failed to collect Object Style references: {ex.Message}");
            }
            catch (RevitExceptions.InvalidOperationException ex)
            {
                Logging.Logger.Instance.LogWarning($"[PurgeContext] Failed to collect Object Style references: {ex.Message}");
            }
        }

        private static void CollectCategoryReferences(
            Category category,
            HashSet<ElementId> usedMaterialIds,
            HashSet<ElementId> usedLineStyleIds,
            HashSet<ElementId> usedLinePatternIds,
            HashSet<ElementId> usedFillPatternIds)
        {
            try
            {
                // Material
                Material? mat = category.Material;
                if (mat != null) AddIfValid(usedMaterialIds, mat.Id);

                // Line patterns (projection + cut)
                AddIfValid(usedLinePatternIds, category.GetLinePatternId(GraphicsStyleType.Projection));
                AddIfValid(usedLinePatternIds, category.GetLinePatternId(GraphicsStyleType.Cut));

                // Line styles (the GraphicsStyle itself references a category id used by line style purge)
                GraphicsStyle? projStyle = category.GetGraphicsStyle(GraphicsStyleType.Projection);
                if (projStyle?.GraphicsStyleCategory != null)
                    AddIfValid(usedLineStyleIds, projStyle.GraphicsStyleCategory.Id);

                GraphicsStyle? cutStyle = category.GetGraphicsStyle(GraphicsStyleType.Cut);
                if (cutStyle?.GraphicsStyleCategory != null)
                    AddIfValid(usedLineStyleIds, cutStyle.GraphicsStyleCategory.Id);
            }
            catch (ArgumentException ex)
            {
                Logging.Logger.Instance.LogWarning($"[PurgeContext] Category property access: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Logging.Logger.Instance.LogWarning($"[PurgeContext] Category property access: {ex.Message}");
            }
            catch (RevitExceptions.InvalidOperationException ex)
            {
                Logging.Logger.Instance.LogWarning($"[PurgeContext] Category property access: {ex.Message}");
            }
        }

        private static void CollectInstanceMaterialParameterReferences(
            Document doc,
            HashSet<ElementId> usedMaterialIds)
        {
            var allMaterialIds = new HashSet<ElementId>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(Material))
                    .ToElementIds());

            if (allMaterialIds.Count == 0) return;

            CollectBuiltInMaterialParameterReferences(doc, allMaterialIds, usedMaterialIds);
            CollectSharedMaterialParameterReferences(doc, allMaterialIds, usedMaterialIds);
            CollectFamilyInstanceMaterialParameters(doc, allMaterialIds, usedMaterialIds);
        }

        private static void CollectBuiltInMaterialParameterReferences(
            Document doc,
            HashSet<ElementId> allMaterialIds,
            HashSet<ElementId> usedMaterialIds)
        {
            // Built-in parameters known to store material references on instances.
            // ElementParameterFilter lets Revit do the heavy filtering natively,
            // then we read only the matching parameter on each result.
            BuiltInParameter[] materialParams =
            {
                BuiltInParameter.MATERIAL_ID_PARAM,
                BuiltInParameter.STRUCTURAL_MATERIAL_PARAM,
            };

            foreach (BuiltInParameter bip in materialParams)
            {
                try
                {
                    var provider = new ParameterValueProvider(new ElementId(bip));
                    var rule = new FilterElementIdRule(
                        provider, new FilterNumericGreater(), ElementId.InvalidElementId);
                    var filter = new ElementParameterFilter(rule);

                    foreach (Element element in new FilteredElementCollector(doc)
                        .WhereElementIsNotElementType()
                        .WherePasses(filter))
                    {
                        try
                        {
                            Parameter p = element.get_Parameter(bip);
                            if (p?.HasValue == true && p.StorageType == StorageType.ElementId)
                            {
                                ElementId id = p.AsElementId();
                                if (allMaterialIds.Contains(id))
                                {
                                    usedMaterialIds.Add(id);
                                }
                            }
                        }
                        catch (ArgumentException ex) { Logging.Logger.Instance.LogWarning($"[PurgeContext] Built-in material param read for element {element.Id}: {ex.Message}"); }
                        catch (InvalidOperationException ex) { Logging.Logger.Instance.LogWarning($"[PurgeContext] Built-in material param read for element {element.Id}: {ex.Message}"); }
                        catch (RevitExceptions.InvalidOperationException ex) { Logging.Logger.Instance.LogWarning($"[PurgeContext] Built-in material param read for element {element.Id}: {ex.Message}"); }
                    }
                }
                catch (ArgumentException ex) { Logging.Logger.Instance.LogWarning($"[PurgeContext] Built-in material param filter for {bip}: {ex.Message}"); }
                catch (InvalidOperationException ex) { Logging.Logger.Instance.LogWarning($"[PurgeContext] Built-in material param filter for {bip}: {ex.Message}"); }
                catch (RevitExceptions.InvalidOperationException ex) { Logging.Logger.Instance.LogWarning($"[PurgeContext] Built-in material param filter for {bip}: {ex.Message}"); }
            }
        }

        private static void CollectSharedMaterialParameterReferences(
            Document doc,
            HashSet<ElementId> allMaterialIds,
            HashSet<ElementId> usedMaterialIds)
        {
            // Discover shared/project parameters of Material type through ParameterBindings,
            // then use ElementParameterFilter for native-side filtering. No per-element
            // parameter enumeration — only targeted get_Parameter() on matching elements.
            BindingMap bindings = doc.ParameterBindings;
            DefinitionBindingMapIterator iter = bindings.ForwardIterator();

            while (iter.MoveNext())
            {
                try
                {
                    if (iter.Current is not InstanceBinding) continue;

                    Definition def = iter.Key;
                    if (def.GetDataType() != SpecTypeId.Reference.Material) continue;

                    if (def is not InternalDefinition internalDef) continue;
                    ElementId paramId = internalDef.Id;
                    if (paramId == ElementId.InvalidElementId) continue;

                    // Prefer GUID access for shared params (faster than LookupParameter by name).
                    Guid? guid = (doc.GetElement(paramId) as SharedParameterElement)?.GuidValue;

                    var provider = new ParameterValueProvider(paramId);
                    var rule = new FilterElementIdRule(
                        provider, new FilterNumericGreater(), ElementId.InvalidElementId);
                    var filter = new ElementParameterFilter(rule);

                    foreach (Element element in new FilteredElementCollector(doc)
                        .WhereElementIsNotElementType()
                        .WherePasses(filter))
                    {
                        try
                        {
                            Parameter p = guid.HasValue
                                ? element.get_Parameter(guid.Value)
                                : element.LookupParameter(def.Name);

                            if (p?.HasValue == true && p.StorageType == StorageType.ElementId)
                            {
                                ElementId id = p.AsElementId();
                                if (allMaterialIds.Contains(id))
                                {
                                    usedMaterialIds.Add(id);
                                }
                            }
                        }
                        catch (ArgumentException ex) { Logging.Logger.Instance.LogWarning($"[PurgeContext] Shared material param read for element {element.Id}: {ex.Message}"); }
                        catch (InvalidOperationException ex) { Logging.Logger.Instance.LogWarning($"[PurgeContext] Shared material param read for element {element.Id}: {ex.Message}"); }
                        catch (RevitExceptions.InvalidOperationException ex) { Logging.Logger.Instance.LogWarning($"[PurgeContext] Shared material param read for element {element.Id}: {ex.Message}"); }
                    }
                }
                catch (ArgumentException ex) { Logging.Logger.Instance.LogWarning($"[PurgeContext] Shared material param binding scan: {ex.Message}"); }
                catch (InvalidOperationException ex) { Logging.Logger.Instance.LogWarning($"[PurgeContext] Shared material param binding scan: {ex.Message}"); }
                catch (RevitExceptions.InvalidOperationException ex) { Logging.Logger.Instance.LogWarning($"[PurgeContext] Shared material param binding scan: {ex.Message}"); }
            }
        }

        private static void CollectFamilyInstanceMaterialParameters(
            Document doc,
            HashSet<ElementId> allMaterialIds,
            HashSet<ElementId> usedMaterialIds)
        {
            // Family-internal material parameters don't appear in ParameterBindings or as
            // BuiltInParameters. Discover them by inspecting FamilySymbol types (safe — these
            // are element types, hundreds not hundreds of thousands), then do targeted
            // LookupParameter reads on their instances (one native call per param, no COM
            // collection enumeration).
            var materialParamNamesBySymbol = new Dictionary<ElementId, List<string>>();

            foreach (FamilySymbol symbol in new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>())
            {
                try
                {
                    var names = new List<string>();
                    foreach (Parameter param in symbol.Parameters)
                    {
                        try
                        {
                            if (param.StorageType == StorageType.ElementId &&
                                param.Definition.GetDataType() == SpecTypeId.Reference.Material)
                            {
                                names.Add(param.Definition.Name);
                            }
                        }
                        catch (ArgumentException ex) { Logging.Logger.Instance.LogWarning($"[PurgeContext] Symbol material param check: {ex.Message}"); }
                        catch (InvalidOperationException ex) { Logging.Logger.Instance.LogWarning($"[PurgeContext] Symbol material param check: {ex.Message}"); }
                        catch (RevitExceptions.InvalidOperationException ex) { Logging.Logger.Instance.LogWarning($"[PurgeContext] Symbol material param check: {ex.Message}"); }
                    }

                    if (names.Count > 0)
                    {
                        materialParamNamesBySymbol[symbol.Id] = names;
                    }
                }
                catch (ArgumentException ex) { Logging.Logger.Instance.LogWarning($"[PurgeContext] FamilySymbol material scan for {symbol.Id}: {ex.Message}"); }
                catch (InvalidOperationException ex) { Logging.Logger.Instance.LogWarning($"[PurgeContext] FamilySymbol material scan for {symbol.Id}: {ex.Message}"); }
                catch (RevitExceptions.InvalidOperationException ex) { Logging.Logger.Instance.LogWarning($"[PurgeContext] FamilySymbol material scan for {symbol.Id}: {ex.Message}"); }
            }

            if (materialParamNamesBySymbol.Count == 0) return;

            foreach (FamilyInstance instance in new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>())
            {
                if (!materialParamNamesBySymbol.TryGetValue(instance.GetTypeId(), out var paramNames))
                    continue;

                foreach (string name in paramNames)
                {
                    try
                    {
                        Parameter p = instance.LookupParameter(name);
                        if (p?.HasValue == true && p.StorageType == StorageType.ElementId)
                        {
                            ElementId id = p.AsElementId();
                            if (allMaterialIds.Contains(id))
                            {
                                usedMaterialIds.Add(id);
                            }
                        }
                    }
                    catch (ArgumentException ex) { Logging.Logger.Instance.LogWarning($"[PurgeContext] Instance material param lookup '{name}': {ex.Message}"); }
                    catch (InvalidOperationException ex) { Logging.Logger.Instance.LogWarning($"[PurgeContext] Instance material param lookup '{name}': {ex.Message}"); }
                    catch (RevitExceptions.InvalidOperationException ex) { Logging.Logger.Instance.LogWarning($"[PurgeContext] Instance material param lookup '{name}': {ex.Message}"); }
                }
            }
        }

        private static void CollectLevelId(Element element, HashSet<ElementId> placedLevelIds)
        {
            try
            {
                AddIfValid(placedLevelIds, element.LevelId);
            }
            catch (ArgumentException ex)
            {
                Logging.Logger.Instance.LogWarning($"[PurgeContext] Failed to collect level for element {element.Id}: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Logging.Logger.Instance.LogWarning($"[PurgeContext] Failed to collect level for element {element.Id}: {ex.Message}");
            }
            catch (RevitExceptions.InvalidOperationException ex)
            {
                Logging.Logger.Instance.LogWarning($"[PurgeContext] Failed to collect level for element {element.Id}: {ex.Message}");
            }
        }

        private static void RunGuarded(Action action, string message)
        {
            try
            {
                action();
            }
            catch (ArgumentException ex)
            {
                Logging.Logger.Instance.LogWarning($"{message}: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Logging.Logger.Instance.LogWarning($"{message}: {ex.Message}");
            }
            catch (RevitExceptions.InvalidOperationException ex)
            {
                Logging.Logger.Instance.LogWarning($"{message}: {ex.Message}");
            }
        }
    }
}
