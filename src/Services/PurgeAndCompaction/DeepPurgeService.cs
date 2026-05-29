using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Core;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class DeepPurgeService : IDeepPurgeService
    {
        private readonly INativePurgeDocumentService _nativePurgeDocumentService;
        private readonly IPurgePassSequenceService _purgePassSequenceService;
        private readonly ITransactionService _transactionService;
        private readonly IFamilyLoadOptionsFactory _familyLoadOptionsFactory;

        public DeepPurgeService(
            INativePurgeDocumentService nativePurgeDocumentService,
            IPurgePassSequenceService purgePassSequenceService,
            ITransactionService transactionService,
            IFamilyLoadOptionsFactory familyLoadOptionsFactory)
        {
            _nativePurgeDocumentService = nativePurgeDocumentService;
            _purgePassSequenceService = purgePassSequenceService;
            _transactionService = transactionService;
            _familyLoadOptionsFactory = familyLoadOptionsFactory;
        }

        public void Purge(Document projectDoc, int passCount, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(projectDoc);
            ArgumentNullException.ThrowIfNull(reporter);

            List<FamilyPurgeTarget> editableFamilies = CollectEditableFamilies(projectDoc);

            reporter.Log($"Deep purge found {editableFamilies.Count} editable loaded families.");

            FamilyPurgeSummary familySummary = PurgeLoadedFamilies(projectDoc, editableFamilies, passCount, reporter);

            int totalDeletedInProject = PurgeProject(projectDoc, passCount, reporter);

            reporter.Log(string.Empty);
            reporter.Log("--- DEEP PURGE SUMMARY ---");
            reporter.Log($"Families processed: {familySummary.FamiliesProcessed}");
            reporter.Log($"Families skipped: {familySummary.FamiliesSkipped}");
            reporter.Log($"Family items purged: {familySummary.TotalDeletedInFamilies}");
            reporter.Log($"Project items purged: {totalDeletedInProject}");
            reporter.Log($"Total items purged: {familySummary.TotalDeletedInFamilies + totalDeletedInProject}");
        }

        private FamilyPurgeSummary PurgeLoadedFamilies(
            Document projectDoc,
            IReadOnlyList<FamilyPurgeTarget> editableFamilies,
            int passCount,
            IProgressReporter reporter)
        {
            int totalDeletedInFamilies = 0;
            int familiesProcessed = 0;
            int familiesSkipped = 0;

            for (int familyIndex = 0; familyIndex < editableFamilies.Count; familyIndex++)
            {
                double familyProgress = editableFamilies.Count == 0
                    ? 70
                    : (familyIndex / (double)editableFamilies.Count) * 70d;

                FamilyPurgeTarget family = editableFamilies[familyIndex];
                reporter.Report($"Processing family {familyIndex + 1}/{editableFamilies.Count}: {family.Name}", familyProgress);

                bool processed = TryPurgeFamily(projectDoc, family, passCount, reporter, out int familyDeleted);
                totalDeletedInFamilies += familyDeleted;

                if (processed)
                {
                    familiesProcessed++;
                }
                else
                {
                    familiesSkipped++;
                }
            }

            return new FamilyPurgeSummary(totalDeletedInFamilies, familiesProcessed, familiesSkipped);
        }

        private int PurgeProject(Document projectDoc, int passCount, IProgressReporter reporter)
        {
            reporter.Log(string.Empty);
            reporter.Log("--- PROJECT PURGE ---");

            SafeFailureHandler failureHandler = new SafeFailureHandler();
            return RunPurgePassSequence(
                projectDoc,
                passCount,
                reporter,
                failureHandler,
                passNumber =>
                {
                    reporter.Report($"Project purge pass {passNumber}/{passCount}", 70 + (passNumber / (double)passCount * 30d));
                },
                passNumber => $"Deep Purge Project - Pass {passNumber}",
                (passNumber, deletedThisPass) =>
                {
                    reporter.Log($"  Project pass {passNumber}: purged {deletedThisPass} items.");
                });
        }

        private bool TryPurgeFamily(Document projectDoc, FamilyPurgeTarget familyTarget, int passCount, IProgressReporter reporter, out int totalDeleted)
        {
            totalDeleted = 0;
            int deletedInFamily = 0;
            Document? familyDoc = null;

            try
            {
                Family? family = projectDoc.GetElement(familyTarget.Id) as Family;
                if (family == null || !family.IsValidObject || !family.IsEditable)
                {
                    reporter.LogWarning($"  Skipped '{familyTarget.Name}': family is no longer available for editing.");
                    return false;
                }

                familyDoc = projectDoc.EditFamily(family);
                if (familyDoc == null)
                {
                    reporter.LogWarning($"  Skipped '{familyTarget.Name}': could not open family document.");
                    return false;
                }

                SafeFailureHandler failureHandler = new SafeFailureHandler();
                deletedInFamily = RunPurgePassSequence(
                    familyDoc,
                    passCount,
                    reporter,
                    failureHandler,
                    _ => { },
                    passNumber => $"Deep Purge Family - Pass {passNumber}",
                    (passNumber, deletedThisPass) =>
                    {
                        reporter.Log($"  {familyTarget.Name} pass {passNumber}: purged {deletedThisPass} items.");
                    });

                if (deletedInFamily > 0)
                {
                    ReloadPurgedFamily(projectDoc, familyDoc, familyTarget.Name, reporter);
                }
                else
                {
                    reporter.Log($"  No purgeable content found in '{familyTarget.Name}'.");
                }

                totalDeleted = deletedInFamily;
                return true;
            }
            catch (Exception ex) when (IsExpectedDeepPurgeException(ex))
            {
                totalDeleted = deletedInFamily;
                reporter.LogWarning($"  Skipped '{familyTarget.Name}': {ex.Message}");
                return false;
            }
            finally
            {
                TryCloseFamilyDocument(familyDoc, familyTarget.Name, reporter);
            }
        }

        private int RunPurgePassSequence(
            Document doc,
            int passCount,
            IProgressReporter reporter,
            SafeFailureHandler failureHandler,
            Action<int> reportPass,
            Func<int, string> transactionNameFactory,
            Action<int, int> logDeleted)
        {
            int totalDeleted = 0;

            foreach (int passNumber in _purgePassSequenceService.GetPasses(passCount))
            {
                reportPass(passNumber);
                int deletedThisPass = 0;
                _transactionService.RunWithWarningHandler(doc, transactionNameFactory(passNumber), currentDoc =>
                {
                    deletedThisPass = _nativePurgeDocumentService.PurgeUnused(currentDoc, reporter);
                    totalDeleted += deletedThisPass;
                    logDeleted(passNumber, deletedThisPass);
                }, failureHandler);

                // Revit's native purge is idempotent: once a pass removes nothing, no later
                // pass will either. Stop early to avoid needless transactions/regens (and, for
                // families, needless edit/reload churn).
                if (deletedThisPass == 0)
                {
                    break;
                }
            }

            return totalDeleted;
        }

        private void ReloadPurgedFamily(Document projectDoc, Document familyDoc, string familyName, IProgressReporter reporter)
        {
            EventHandler<Autodesk.Revit.DB.Events.FailuresProcessingEventArgs>? reloadFailureHandler = null;
            try
            {
                reloadFailureHandler = CreateReloadFailureHandler();
                projectDoc.Application.FailuresProcessing += reloadFailureHandler;
                familyDoc.LoadFamily(projectDoc, _familyLoadOptionsFactory.Create());
                reporter.Log($"  Reloaded '{familyName}' into project.");
            }
            catch (Exception reloadEx) when (IsExpectedDeepPurgeException(reloadEx))
            {
                reporter.LogWarning($"  Could not reload '{familyName}': {reloadEx.Message}");
            }
            finally
            {
                if (reloadFailureHandler != null)
                {
                    projectDoc.Application.FailuresProcessing -= reloadFailureHandler;
                }
            }
        }

        private static EventHandler<Autodesk.Revit.DB.Events.FailuresProcessingEventArgs> CreateReloadFailureHandler()
        {
            return (sender, args) =>
            {
                FailuresAccessor accessor = args.GetFailuresAccessor();
                foreach (FailureMessageAccessor fma in accessor.GetFailureMessages())
                {
                    if (fma.GetSeverity() == FailureSeverity.Warning)
                    {
                        accessor.DeleteWarning(fma);
                    }
                }

                if (accessor.GetFailureMessages().Count > 0)
                {
                    args.SetProcessingResult(FailureProcessingResult.ProceedWithRollBack);
                }
            };
        }

        private static bool IsExpectedDeepPurgeException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.ArgumentException
                || ex is RevitExceptions.InvalidOperationException;
        }

        private static void TryCloseFamilyDocument(Document? familyDoc, string familyName, IProgressReporter reporter)
        {
            if (familyDoc == null || !familyDoc.IsValidObject)
            {
                return;
            }

            try
            {
                familyDoc.Close(false);
            }
            catch (Exception ex) when (IsExpectedDeepPurgeException(ex))
            {
                reporter.LogWarning($"  Could not close '{familyName}': {ex.Message}");
            }
        }

        private static List<FamilyPurgeTarget> CollectEditableFamilies(Document projectDoc)
        {
            return new FilteredElementCollector(projectDoc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Where(family => family.IsEditable)
                .Select(family => new FamilyPurgeTarget(family.Id, family.Name))
                .OrderBy(family => family.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private sealed record FamilyPurgeSummary(int TotalDeletedInFamilies, int FamiliesProcessed, int FamiliesSkipped);
        private sealed record FamilyPurgeTarget(ElementId Id, string Name);
    }
}
