using System.Collections.Generic;
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Services;

namespace LECG.Commands
{
    /// <summary>
    /// Command to clean third-party extensible storage schemas from the project.
    /// Removes Environment plugin data and breaks Toposolid/Reference Surface links.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CleanSchemasCommand : RevitCommand
    {
        // No automatic transaction needed for initial scan
        protected override string? TransactionName => null;

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            // Show Log Window
            ShowLogWindow("Schema Cleaner");

            Log("Environment Schema Cleaner");
            Log("==========================");
            Log("");
            Log("Removes third-party plugin data from the project.");
            Log("");

            var cleaner = ServiceLocator.GetRequiredService<ISchemaCleanerService>();
            
            // STEP 1: Scan for schemas
            Log("STEP 1: Scanning project for third-party schemas...");
            UpdateProgress(10, "Scanning...");

            HashSet<System.Guid> elementSchemas = cleaner.ScanForThirdPartySchemas(doc, Log);

            // STEP 2: Scan DataStorage elements
            Log("");
            Log("STEP 2: Scanning DataStorage elements...");
            UpdateProgress(30, "Scanning DataStorage...");

            var (dsSchemas, dataStorageIds) = cleaner.ScanDataStorageElements(doc, Log);
            
            // Merge all schemas
            foreach (var guid in dsSchemas) elementSchemas.Add(guid);

            Log($"");
            Log($"Total schemas found: {elementSchemas.Count}");
            Log($"DataStorage elements to delete: {dataStorageIds.Count}");

            if (elementSchemas.Count == 0)
            {
                Log("");
                Log("✓ No third-party schemas found! Project is clean.");
                UpdateProgress(100, "Done - No schemas found");
                return;
            }

            // STEP 3: Delete DataStorage elements
            Log("");
            Log("STEP 3: Deleting DataStorage elements...");
            UpdateProgress(50, "Deleting DataStorage...");

            int dataStoragesDeleted = 0;
            using (Transaction t1 = new Transaction(doc, "Delete DataStorage"))
            {
                t1.Start();
                dataStoragesDeleted = cleaner.DeleteDataStorageElements(doc, dataStorageIds);
                t1.Commit();
                Log($"  Deleted {dataStoragesDeleted} DataStorage elements.");
            }

            // STEP 4: Erase schemas
            Log("");
            Log("STEP 4: Erasing schemas from document...");
            UpdateProgress(70, "Erasing schemas...");

            int schemasErased = 0;
            using (Transaction t2 = new Transaction(doc, "Erase Schemas"))
            {
                t2.Start();
                schemasErased = cleaner.EraseSchemas(doc, elementSchemas, Log);
                t2.Commit();
            }

            // Done
            UpdateProgress(100, "Complete!");
            Log("");
            Log("=== COMPLETED ===");
            Log($"DataStorage deleted: {dataStoragesDeleted}");
            Log($"Schemas erased: {schemasErased}");
            Log("");
            Log("✓ Save and reopen the file to apply changes.");
        }
    }
}
