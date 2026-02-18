namespace LECG.Core.Naming;

public static class DetailFamilyNamePolicy
{
    public static string FromTypeName(string typeName)
    {
        ArgumentNullException.ThrowIfNull(typeName);
        return typeName.Replace(".dwg", "").Replace(".pdf", "") + "_Detail";
    }
}
