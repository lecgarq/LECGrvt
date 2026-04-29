using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public sealed class CompactingStylesContext
    {
        private static readonly HashSet<ViewType> SupportedViewTypes = new HashSet<ViewType>
        {
            ViewType.FloorPlan,
            ViewType.CeilingPlan,
            ViewType.Elevation,
            ViewType.Section,
            ViewType.Detail,
            ViewType.ThreeD,
            ViewType.DraftingView,
            ViewType.Legend,
            ViewType.AreaPlan,
            ViewType.EngineeringPlan,
        };

        private CompactingStylesContext(
            Document document,
            IReadOnlyList<Element> instanceElements,
            IReadOnlyList<Element> typeElements,
            IReadOnlyList<View> views,
            IReadOnlyList<Category> categories,
            IReadOnlyList<Category> allCategories,
            IReadOnlyList<Material> materials,
            IReadOnlyList<FilledRegionType> filledRegionTypes,
            IReadOnlyList<TextNote> textNotes,
            Dictionary<ElementId, HashSet<ElementId>> parameterIndex)
        {
            Document = document;
            InstanceElements = instanceElements;
            TypeElements = typeElements;
            Views = views;
            Categories = categories;
            AllCategories = allCategories;
            Materials = materials;
            FilledRegionTypes = filledRegionTypes;
            TextNotes = textNotes;
            ParameterIndex = parameterIndex;
        }

        public Document Document { get; }
        public IReadOnlyList<Element> InstanceElements { get; }
        public IReadOnlyList<Element> TypeElements { get; }
        public IReadOnlyList<View> Views { get; }
        public IReadOnlyList<Category> Categories { get; }
        public IReadOnlyList<Category> AllCategories { get; }
        public IReadOnlyList<Material> Materials { get; }
        public IReadOnlyList<FilledRegionType> FilledRegionTypes { get; }
        public IReadOnlyList<TextNote> TextNotes { get; }
        public Dictionary<ElementId, HashSet<ElementId>> ParameterIndex { get; }

        public static CompactingStylesContext Create(
            Document doc,
            IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(reporter);

            return Create(doc, reporter.Log, (percent, message) => reporter.Report(message, percent));
        }

        public static CompactingStylesContext Create(
            Document doc,
            Action<string>? logCallback = null,
            Action<double, string>? progressCallback = null)
        {
            ArgumentNullException.ThrowIfNull(doc);

            logCallback?.Invoke("Preparing compacting context...");

            // Only collect type elements for parameter indexing.
            // Instance elements (200K-500K in large models) are not needed —
            // pattern/style references live on types, materials, views, and categories
            // which are handled by dedicated indexes in each compaction service.
            IReadOnlyList<Element> instanceElements = Array.Empty<Element>();

            IReadOnlyList<Element> typeElements = new FilteredElementCollector(doc)
                .WhereElementIsElementType()
                .ToElements()
                .ToList();

            IReadOnlyList<View> views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(view => view.IsTemplate || SupportedViewTypes.Contains(view.ViewType))
                .ToList();

            IReadOnlyList<Category> categories = doc.Settings.Categories.Cast<Category>().ToList();
            IReadOnlyList<Category> allCategories = FlattenCategories(categories);

            IReadOnlyList<Material> materials = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .ToList();

            IReadOnlyList<FilledRegionType> filledRegionTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(FilledRegionType))
                .Cast<FilledRegionType>()
                .ToList();

            IReadOnlyList<TextNote> textNotes = new FilteredElementCollector(doc)
                .OfClass(typeof(TextNote))
                .Cast<TextNote>()
                .ToList();

            Dictionary<ElementId, HashSet<ElementId>> parameterIndex =
                BuildParamIndex(instanceElements, typeElements, progressCallback);

            return new CompactingStylesContext(
                doc,
                instanceElements,
                typeElements,
                views,
                categories,
                allCategories,
                materials,
                filledRegionTypes,
                textNotes,
                parameterIndex);
        }

        private static Dictionary<ElementId, HashSet<ElementId>> BuildParamIndex(
            IReadOnlyList<Element> instanceElements,
            IReadOnlyList<Element> typeElements,
            Action<double, string>? progressCallback)
        {
            var index = new Dictionary<ElementId, HashSet<ElementId>>();
            int total = instanceElements.Count + typeElements.Count;
            int processed = 0;

            void IndexElements(IReadOnlyList<Element> elements)
            {
                foreach (Element element in elements)
                {
                    if (element == null || !element.IsValidObject)
                    {
                        processed++;
                        continue;
                    }

                    foreach (Parameter parameter in element.Parameters)
                    {
                        if (parameter.IsReadOnly || parameter.StorageType != StorageType.ElementId)
                        {
                            continue;
                        }

                        if (!parameter.HasValue) continue;
                        ElementId value = parameter.AsElementId();
                        if (value == ElementId.InvalidElementId)
                        {
                            continue;
                        }

                        if (!index.TryGetValue(value, out HashSet<ElementId>? set))
                        {
                            set = new HashSet<ElementId>();
                            index[value] = set;
                        }

                        set.Add(element.Id);
                    }

                    processed++;
                    if (processed % 10000 == 0)
                    {
                        progressCallback?.Invoke(0, $"Indexing parameters... {processed}/{total}");
                    }
                }
            }

            IndexElements(instanceElements);
            IndexElements(typeElements);

            return index;
        }

        private static IReadOnlyList<Category> FlattenCategories(IEnumerable<Category> rootCategories)
        {
            var allCategories = new List<Category>();

            foreach (Category category in rootCategories)
            {
                AddCategory(category, allCategories);
            }

            return allCategories;
        }

        private static void AddCategory(Category category, List<Category> allCategories)
        {
            allCategories.Add(category);

            foreach (Category subCategory in category.SubCategories)
            {
                AddCategory(subCategory, allCategories);
            }
        }
    }
}
