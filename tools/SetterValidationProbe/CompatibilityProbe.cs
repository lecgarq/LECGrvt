using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using NUnit.Framework;

[assembly: AssemblyMetadata("NUnit.Version", "2026")]
[assembly: AssemblyMetadata("NUnit.Open", "true")]
[assembly: AssemblyMetadata("NUnit.Close", "true")]
[assembly: AssemblyMetadata("NUnit.Timeout", "45")]
[assembly: AssemblyMetadata("NUnit.Verbosity", "2")]

namespace LECG.SetterValidationProbe;

// Compatibility gate only. No documents opened, transactions started or setters invoked.
public sealed class CompatibilityProbe
{
    [Test]
    public void SnapshotIncludesAllTargetValuesAndOnlyWritableNonTargetValues()
    {
        Assert.That(SetterHarness.IncludeParameterValue(true, true), Is.True);
        Assert.That(SetterHarness.IncludeParameterValue(true, false), Is.True);
        Assert.That(SetterHarness.IncludeParameterValue(false, false), Is.True);
        Assert.That(SetterHarness.IncludeParameterValue(false, true), Is.False);
        double recomputedFarClip = 1 + 1e-14;
        Assert.That(SetterHarness.NormalizeParameterDouble((long)BuiltInParameter.VIEWER_BOUND_OFFSET_FAR, recomputedFarClip),
            Is.EqualTo(Math.Round(recomputedFarClip, 12, MidpointRounding.ToEven)));
        Assert.That(SetterHarness.NormalizeParameterDouble((long)BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS, recomputedFarClip),
            Is.EqualTo(recomputedFarClip));
    }
    private UIApplication? _application;

    [OneTimeSetUp]
    public void Setup(UIApplication application) => _application = application;

    [Test]
    public void RunsInsideRevit2026OnNet10WithoutModels()
    {
        Assert.That(_application, Is.Not.Null, "Runner must inject a real Revit UIApplication.");
        var application = _application!.Application;
        Assert.That(Process.GetCurrentProcess().ProcessName, Is.EqualTo("Revit").IgnoreCase);
        Assert.That(application.VersionNumber, Is.EqualTo("2026"));
        Assert.That(Environment.Version.Major, Is.EqualTo(10));
        Assert.That(application.Documents.Size, Is.EqualTo(0), "No model may be opened by this gate.");
        TestContext.Progress.WriteLine($"Revit={application.VersionNumber}; Build={application.VersionBuild}; " +
            $"Runtime={RuntimeInformation.FrameworkDescription}; PID={Environment.ProcessId}; Documents=0; " +
            $"RevitAPI={typeof(Autodesk.Revit.DB.Element).Assembly.FullName}; " +
            $"TestBinary={Assembly.GetExecutingAssembly().Location}");
    }
}
