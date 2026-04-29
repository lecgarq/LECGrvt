using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Core;
using LECG.Models;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Services
{
    public class FilterCopyService : IFilterCopyService
    {
        private readonly ITransactionService _transactionService;

        public FilterCopyService(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        public Result<int> Apply(Document doc, IReadOnlyList<FilterCopyViewState> viewStates)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(viewStates);

            try
            {
                int updatedViews = _transactionService.Run(doc, "Filter Copy", _ =>
                {
                    int processed = 0;

                    foreach (FilterCopyViewState viewState in viewStates)
                    {
                        View? view = doc.GetElement(viewState.ViewId) as View;
                        if (view == null)
                        {
                            continue;
                        }

                        foreach (FilterCopyFilterState filterState in viewState.Filters.Where(f => f.ShouldRemove))
                        {
                            if (view.IsFilterApplied(filterState.FilterId))
                            {
                                view.RemoveFilter(filterState.FilterId);
                            }
                        }

                        foreach (FilterCopyFilterState filterState in viewState.Filters.Where(f => !f.ShouldRemove))
                        {
                            if (view.IsFilterApplied(filterState.FilterId))
                            {
                                view.RemoveFilter(filterState.FilterId);
                            }

                            view.AddFilter(filterState.FilterId);
                            view.SetFilterOverrides(filterState.FilterId, filterState.GraphicsSettings);
                            view.SetFilterVisibility(filterState.FilterId, filterState.IsVisible);
                        }

                        processed++;
                    }

                    return processed;
                });

                return Result<int>.Success(updatedViews);
            }
            catch (ArgumentException ex)
            {
                return Result<int>.Failure(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Result<int>.Failure(ex.Message);
            }
            catch (RevitExceptions.InvalidOperationException ex)
            {
                return Result<int>.Failure(ex.Message);
            }
        }
    }
}
