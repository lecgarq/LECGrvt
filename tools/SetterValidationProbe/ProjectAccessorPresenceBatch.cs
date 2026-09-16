using NUnit.Framework;

namespace LECG.SetterValidationProbe;

public sealed class ProjectAccessorPresenceBatch : SetterHarness
{
    protected override string ManifestName => "project-accessor-presence-manifest.json";
    protected override string PreregistrationName => "project-accessor-presence-preregistration.md";
    protected override string RunKind => "project-accessor-presence-runs";
    protected override bool TypePresenceProbe => true;

    [Test]
    public void ProfileAccessorDeclaringTypes()
    {
        foreach (var model in Manifest.RootElement.GetProperty("models").EnumerateArray())
        {
            UseModel(model.GetProperty("name").GetString()!);
            Record("ProjectAccessorPresence");
        }
    }
}
