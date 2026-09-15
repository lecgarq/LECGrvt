namespace LECG.Core.Substance;

public static class SubstanceIdentityPolicy
{
    public const string Manufacturer = "Grupo Hermosillo";
    public const string Model = "Core Innovation";
    public const string Url = "https://hermosillo.com";

    public static string Description(SubstanceMaterialEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        int k = Math.Max(1, entry.Resolution / 1024);
        return $"{entry.Category} / {entry.Slug} / Substance {k}K bake";
    }

    public static string Keywords(SubstanceMaterialEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return $"substance, pbr, {entry.Category.ToLowerInvariant()}";
    }
}
