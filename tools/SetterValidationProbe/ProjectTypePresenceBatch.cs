using NUnit.Framework;

namespace LECG.SetterValidationProbe;

public sealed class ProjectTypePresenceBatch : SetterHarness
{
    protected override string ManifestName => "project-type-presence-manifest.json";
    protected override string PreregistrationName => "project-type-presence-preregistration.md";
    protected override string RunKind => "project-type-presence-runs";
    protected override bool TypePresenceProbe => true;

    [Test]
    public void ProfileSetterDeclaringTypes()
    {
        foreach (var model in Manifest.RootElement.GetProperty("models").EnumerateArray())
        {
            UseModel(model.GetProperty("name").GetString()!);
            Record("ProjectTypePresence");
        }
    }
}
