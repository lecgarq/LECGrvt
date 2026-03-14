using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class FamilyParameterSetupService : IFamilyParameterSetupService
    {
        public void ConfigureTargetFamilyParameters(Document targetFamilyDoc, Document sourceFamilyDoc)
        {
            ArgumentNullException.ThrowIfNull(targetFamilyDoc);
            ArgumentNullException.ThrowIfNull(sourceFamilyDoc);

            Family targetFamily = targetFamilyDoc.OwnerFamily;
            Family sourceFamily = sourceFamilyDoc.OwnerFamily;

            // Preserve the original category (Door stays Door, Window stays Window, etc.)
            Category sourceCategory = sourceFamily.FamilyCategory;
            if (sourceCategory != null)
            {
                Category? matchingCategory = targetFamilyDoc.Settings.Categories.get_Item(sourceCategory.BuiltInCategory);
                if (matchingCategory != null)
                    targetFamily.FamilyCategory = matchingCategory;
            }

            // Enable work-plane-based placement (required for non-hosted behavior)
            Parameter? pWorkPlane = targetFamily.get_Parameter(BuiltInParameter.FAMILY_WORK_PLANE_BASED);
            if (pWorkPlane != null && !pWorkPlane.IsReadOnly) pWorkPlane.Set(1);

            // Copy ALWAYS_VERTICAL value from the source family
            Parameter? pSourceAlwaysVertical = sourceFamily.get_Parameter(BuiltInParameter.FAMILY_ALWAYS_VERTICAL);
            int alwaysVerticalValue = pSourceAlwaysVertical != null ? pSourceAlwaysVertical.AsInteger() : 0;

            Parameter? pAlwaysVertical = targetFamily.get_Parameter(BuiltInParameter.FAMILY_ALWAYS_VERTICAL);
            if (pAlwaysVertical != null && !pAlwaysVertical.IsReadOnly) pAlwaysVertical.Set(alwaysVerticalValue);

            // Copy family parameters, values, formulas, and types
            CopyFamilyParameters(targetFamilyDoc, sourceFamilyDoc);
        }

        private void CopyFamilyParameters(Document targetFamilyDoc, Document sourceFamilyDoc)
        {
            FamilyManager sourceFm = sourceFamilyDoc.FamilyManager;
            FamilyManager targetFm = targetFamilyDoc.FamilyManager;

            // Pass 1: Create matching parameters on the target
            var paramMap = new Dictionary<string, FamilyParameter>(StringComparer.OrdinalIgnoreCase);
            var sourceParamsWithFormulas = new List<FamilyParameter>();

            foreach (FamilyParameter sourceFp in sourceFm.Parameters)
            {
                // Skip built-in parameters (they already exist on the target)
                if (sourceFp.Id.Value < 0)
                    continue;

                string name = sourceFp.Definition.Name;
                ForgeTypeId groupId = sourceFp.Definition.GetGroupTypeId();
                ForgeTypeId dataType = sourceFp.Definition.GetDataType();
                bool isInstance = sourceFp.IsInstance;

                // Check if target already has a parameter with this name
                FamilyParameter? existing = FindParameterByName(targetFm, name);
                if (existing != null)
                {
                    paramMap[name] = existing;
                }
                else
                {
                    try
                    {
                        FamilyParameter created = targetFm.AddParameter(name, groupId, dataType, isInstance);
                        paramMap[name] = created;
                    }
                    catch (Exception ex)
                    {
                        Logging.Logger.Instance.Log($"  Could not create parameter '{name}': {ex.Message}");
                        continue;
                    }
                }

                if (!string.IsNullOrEmpty(sourceFp.Formula))
                    sourceParamsWithFormulas.Add(sourceFp);
            }

            // Pass 2: Copy parameter values for the current/default type
            // (Must skip parameters that have formulas — formulas override values)
            if (sourceFm.CurrentType != null)
            {
                CopyTypeValues(sourceFm, targetFm, sourceFm.CurrentType, paramMap, sourceParamsWithFormulas);
            }

            // Pass 3: Copy formulas (all parameters exist now, so name references resolve)
            foreach (FamilyParameter sourceFp in sourceParamsWithFormulas)
            {
                string name = sourceFp.Definition.Name;
                if (!paramMap.TryGetValue(name, out FamilyParameter? targetFp))
                    continue;

                try
                {
                    targetFm.SetFormula(targetFp, sourceFp.Formula);
                }
                catch (Exception ex)
                {
                    Logging.Logger.Instance.Log($"  Could not set formula for '{name}': {ex.Message}");
                }
            }

            // Pass 4: Copy additional family types (beyond the default)
            CopyFamilyTypes(sourceFm, targetFm, paramMap, sourceParamsWithFormulas);
        }

        private void CopyTypeValues(
            FamilyManager sourceFm,
            FamilyManager targetFm,
            FamilyType sourceType,
            Dictionary<string, FamilyParameter> paramMap,
            List<FamilyParameter> formulaParams)
        {
            var formulaNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var fp in formulaParams)
                formulaNames.Add(fp.Definition.Name);

            foreach (FamilyParameter sourceFp in sourceFm.Parameters)
            {
                if (sourceFp.Id.Value < 0) continue;

                string name = sourceFp.Definition.Name;

                // Skip parameters with formulas (formula drives the value)
                if (formulaNames.Contains(name)) continue;

                if (!paramMap.TryGetValue(name, out FamilyParameter? targetFp))
                    continue;

                if (!sourceType.HasValue(sourceFp))
                    continue;

                try
                {
                    switch (sourceFp.StorageType)
                    {
                        case StorageType.Double:
                            double? dVal = sourceType.AsDouble(sourceFp);
                            if (dVal.HasValue) targetFm.Set(targetFp, dVal.Value);
                            break;
                        case StorageType.Integer:
                            int? iVal = sourceType.AsInteger(sourceFp);
                            if (iVal.HasValue) targetFm.Set(targetFp, iVal.Value);
                            break;
                        case StorageType.String:
                            string? sVal = sourceType.AsString(sourceFp);
                            if (sVal != null) targetFm.Set(targetFp, sVal);
                            break;
                        case StorageType.ElementId:
                            ElementId idVal = sourceType.AsElementId(sourceFp);
                            targetFm.Set(targetFp, idVal);
                            break;
                    }
                }
                catch
                {
                    // Some values may fail to set (e.g., ElementId references that don't exist in target)
                }
            }
        }

        private void CopyFamilyTypes(
            FamilyManager sourceFm,
            FamilyManager targetFm,
            Dictionary<string, FamilyParameter> paramMap,
            List<FamilyParameter> sourceParamsWithFormulas)
        {
            // Collect source type names, skipping the current/default type
            string? defaultTypeName = sourceFm.CurrentType?.Name;
            var sourceTypes = new List<FamilyType>();
            foreach (FamilyType ft in sourceFm.Types)
            {
                if (ft.Name == defaultTypeName) continue;
                sourceTypes.Add(ft);
            }

            if (sourceTypes.Count == 0) return;

            foreach (FamilyType sourceType in sourceTypes)
            {
                try
                {
                    // Create matching type on target (reads values directly from sourceType object)
                    FamilyType newType = targetFm.NewType(sourceType.Name);
                    targetFm.CurrentType = newType;

                    // Copy values for this type
                    CopyTypeValues(sourceFm, targetFm, sourceType, paramMap, sourceParamsWithFormulas);
                }
                catch (Exception ex)
                {
                    Logging.Logger.Instance.Log($"  Could not copy type '{sourceType.Name}': {ex.Message}");
                }
            }
        }

        private static FamilyParameter? FindParameterByName(FamilyManager fm, string name)
        {
            foreach (FamilyParameter fp in fm.Parameters)
            {
                if (string.Equals(fp.Definition.Name, name, StringComparison.OrdinalIgnoreCase))
                    return fp;
            }
            return null;
        }
    }
}
