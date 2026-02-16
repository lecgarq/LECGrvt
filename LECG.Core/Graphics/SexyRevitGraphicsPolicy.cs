namespace LECG.Core.Graphics;

public sealed record SexyRevitGraphicsSettings(
    bool UseConsistentColors,
    bool UseDetailFine);

public enum CoreDisplayStyle
{
    Wireframe,
    Realistic
}

public enum CoreDetailLevel
{
    Coarse,
    Fine
}

public sealed record SexyRevitGraphicsDecision(
    bool ShouldApply,
    CoreDisplayStyle? DisplayStyle,
    CoreDetailLevel? DetailLevel,
    IReadOnlyList<string> Messages);

public static class SexyRevitGraphicsPolicy
{
    public static SexyRevitGraphicsDecision Evaluate(SexyRevitGraphicsSettings settings)
    {
        if (!settings.UseConsistentColors)
        {
            return new SexyRevitGraphicsDecision(false, null, null, Array.Empty<string>());
        }

        var messages = new List<string>
        {
            "GRAPHICS & LIGHTING",
            "  Display Style: Realistic",
            "  Shadows/Lighting skipped (API limitation)"
        };

        CoreDetailLevel? detailLevel = null;
        if (settings.UseDetailFine)
        {
            detailLevel = CoreDetailLevel.Fine;
            messages.Add("  Detail Level: Fine");
        }

        return new SexyRevitGraphicsDecision(
            true,
            CoreDisplayStyle.Realistic,
            detailLevel,
            messages);
    }
}
