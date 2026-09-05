using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Models;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using LECG.Views;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class PbrMaterialCreatorCommand : RevitCommand
    {
        protected override string? TransactionName => null;

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            var materialService = ServiceLocator.GetRequiredService<IMaterialService>();
            var textureLookup = ServiceLocator.GetRequiredService<IMaterialTextureLookupService>();
            var viewModel = new PbrMaterialCreatorViewModel(textureLookup);
            var view = ServiceLocator.CreateWith<PbrMaterialCreatorView>(viewModel);
            view.Initialize(uiDoc);

            bool? result = view.ShowDialog();
            if (result != true || !viewModel.ShouldRun || !viewModel.CanRun)
            {
                return;
            }

            if (viewModel.BatchLibraryRoot is string libraryRoot)
            {
                var batch = new SubstanceBatchCommand();
                batch.PrepareAsSubCommand(CommandData);
                batch.Execute(uiDoc, doc, libraryRoot);
                return;
            }

            List<PbrMaterialCreateRequest> requests = viewModel.CreateRequests();
            ShowLogWindow($"Creating {requests.Count} PBR Material{(requests.Count > 1 ? "s" : "")}...");

            for (int i = 0; i < requests.Count; i++)
            {
                UpdateProgress((double)(i) / requests.Count * 100, $"Creating material {i + 1} of {requests.Count}...");
                PbrMaterialCreateRequest request = requests[i];
                Log($"[{i + 1}/{requests.Count}] CREATING MATERIAL");
                Log($"  Material: {request.MaterialName}");
                Log($"  Appearance Asset: {request.AppearanceAssetName}");
                if (!string.IsNullOrEmpty(request.Description))
                {
                    Log($"  Description: {request.Description}");
                }
                if (!string.IsNullOrEmpty(request.MaterialClass))
                {
                    Log($"  Class: {request.MaterialClass}");
                }
                Log("");

                ElementId materialId = materialService.CreatePBRMaterial(doc, request, Log);
                Material? material = doc.GetElement(materialId) as Material;

                Log(material == null
                    ? $"  DONE"
                    : $"  DONE: {material.Name}");
                Log("");
            }

            UpdateProgress(100, "Complete");
            Log($"COMPLETE: {requests.Count} material{(requests.Count > 1 ? "s" : "")} created.");
        }
    }
}
