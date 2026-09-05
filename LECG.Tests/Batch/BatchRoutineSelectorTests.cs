using FluentAssertions;

namespace LECG.Tests.Batch;

public class BatchRoutineSelectorTests
{
    [Fact]
    public void Constructor_DefaultsToFirstAvailableRoutine()
    {
        Type selectorType = Type.GetType("LECG.Batch.Services.BatchRoutineSelector, LECG", throwOnError: true)!;
        Type routineInterfaceType = Type.GetType("LECG.Batch.Services.Interfaces.IBatchJobRoutine, LECG", throwOnError: true)!;
        Type defaultRoutineType = Type.GetType("LECG.Batch.Services.DefaultBatchJobRoutine, LECG", throwOnError: true)!;
        Type publishRoutineType = Type.GetType("LECG.Batch.Services.PublishToCloudJobRoutine, LECG", throwOnError: true)!;

        object defaultRoutine = Activator.CreateInstance(defaultRoutineType)!;
        object publishRoutine = Activator.CreateInstance(publishRoutineType)!;
        Array routines = Array.CreateInstance(routineInterfaceType, 2);
        routines.SetValue(defaultRoutine, 0);
        routines.SetValue(publishRoutine, 1);

        object selector = Activator.CreateInstance(selectorType, routines)!;

        selectorType.GetProperty("Selected")!.GetValue(selector).Should().BeSameAs(defaultRoutine);
        selectorType.GetProperty("Name")!.GetValue(selector).Should().Be("Default");
        selectorType.GetProperty("Description")!.GetValue(selector).Should().Be("No additional processing. Opens, syncs/saves, and optionally publishes.");
    }

    [Fact]
    public void Name_UsesExplicitlySelectedRoutine()
    {
        Type selectorType = Type.GetType("LECG.Batch.Services.BatchRoutineSelector, LECG", throwOnError: true)!;
        Type routineInterfaceType = Type.GetType("LECG.Batch.Services.Interfaces.IBatchJobRoutine, LECG", throwOnError: true)!;
        Type defaultRoutineType = Type.GetType("LECG.Batch.Services.DefaultBatchJobRoutine, LECG", throwOnError: true)!;
        Type publishRoutineType = Type.GetType("LECG.Batch.Services.PublishToCloudJobRoutine, LECG", throwOnError: true)!;

        object defaultRoutine = Activator.CreateInstance(defaultRoutineType)!;
        object publishRoutine = Activator.CreateInstance(publishRoutineType)!;
        Array routines = Array.CreateInstance(routineInterfaceType, 2);
        routines.SetValue(defaultRoutine, 0);
        routines.SetValue(publishRoutine, 1);

        object selector = Activator.CreateInstance(selectorType, routines)!;
        selectorType.GetProperty("Selected")!.SetValue(selector, publishRoutine);

        selectorType.GetProperty("Name")!.GetValue(selector).Should().Be("Publish To Cloud");
    }
}
