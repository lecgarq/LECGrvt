using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Services
{
    // -----------------------------------------------------------------------
    // ScopeMask: pure flag enumeration for scope dispatch (no Revit API).
    // Maps the bool scope arguments of CollectBaseElements to a testable mask.
    // -----------------------------------------------------------------------
    [System.Flags]
    internal enum ScopeMask
    {
        None = 0,
        Types = 1 << 0,
        Families = 1 << 1,
        Views = 1 << 2,
        Sheets = 1 << 3,
        Materials = 1 << 4,
        ObjectStyles = 1 << 5,
        LineStyles = 1 << 6,
        FillPatterns = 1 << 7,
        FamilyParameters = 1 << 8,
    }

    public class BaseElementCollectionService : IBaseElementCollectionService
    {
        private readonly ILogger _logger;

        public BaseElementCollectionService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public List<ElementData> CollectBaseElements(Document doc, bool types, bool families, bool views, bool sheets, bool materials, bool objectStyles, bool lineStyles, bool fillPatterns, bool familyParameters)
        {
            ArgumentNullException.ThrowIfNull(doc);
            List<ElementData> data = new List<ElementData>();

            if (types)
            {
                FilteredElementCollector typeCollector = new FilteredElementCollector(doc)
                    .WhereElementIsElementType();

                foreach (var el in typeCollector)
                {
                    var (resolvedName, resolvedCategory) = ElementLabelService.GetLabels(el);
                    data.Add(new ElementData
                    {
                        Id = el.Id.Value,
                        Name = resolvedName,
                        Category = resolvedCategory,
                        Type = "Type",
                        OriginalValue = resolvedName
                    });
                }
            }

            if (families)
            {
                FilteredElementCollector familyCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(Family));

                foreach (var el in familyCollector)
                {
                    data.Add(new ElementData
                    {
                        Id = el.Id.Value,
                        Name = el.Name,
                        Category = "Families",
                        Type = "Family",
                        OriginalValue = el.Name
                    });
                }
            }

            if (views || sheets)
            {
                FilteredElementCollector viewCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(View));

                foreach (var el in viewCollector)
                {
                    if (el is View v && !v.IsTemplate)
                    {
                        bool isSheet = v.ViewType == ViewType.DrawingSheet;
                        if (isSheet && sheets)
                        {
                            data.Add(new ElementData { Id = el.Id.Value, Name = v.Name, Category = "Sheets", Type = "Sheet", OriginalValue = v.Name });
                        }
                        else if (!isSheet && views)
                        {
                            string cat = v.ViewType.ToString();
                            data.Add(new ElementData { Id = el.Id.Value, Name = v.Name, Category = cat, Type = "View", OriginalValue = v.Name });
                        }
                    }
                }
            }

            if (materials)
            {
                FilteredElementCollector materialCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(Material));

                foreach (var el in materialCollector)
                {
                    data.Add(new ElementData { Id = el.Id.Value, Name = el.Name, Category = "Materials", Type = "Material", OriginalValue = el.Name });
                }
            }

            if (fillPatterns)
            {
                FilteredElementCollector patternCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement));

                foreach (var el in patternCollector)
                {
                    data.Add(new ElementData { Id = el.Id.Value, Name = el.Name, Category = "Fill Patterns", Type = "FillPattern", OriginalValue = el.Name });
                }
            }

            if (objectStyles || lineStyles)
            {
                FilteredElementCollector styleCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(GraphicsStyle));

                foreach (var el in styleCollector)
                {
                    if (el is GraphicsStyle gs && gs.GraphicsStyleType == GraphicsStyleType.Projection)
                    {
                        Category cat = gs.GraphicsStyleCategory;
                        if (cat == null)
                        {
                            // Fallback: GraphicsStyleCategory missing — surface the row via ElementLabelService
                            // rather than silently skipping. Locale-safe label resolution + LogView warning.
                            var (gsName, gsCategory) = ElementLabelService.GetLabels(gs);
                            try
                            {
                                _logger.LogWarning(
                                    $"GraphicsStyle {gs.Id.Value} has null GraphicsStyleCategory — using fallback label ({gsName}, {gsCategory})",
                                    scope: "BaseElementCollection");
                            }
                            catch { }
                            data.Add(new ElementData
                            {
                                Id = gs.Id.Value,
                                Name = gsName,
                                Category = gsCategory,
                                Type = "ObjectStyle",
                                OriginalValue = gsName
                            });
                            continue;
                        }

                        // Identify if this is a built-in category/subcategory
                        // SAFE CHECK: BuiltInCategories have negative integer IDs.
                        // User-created subcategories have positive integer IDs.
                        bool isBuiltIn = cat.Id.Value < 0;

                        // We want to SHOW user created styles (which are not built-in).
                        // So if it IS built-in, we generally skip it...
                        // ...UNLESS it's an Import (which also has positive keys sometimes, but often negative if standard).
                        // Actually, Imports in Object Styles usually appear as subcategories of "Imports in Families".

                        // For now, the user goal is to see styles that AREN'T showing up.
                        // The previous logic skipped if Enum.IsDefined, which might have been too aggressive 
                        // or coincidentally matching user IDs if they were large/small enough (unlikely but possible).
                        // The reliable check is IsBuiltIn -> Id < 0.

                        if (isBuiltIn) continue;

                        // Additional Check: If it is a subcategory of Lines, it is a Line Style
                        bool isLineStyle = cat.Parent != null && cat.Parent.Id.Value == (long)BuiltInCategory.OST_Lines;

                        if (isLineStyle && lineStyles)
                        {
                            data.Add(new ElementData
                            {
                                Id = el.Id.Value,
                                Name = cat.Name,
                                Category = "Line Styles",
                                Type = "LineStyle",
                                OriginalValue = cat.Name
                            });
                        }
                        else if (!isLineStyle && objectStyles)
                        {
                            data.Add(new ElementData
                            {
                                Id = el.Id.Value,
                                Name = cat.Name,
                                Category = "Object Styles",
                                Type = "ObjectStyle",
                                OriginalValue = cat.Name
                            });
                        }
                    }
                }
            }

            if (familyParameters)
            {
                // Build a lookup: definition -> isInstance, using the document's ParameterBindings
                // This is the ONLY way to know if a param is Instance or Type in project context.
                var instanceDefs = new HashSet<Definition>();
                BindingMap bindingMap = doc.ParameterBindings;
                DefinitionBindingMapIterator it = bindingMap.ForwardIterator();
                while (it.MoveNext())
                {
                    if (it.Current is InstanceBinding)
                        instanceDefs.Add(it.Key);
                }

                // Group ALL FamilySymbols by their parent Family to ensure we collect
                // parameters from EVERY symbol, not just the first one encountered.
                FilteredElementCollector symbolCollector = new FilteredElementCollector(doc)
                    .WhereElementIsElementType()
                    .OfClass(typeof(FamilySymbol));

                // Group by Family.Id so we process each family once but scan ALL its symbols
                var symbolsByFamily = new Dictionary<long, List<FamilySymbol>>();
                foreach (FamilySymbol fs in symbolCollector)
                {
                    if (fs.Family == null) continue;
                    long familyId = fs.Family.Id.Value;
                    if (!symbolsByFamily.ContainsKey(familyId))
                        symbolsByFamily[familyId] = new List<FamilySymbol>();
                    symbolsByFamily[familyId].Add(fs);
                }

                // Track seen param names PER FAMILY across both type and instance scans
                var seenParamsByFamily = new Dictionary<long, HashSet<string>>();

                // --- Phase A: Scan FamilySymbols (type parameters) ---
                foreach (var kvp in symbolsByFamily)
                {
                    List<FamilySymbol> symbols = kvp.Value;
                    if (symbols.Count == 0)
                    {
                        continue;
                    }

                    string familyName = symbols[0].FamilyName;
                    long familyId = kvp.Key;

                    var seenParamNames = new HashSet<string>();
                    seenParamsByFamily[familyId] = seenParamNames;

                    foreach (FamilySymbol fs in symbols)
                    {
                        foreach (Parameter p in fs.Parameters)
                        {
                            bool isShared = p.IsShared;
                            bool isBuiltIn = p.Id.Value < 0;

                            if (!isShared && !isBuiltIn && seenParamNames.Add(p.Definition.Name))
                            {
                                bool isInstanceParam = instanceDefs.Contains(p.Definition);

                                string paramGroupLabel = TryGetGroupLabel(
                                    () => LabelUtils.GetLabelForGroup(p.Definition.GetGroupTypeId()));

                                data.Add(new ElementData
                                {
                                    Id = familyId,
                                    Name = p.Definition.Name,
                                    Category = familyName,
                                    Type = "FamilyParameter",
                                    OriginalValue = p.Definition.Name,
                                    ParamGroup = paramGroupLabel,
                                    IsInstance = isInstanceParam,
                                    IsReadOnly = p.IsReadOnly
                                });
                            }
                        }
                    }
                }

                // --- Phase B: Scan FamilyInstances for instance-only parameters ---
                // Some parameters only appear on placed instances, not on the FamilySymbol.
                FilteredElementCollector instanceCollector = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .OfClass(typeof(FamilyInstance));

                // We only need ONE instance per family to discover instance params
                var processedInstanceFamilies = new HashSet<long>();

                foreach (FamilyInstance fi in instanceCollector)
                {
                    if (fi.Symbol?.Family == null)
                    {
                        try
                        {
                            _logger.LogWarning(
                                $"Skipping family parameter row for FamilyInstance {fi.Id.Value} — Symbol or Family null",
                                scope: "BaseElementCollection");
                        }
                        catch { }
                        continue;
                    }
                    long familyId = fi.Symbol.Family.Id.Value;

                    // Skip if we already scanned an instance of this family
                    if (!processedInstanceFamilies.Add(familyId)) continue;

                    string familyName = fi.Symbol.FamilyName;

                    // Get or create the seen set for this family
                    if (!seenParamsByFamily.TryGetValue(familyId, out var seenParamNames))
                    {
                        seenParamNames = new HashSet<string>();
                        seenParamsByFamily[familyId] = seenParamNames;
                    }

                    foreach (Parameter p in fi.Parameters)
                    {
                        bool isShared = p.IsShared;
                        bool isBuiltIn = p.Id.Value < 0;

                        if (!isShared && !isBuiltIn && seenParamNames.Add(p.Definition.Name))
                        {
                            // This param was NOT found on the FamilySymbol — it's instance-only
                            // NOTE: full dedup of ParamGroup branches deferred to v1.2 Phase 6
                            string paramGroupLabel = TryGetGroupLabel(
                                () => LabelUtils.GetLabelForGroup(p.Definition.GetGroupTypeId()));

                            data.Add(new ElementData
                            {
                                Id = familyId,
                                Name = p.Definition.Name,
                                Category = familyName,
                                Type = "FamilyParameter",
                                OriginalValue = p.Definition.Name,
                                ParamGroup = paramGroupLabel,
                                IsInstance = true, // Found on instance, so it's instance
                                IsReadOnly = p.IsReadOnly
                            });
                        }
                    }
                }
            }

            return data;
        }

        // -----------------------------------------------------------------------
        // Pure-data helpers — internal static, no Revit API in signatures.
        // Each ≤ 30 LOC. Directly unit-testable without a Revit Document.
        // -----------------------------------------------------------------------

        /// <summary>
        /// Safely resolves a parameter-group label by invoking <paramref name="resolve"/>,
        /// which is expected to call GetGroupTypeId() then LabelUtils.GetLabelForGroup().
        /// Returns "" if the delegate throws (Revit API unavailable or group type unknown).
        /// NOTE: full dedup of ParamGroup branches deferred to v1.2 Phase 6.
        /// </summary>
        internal static string TryGetGroupLabel(Func<string> resolve)
        {
            try
            {
                return resolve();
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Maps individual scope bool flags to a combined <see cref="ScopeMask"/>.
        /// Pure flag→mask mapping; no Revit API dependency.
        /// </summary>
        internal static ScopeMask DispatchScopeFlags(
            bool types, bool families, bool views, bool sheets,
            bool materials, bool objectStyles, bool lineStyles,
            bool fillPatterns, bool familyParameters)
        {
            var mask = ScopeMask.None;
            if (types) mask |= ScopeMask.Types;
            if (families) mask |= ScopeMask.Families;
            if (views) mask |= ScopeMask.Views;
            if (sheets) mask |= ScopeMask.Sheets;
            if (materials) mask |= ScopeMask.Materials;
            if (objectStyles) mask |= ScopeMask.ObjectStyles;
            if (lineStyles) mask |= ScopeMask.LineStyles;
            if (fillPatterns) mask |= ScopeMask.FillPatterns;
            if (familyParameters) mask |= ScopeMask.FamilyParameters;
            return mask;
        }

        /// <summary>
        /// Merges two Phase A/B param scan result lists, deduplicating rows by
        /// (familyId, paramName). scanA rows take precedence; scanB rows are added
        /// only when the (familyId, paramName) pair has not been seen.
        /// Empty inputs → empty output.
        /// </summary>
        internal static List<ElementData> MergeParamScanResults(
            IReadOnlyList<ElementData> scanA,
            IReadOnlyList<ElementData> scanB)
        {
            var seen = new Dictionary<long, HashSet<string>>();
            var result = new List<ElementData>();

            foreach (var row in scanA)
            {
                if (!seen.TryGetValue(row.Id, out var names))
                    seen[row.Id] = names = new HashSet<string>(StringComparer.Ordinal);

                if (names.Add(row.Name))
                    result.Add(row);
            }

            foreach (var row in scanB)
            {
                if (!seen.TryGetValue(row.Id, out var names))
                    seen[row.Id] = names = new HashSet<string>(StringComparer.Ordinal);

                if (names.Add(row.Name))
                    result.Add(row);
            }

            return result;
        }
    }
}
