#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Configuration;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    /// <summary>
    /// Service for purging unused elements from Revit documents.
    /// Refactored to use RevitConstants and cleaner logic.
    /// </summary>
    public class PurgeService : IPurgeService
    {
        private readonly IPurgeReferenceScannerService _referenceScanner;

        public PurgeService() : this(new PurgeReferenceScannerService())
        {
        }

        public PurgeService(IPurgeReferenceScannerService referenceScanner)
        {
            _referenceScanner = referenceScanner;
        }

        public void PurgeAll(Document doc, bool lineStyles, bool fillPatterns, bool materials, bool levels, Action<string> logCallback, Action<double, string> progressCallback)
        {
            int lineStylesDeleted = 0;
            int fillPatternsDeleted = 0;
            int materialsDeleted = 0;
            int levelsDeleted = 0;

            using (Transaction t = new Transaction(doc, "Purge Unused Elements"))
            {
                t.Start();

                // Run 3 times to catch dependent elements
                for (int i = 1; i <= 3; i++)
                {
                    logCallback?.Invoke($"--- PASS {i}/3 ---");

                    if (lineStyles)
                    {
                        logCallback?.Invoke("Checking Line Styles...");
                        progressCallback?.Invoke(10 + (i * 10), $"Pass {i}: Purging line styles...");
                        lineStylesDeleted += PurgeUnusedLineStyles(doc, logCallback);
                    }

                    if (fillPatterns)
                    {
                        logCallback?.Invoke("Checking Fill Patterns...");
                        progressCallback?.Invoke(20 + (i * 10), $"Pass {i}: Purging fill patterns...");
                        fillPatternsDeleted += PurgeUnusedFillPatterns(doc, logCallback);
                    }

                    if (materials)
                    {
                        logCallback?.Invoke("Checking Materials...");
                        progressCallback?.Invoke(30 + (i * 10), $"Pass {i}: Purging materials...");
                        materialsDeleted += PurgeUnusedMaterials(doc, logCallback);
                    }
                    
                    if (levels)
                    {
                        logCallback?.Invoke("Checking Levels...");
                        progressCallback?.Invoke(40 + (i * 10), $"Pass {i}: Purging levels...");
                        levelsDeleted += PurgeUnusedLevels(doc, logCallback);
                    }
                }

                t.Commit();
            }

            // Summary
            progressCallback?.Invoke(100, "Complete!");
            logCallback?.Invoke("");
            logCallback?.Invoke("=== SUMMARY ===");
            logCallback?.Invoke($"Line Styles deleted: {lineStylesDeleted}");
            logCallback?.Invoke($"Fill Patterns deleted: {fillPatternsDeleted}");
            logCallback?.Invoke($"Materials deleted: {materialsDeleted}");
            logCallback?.Invoke($"Levels deleted: {levelsDeleted}");
            logCallback?.Invoke("");
            
            int total = lineStylesDeleted + fillPatternsDeleted + materialsDeleted + levelsDeleted;
            logCallback?.Invoke($"✓ Total items purged: {total}");
        }

        /// <summary>
        /// Purge unused line styles.
        /// </summary>
        public int PurgeUnusedLineStyles(Document doc, Action<string>? logCallback = null)
        {
            logCallback?.Invoke("Scanning for unused line styles...");
            
            Category? linesCategory = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
            if (linesCategory == null) return 0;

            // 1. Collect user-created style IDs
            var allStyles = new Dictionary<ElementId, string>();
            foreach (Category subCat in linesCategory.SubCategories)
            {
                if (!RevitConstants.IsBuiltInLineStyle(subCat.Name))
                {
                    allStyles[subCat.Id] = subCat.Name;
                }
            }
            logCallback?.Invoke($"  Found {allStyles.Count} potential candidates.");

            // 2. Find usage in CurveElements
            var usedIds = new HashSet<ElementId>();
            var curves = new FilteredElementCollector(doc).OfClass(typeof(CurveElement));

            foreach (CurveElement curve in curves)
            {
                if (curve.LineStyle is GraphicsStyle gs && gs.GraphicsStyleCategory != null)
                {
                    usedIds.Add(gs.GraphicsStyleCategory.Id);
                }
            }

            // 3. Delete Unused
            int deleted = 0;
            foreach (var kvp in allStyles)
            {
                if (!usedIds.Contains(kvp.Key))
                {
                    if (DeleteElement(doc, kvp.Key, kvp.Value, logCallback)) deleted++;
                }
            }

            logCallback?.Invoke($"  Deleted {deleted} line styles.");
            return deleted;
        }

        /// <summary>
        /// Purge unused fill patterns.
        /// </summary>
        public int PurgeUnusedFillPatterns(Document doc, Action<string>? logCallback = null)
        {
            logCallback?.Invoke("Scanning for unused fill patterns...");

            // 1. All patterns
            var allPatterns = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .Where(p => !RevitConstants.IsBuiltInFillPattern(p.Name))
                .ToDictionary(p => p.Id, p => p.Name);

            // 2. Usage in Materials
            var usedIds = new HashSet<ElementId>();
            foreach (Material mat in new FilteredElementCollector(doc).OfClass(typeof(Material)))
            {
                _referenceScanner.AddIfValid(usedIds, mat.SurfaceForegroundPatternId);
                _referenceScanner.AddIfValid(usedIds, mat.SurfaceBackgroundPatternId);
                _referenceScanner.AddIfValid(usedIds, mat.CutForegroundPatternId);
                _referenceScanner.AddIfValid(usedIds, mat.CutBackgroundPatternId);
            }

            // 3. Usage in Filled Regions
            foreach (FilledRegionType frt in new FilteredElementCollector(doc).OfClass(typeof(FilledRegionType)))
            {
                _referenceScanner.AddIfValid(usedIds, frt.ForegroundPatternId);
                _referenceScanner.AddIfValid(usedIds, frt.BackgroundPatternId);
            }

            // 4. Delete
            int deleted = 0;
            foreach (var kvp in allPatterns)
            {
                if (!usedIds.Contains(kvp.Key))
                {
                    if (DeleteElement(doc, kvp.Key, kvp.Value, logCallback)) deleted++;
                }
            }

            logCallback?.Invoke($"  Deleted {deleted} fill patterns.");
            return deleted;
        }

        /// <summary>
        /// Purge unused materials.
        /// </summary>
        /// <summary>
        /// Purge unused materials.
        /// Optimized to use HashSet lookups instead of expensive doc.GetElement calls.
        /// </summary>
        public int PurgeUnusedMaterials(Document doc, Action<string>? logCallback = null)
        {
            logCallback?.Invoke("Scanning for unused materials...");

            // 1. All materials (Candidates for deletion)
            var allMaterials = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .Where(m => !RevitConstants.IsBuiltInMaterial(m.Name))
                .ToDictionary(m => m.Id, m => m.Name);

            // 2. All valid Material IDs (for fast lookup)
            var validMaterialIds = new HashSet<ElementId>(allMaterials.Keys);
            var usedIds = new HashSet<ElementId>();

            // Safe Classes for Parameter Scanning (HostObjects and Families are safe to scan)
            // We avoid scanning parameters of weird internal elements which causes crashes.
            var safeClassesForParams = new List<Type> {
                typeof(HostObject),
                typeof(FamilyInstance),
                typeof(FamilySymbol)
            };
            var safeClassFilter = new ElementMulticlassFilter(safeClassesForParams);

            // 3. Scan ALL Model Elements (Types and Instances)
            // Combined loop for efficiency
            var collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WhereElementIsViewIndependent();

            var typesCollector = new FilteredElementCollector(doc)
                .WhereElementIsElementType();

            // Helper to process element
            void ProcessElement(Element e)
            {
                if (!e.IsValidObject) return;
                
                try
                {
                    // A. Use GetMaterialIds (Native Revit API - Fast & Stable)
                    // This finds material used in geometry, layers, and painted faces.
                    var mats = e.GetMaterialIds(false);
                    foreach (var id in mats) usedIds.Add(id);
                }
                catch { }

                try
                {
                    // B. Scan Parameters (Only for "Safe" classes to avoid crash)
                    if (safeClassFilter.PassesFilter(e))
                    {
                        _referenceScanner.CollectUsedIds(e, validMaterialIds, usedIds);
                    }
                }
                catch { }
            }

            foreach (var e in typesCollector) ProcessElement(e);
            foreach (var e in collector) ProcessElement(e);

            // 4. Delete
            int deleted = 0;
            foreach (var kvp in allMaterials)
            {
                if (!usedIds.Contains(kvp.Key))
                {
                    if (DeleteElement(doc, kvp.Key, kvp.Value, logCallback)) deleted++;
                }
            }

            logCallback?.Invoke($"  Deleted {deleted} materials.");
            return deleted;
        }

        /// <summary>
        /// Purge unused levels.
        /// A level is unused if:
        /// 1. No elements are placed on it (ElementLevelFilter).
        /// 2. It is not referenced by valid parameters (e.g. Top Constraint) - similar scan to Materials.
        /// </summary>
        public int PurgeUnusedLevels(Document doc, Action<string>? logCallback = null)
        {
            logCallback?.Invoke("Scanning for unused levels...");

            // 1. All Levels
            var allLevels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();

            if (allLevels.Count <= 1)
            {
                logCallback?.Invoke("  Skipping level purge (project has 1 or fewer levels).");
                return 0; // Don't delete the last level
            }

            var levelIdsToRemove = new HashSet<ElementId>();
            var validLevelIds = new HashSet<ElementId>(allLevels.Select(l => l.Id));

            // 2. Check each level for placed elements
            // We do this FIRST because it's safer.
            // But checking parameters requires a full model scan, which is expensive.
            // Let's do the parameter scan first for ALL levels to build a "Referenced Levels" set.

            var referencedLevelIds = new HashSet<ElementId>();

            // Scan ALL elements for Level references
            // Note: We scan both Types and Instances because types might reference levels (unlikely but possible?)
            // Usually Instances reference constraints.
            
            // Scan Instances
            foreach (Element inst in new FilteredElementCollector(doc).WhereElementIsNotElementType())
            {
                _referenceScanner.CollectUsedIds(inst, validLevelIds, referencedLevelIds);
            }

            logCallback?.Invoke($"  Found {referencedLevelIds.Count} levels referenced by parameters.");

            // 3. Check for placed elements (ElementLevelFilter)
            foreach (var level in allLevels)
            {
                // If it's already referenced by a parameter, we can't delete it (it's a constraint)
                if (referencedLevelIds.Contains(level.Id)) continue;
                
                // If not referenced, check if anything is hosted on it
                ElementLevelFilter levelFilter = new ElementLevelFilter(level.Id);
                var dependentElements = new FilteredElementCollector(doc)
                    .WherePasses(levelFilter)
                    .ToElementIds();

                if (dependentElements.Count == 0)
                {
                    // No elements on this level, and not referenced -> Candidate!
                    levelIdsToRemove.Add(level.Id);
                }
            }

            // 4. Delete
            int deleted = 0;
            // Sort by elevationdescending? Doesn't matter much for deletion if they are empty.
            foreach (var level in allLevels)
            {
                if (levelIdsToRemove.Contains(level.Id))
                {
                    if (DeleteElement(doc, level.Id, level.Name, logCallback)) deleted++;
                }
            }

            logCallback?.Invoke($"  Deleted {deleted} levels.");
            return deleted; 
        }

        // ============================================
        // HELPERS
        // ============================================

        private bool DeleteElement(Document doc, ElementId id, string name, Action<string>? logCallback)
        {
            try
            {
                doc.Delete(id);
                logCallback?.Invoke($"  Deleted: {name}");
                return true;
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"  Could not delete '{name}': {ex.Message}");
                return false;
            }
        }

    }
}
