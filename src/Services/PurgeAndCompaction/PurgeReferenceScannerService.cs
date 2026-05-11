using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class PurgeReferenceScannerService : IPurgeReferenceScannerService
    {
        private readonly ILogger _logger;

        public PurgeReferenceScannerService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void AddIfValid(HashSet<ElementId> set, ElementId id)
        {
            ArgumentNullException.ThrowIfNull(set);
            ArgumentNullException.ThrowIfNull(id);

            if (id != ElementId.InvalidElementId)
            {
                set.Add(id);
            }
        }

        public void CollectUsedIds(Element elem, HashSet<ElementId> validIds, HashSet<ElementId> usedIds)
        {
            ArgumentNullException.ThrowIfNull(elem);
            ArgumentNullException.ThrowIfNull(validIds);
            ArgumentNullException.ThrowIfNull(usedIds);

            try
            {
                foreach (Parameter param in elem.Parameters)
                {
                    if (param.StorageType == StorageType.ElementId)
                    {
                        ElementId id = param.AsElementId();
                        if (validIds.Contains(id))
                        {
                            usedIds.Add(id);
                        }
                    }
                }
            }
            catch (Exception ex) when (IsExpectedReferenceScanException(ex))
            {
                _logger.LogWarning($"Failed to collect used IDs for element {elem.Id}: {ex.Message}", scope: "PurgeReferenceScanner");
            }
        }

        private static bool IsExpectedReferenceScanException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.ArgumentException
                || ex is RevitExceptions.InvalidOperationException;
        }
    }
}
