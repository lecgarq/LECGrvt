// Wave 0 skip-gated RED scaffold — plan 05-02 flips these GREEN.
// Covers SearchReplaceService facade delegation and constructor null-guards (REQ-05).
using System;
using System.Collections.Generic;
using System.Threading;
using FluentAssertions;
using LECG.Models;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.Services.TestHelpers;
using LECG.ViewModels;
using LECG.ViewModels.Components;
using NSubstitute;
using Xunit;

// Facade delegation tests use internal FakeXxx classes from LECG.Services.TestHelpers
// (via InternalsVisibleTo). Those fakes live in the production LECG project so they can
// implement interfaces that reference Autodesk.Revit.DB.Document without requiring a direct
// RevitAPI.dll reference in this test project (Phase 04-03 decision pattern extended).

namespace LECG.Tests.Services;

/// <summary>
/// Wave 2 RED anchor for SearchReplaceService facade coverage.
/// Behavioural tests are skip-gated naming plan 05-02.
/// Un-skipped by plan 05-02.
/// </summary>
[Trait("Category", "Renaming")]
public class SearchReplaceServiceTests
{
    // -----------------------------------------------------------------------
    // Anchor — keeps --filter discovery working even when all behavioural
    // tests are skip-gated
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "anchor — fixture exists; behavioural tests skip-gated by implementing plan")]
    public void Fixture_Anchor_Exists()
    {
        Assert.NotNull(typeof(SearchReplaceService));
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static (
        SearchReplaceService sut,
        FakeSearchReplacePreviewService preview,
        FakeBatchRenameExecutionService execution,
        FakeBaseElementCollectionService collection) Build()
    {
        var preview = new FakeSearchReplacePreviewService();
        var execution = new FakeBatchRenameExecutionService();
        var collection = new FakeBaseElementCollectionService();
        var sut = new SearchReplaceService(preview, execution, collection);
        return (sut, preview, execution, collection);
    }

    private static RenameRuleContext MakeContext() => new RenameRuleContext(
        new ReplaceRule(), new RemoveRule(), new AddRule(), new NumberingRule(), new CaseRule(),
        ScopeTypeName: false, ScopeFamilyName: false, ScopeViewName: false, ScopeSheetName: false,
        ScopeMaterialName: false, ScopeObjectStyleName: false, ScopeLineStyleName: false,
        ScopeFillPatternName: false, ScopeFamilyParameterName: false,
        FilterName: "", FilterCategory: "", SelectedFilterType: SearchFilterType.Contains,
        FilterParamGroup: "", FilterIsInstance: null, FilterIsReadOnly: null, FilterViewType: "");

    // -----------------------------------------------------------------------
    // Wave 2 GREEN rows — plan 05-02 facade delegation
    // -----------------------------------------------------------------------

    // CollectBaseElements takes Autodesk.Revit.DB.Document as first argument.
    // RevitAPI.dll has native-code dependencies available only inside Revit, so calling
    // any method with a Document parameter in the xUnit runner triggers a load failure.
    // Delegation is verified by manual Revit session (see 05-VERIFICATION.md §manual).
    [Fact(Skip = "Document-parametered method — RevitAPI native deps unavailable in xUnit runner; see 05-VERIFICATION.md")]
    public void CollectBaseElements_DelegatesToBaseElementCollectionService()
    {
        var (sut, _, _, collection) = Build();
        var expected = new List<ElementData> { new ElementData { Name = "TypeA" } };
        collection.NextReturn = expected;

        var actual = sut.CollectBaseElements(
            doc: null!, types: true, families: false, views: false, sheets: false,
            materials: false, objectStyles: false, lineStyles: false, fillPatterns: false,
            familyParameters: false);

        actual.Should().BeSameAs(expected, because: "facade must return the collection service result unchanged");
    }

    [Fact]
    public void GetUniqueCategories_DelegatesToCollectionService_AndReturnsItsResult()
    {
        var (sut, preview, _, _) = Build();
        var expected = new List<string> { "Walls", "Doors" };
        preview.NextCategories = expected;

        var actual = sut.GetUniqueCategories(new List<ElementData>());

        actual.Should().BeSameAs(expected, because: "facade must return the preview service result unchanged");
    }

    [Fact]
    public void ProcessPreview_DelegatesToPreviewService_AndPassesCancellationToken()
    {
        var (sut, preview, _, _) = Build();
        var expectedRows = new List<ElementRowViewModel>
        {
            new ElementRowViewModel { Name = "TypeA", NewValue = "TypeB" }
        };
        preview.NextPreview = expectedRows;

        var cts = new CancellationTokenSource();
        var actual = sut.ProcessPreview(new List<ElementData>(), new SearchCriteria(), MakeContext(), cts.Token);

        actual.Should().BeSameAs(expectedRows, because: "facade must return the preview service result unchanged");
        preview.LastToken.Should().Be(cts.Token, because: "CancellationToken must be forwarded unchanged");
    }

    // ExecuteBatchRename overloads take Autodesk.Revit.DB.Document as first argument.
    // RevitAPI.dll has native-code dependencies available only inside Revit, so calling
    // any method with a Document parameter in the xUnit runner triggers a load failure.
    // Delegation is verified by manual Revit session (see 05-VERIFICATION.md §manual).
    [Fact(Skip = "Document-parametered method — RevitAPI native deps unavailable in xUnit runner; see 05-VERIFICATION.md")]
    public void ExecuteBatchRename_ProgressReporterOverload_DelegatesToExecutionService()
    {
        var (sut, _, execution, _) = Build();
        execution.NextCount = 7;

        var reporter = new SimpleProgressReporter(_ => { });
        var logger = Substitute.For<LECG.Services.Logging.ILogger>();

        var actual = sut.ExecuteBatchRename(null!, new List<ElementRowViewModel>(), logger, reporter);

        actual.Should().Be(7, because: "facade must return the execution service count unchanged");
        execution.LastReporter.Should().BeSameAs(reporter, because: "reporter arg must be forwarded");
    }

    [Fact(Skip = "Document-parametered method — RevitAPI native deps unavailable in xUnit runner; see 05-VERIFICATION.md")]
    public void ExecuteBatchRename_ActionCallbackOverload_DelegatesToExecutionService()
    {
        var (sut, _, execution, _) = Build();
        execution.NextCount = 3;

        Action<double, string> callback = (_, _) => { };
        var logger = Substitute.For<LECG.Services.Logging.ILogger>();

        var actual = sut.ExecuteBatchRename(null!, new List<ElementRowViewModel>(), logger, callback);

        actual.Should().Be(3, because: "facade must return the execution service count unchanged");
        execution.LastCallback.Should().BeSameAs(callback, because: "callback arg must be forwarded");
    }

    // -----------------------------------------------------------------------
    // Constructor null-guards: 3 separate assertions (one per ctor argument)
    // -----------------------------------------------------------------------

    [Fact]
    public void Constructor_NullDependency_Throws()
    {
        // Fakes are concrete classes — passing null! does not require RevitAPI.dll proxy.
        var preview = new FakeSearchReplacePreviewService();
        var execution = new FakeBatchRenameExecutionService();
        var collection = new FakeBaseElementCollectionService();

        Action nullPreview = () => new SearchReplaceService(
            searchReplacePreviewService: null!,
            batchRenameExecutionService: execution,
            baseElementCollectionService: collection);

        Action nullExecution = () => new SearchReplaceService(
            searchReplacePreviewService: preview,
            batchRenameExecutionService: null!,
            baseElementCollectionService: collection);

        Action nullCollection = () => new SearchReplaceService(
            searchReplacePreviewService: preview,
            batchRenameExecutionService: execution,
            baseElementCollectionService: null!);

        nullPreview.Should().Throw<ArgumentNullException>()
            .WithParameterName("searchReplacePreviewService");

        nullExecution.Should().Throw<ArgumentNullException>()
            .WithParameterName("batchRenameExecutionService");

        nullCollection.Should().Throw<ArgumentNullException>()
            .WithParameterName("baseElementCollectionService");
    }
}
