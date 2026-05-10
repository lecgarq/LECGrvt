// Wave 0 skip-gated RED scaffold — plan 05-02 flips these GREEN.
// Covers SearchReplaceService facade delegation and constructor null-guards (REQ-05).
using LECG.Services;
using Xunit;

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
    // Wave 2 RED rows — plan 05-02 owns implementation
    // -----------------------------------------------------------------------

    [Fact(Skip = "Implemented by plan 05-02: CollectBaseElements delegates to IBaseElementCollectionService")]
    public void CollectBaseElements_DelegatesToBaseElementCollectionService()
    {
        Assert.True(false, "see plan 05-02");
    }

    [Fact(Skip = "Implemented by plan 05-02: GetUniqueCategories delegates to preview service and returns its result")]
    public void GetUniqueCategories_DelegatesToCollectionService_AndReturnsItsResult()
    {
        Assert.True(false, "see plan 05-02");
    }

    [Fact(Skip = "Implemented by plan 05-02: ProcessPreview delegates to preview service and passes CancellationToken")]
    public void ProcessPreview_DelegatesToPreviewService_AndPassesCancellationToken()
    {
        Assert.True(false, "see plan 05-02");
    }

    [Fact(Skip = "Implemented by plan 05-02: ExecuteBatchRename IProgressReporter overload delegates to execution service")]
    public void ExecuteBatchRename_ProgressReporterOverload_DelegatesToExecutionService()
    {
        Assert.True(false, "see plan 05-02");
    }

    [Fact(Skip = "Implemented by plan 05-02: ExecuteBatchRename Action callback overload delegates to execution service")]
    public void ExecuteBatchRename_ActionCallbackOverload_DelegatesToExecutionService()
    {
        Assert.True(false, "see plan 05-02");
    }

    [Fact(Skip = "Implemented by plan 05-02: constructor null-guards for all 3 constructor arguments")]
    public void Constructor_NullDependency_Throws()
    {
        Assert.True(false, "see plan 05-02");
    }
}
