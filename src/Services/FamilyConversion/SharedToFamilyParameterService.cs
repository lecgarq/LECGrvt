using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    /// <summary>
    /// Bulk converter that turns shared family parameters into ordinary (non-shared)
    /// family parameters. See <see cref="ISharedToFamilyParameterService"/> for the contract.
    /// </summary>
    public class SharedToFamilyParameterService : ISharedToFamilyParameterService
    {
        private const string Scope = "SharedToFamilyParameter";

        private readonly ITransactionService _transactionService;
        private readonly ILogger _logger;

        public SharedToFamilyParameterService(ITransactionService transactionService, ILogger logger)
        {
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public SharedToFamilyParameterResult ConvertFamilies(Document projectDoc, IEnumerable<Family> families)
        {
            ArgumentNullException.ThrowIfNull(projectDoc);
            ArgumentNullException.ThrowIfNull(families);

            var result = new SharedToFamilyParameterResult();

            // De-duplicate by id so selecting many instances of the same family processes it once.
            var uniqueFamilies = families
                .Where(f => f != null)
                .GroupBy(f => f.Id)
                .Select(g => g.First())
                .ToList();

            foreach (Family family in uniqueFamilies)
            {
                ProcessSingleFamily(projectDoc, family, result);
            }

            return result;
        }

        private void ProcessSingleFamily(Document projectDoc, Family family, SharedToFamilyParameterResult result)
        {
            string familyName = SafeName(family);

            if (!family.IsEditable)
            {
                _logger.Log($"  [SKIP] '{familyName}' is not editable (system or in-place family).", scope: Scope);
                result.FamiliesSkipped++;
                return;
            }

            Document familyDoc = null!;
            try
            {
                familyDoc = projectDoc.EditFamily(family);
                if (familyDoc == null)
                {
                    _logger.Log($"  [SKIP] Could not open '{familyName}' in the family editor.", scope: Scope);
                    result.FamiliesSkipped++;
                    return;
                }

                result.FamiliesProcessed++;

                FamilyManager fm = familyDoc.FamilyManager;
                List<FamilyParameter> sharedParams = GetConvertibleSharedParameters(fm);

                if (sharedParams.Count == 0)
                {
                    _logger.Log($"  [SKIP] '{familyName}' has no shared parameters.", scope: Scope);
                    result.FamiliesSkipped++;
                    return;
                }

                int convertedInThisFamily = 0;

                _transactionService.Run(familyDoc, "Convert Shared to Family Parameters", _ =>
                {
                    foreach (FamilyParameter fp in sharedParams)
                    {
                        string paramName = fp.Definition.Name;
                        try
                        {
                            // String-name overload of ReplaceParameter creates a NON-shared
                            // family parameter, preserving name, group, type, instance/type
                            // binding, formulas and label associations. The only change is
                            // dropping the shared (GUID-backed) identity.
                            ForgeTypeId groupId = fp.Definition.GetGroupTypeId();
                            bool isInstance = fp.IsInstance;
                            fm.ReplaceParameter(fp, paramName, groupId, isInstance);
                            convertedInThisFamily++;
                            _logger.Log($"    [OK] '{paramName}' -> family parameter ({(isInstance ? "instance" : "type")}).", scope: Scope);
                        }
                        catch (Exception ex) when (IsExpectedConversionException(ex))
                        {
                            _logger.LogWarning($"    [WARN] Could not convert '{paramName}' in '{familyName}': {ex.Message}", scope: Scope);
                        }
                    }
                });

                if (convertedInThisFamily == 0)
                {
                    _logger.Log($"  [SKIP] '{familyName}': no parameters could be converted.", scope: Scope);
                    result.FamiliesSkipped++;
                    return;
                }

                // Reload into the project WITHOUT overwriting parameter values, so that
                // values already set on placed instances/types survive the reload.
                var options = new PreserveValuesLoadOptions();
                familyDoc.LoadFamily(projectDoc, options);

                result.FamiliesModified++;
                result.ParametersConverted += convertedInThisFamily;
                _logger.Log($"  [DONE] '{familyName}': converted {convertedInThisFamily} shared parameter(s) and reloaded.", scope: Scope);
            }
            catch (Exception ex) when (IsExpectedConversionException(ex))
            {
                _logger.LogError($"  [FAIL] '{familyName}': {ex.Message}", scope: Scope);
                result.FamiliesFailed++;
            }
            finally
            {
                TryCloseFamilyDocument(familyDoc, familyName);
            }
        }

        private static List<FamilyParameter> GetConvertibleSharedParameters(FamilyManager fm)
        {
            var list = new List<FamilyParameter>();
            foreach (FamilyParameter fp in fm.Parameters)
            {
                // Shared parameters always carry a positive element id; the IsShared flag is
                // the authoritative test. Built-in params (negative id) are never shared.
                if (fp.IsShared && fp.Id.Value > 0)
                    list.Add(fp);
            }
            return list;
        }

        private void TryCloseFamilyDocument(Document? familyDoc, string familyName)
        {
            if (familyDoc == null || !familyDoc.IsValidObject)
                return;

            try
            {
                familyDoc.Close(false);
            }
            catch (Exception ex) when (IsExpectedConversionException(ex))
            {
                _logger.LogWarning($"  Could not close family document for '{familyName}': {ex.Message}", scope: Scope);
            }
        }

        private static string SafeName(Family family)
        {
            try
            {
                return family.Name;
            }
            catch (Exception ex) when (IsExpectedConversionException(ex))
            {
                return "<unknown family>";
            }
        }

        private static bool IsExpectedConversionException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.ArgumentException
                || ex is RevitExceptions.InvalidOperationException;
        }

        /// <summary>
        /// Load options that keep the family's existing parameter VALUES in the project
        /// instead of resetting them to the family's defaults. This is the key difference
        /// from <see cref="FamilyLoadOptionsFactory"/>, which overwrites values.
        /// </summary>
        private sealed class PreserveValuesLoadOptions : IFamilyLoadOptions
        {
            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
            {
                overwriteParameterValues = false;
                return true;
            }

            public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
            {
                source = FamilySource.Family;
                overwriteParameterValues = false;
                return true;
            }
        }
    }
}
