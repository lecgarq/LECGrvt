using Microsoft.CodeAnalysis;

namespace KnowledgeLab;

internal static class ApiContract
{
    private static string TypeName(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    private static string Ref(RefKind kind) => kind switch { RefKind.Ref => "ref ", RefKind.Out => "out ", RefKind.In => "in ", _ => "" };

    internal static string Describe(string member, string documentation)
    {
        IMethodSymbol method = CandidateValidator.PublicMethod(member) ?? throw new ArgumentException("API method unavailable.");
        bool transaction = documentation.Contains("ModificationOutsideTransaction", StringComparison.Ordinal) ||
            new[] { "Create", "Set", "Add", "Remove", "Delete", "Change", "Move", "Rotate", "Update", "Duplicate", "Assign", "Unassign", "Clear", "Reset", "Copy", "Convert", "Disconnect", "Connect" }
                .Any(prefix => method.Name.StartsWith(prefix, StringComparison.Ordinal));
        var parameters = method.Parameters.Select(p => Ref(p.RefKind) + TypeName(p.Type) + " @" + p.Name).ToList();
        string receiver = TypeName(method.ContainingType);
        if (!method.IsStatic) { parameters.Insert(0, receiver + " __target"); receiver = "__target"; }
        if (transaction) parameters.Insert(0, "global::Autodesk.Revit.DB.Document __document");
        string invocation = receiver + ".@" + method.Name + "(" + string.Join(", ", method.Parameters.Select(p => Ref(p.RefKind) + "@" + p.Name)) + ")";
        string body = method.ReturnsVoid ? invocation + ";" : "return " + invocation + ";";
        if (transaction)
        {
            body = "if (__document == null || __document.IsReadOnly || __document.IsModifiable) throw new global::System.InvalidOperationException(\"Requires an editable document without an active transaction.\");\n" +
                "using var __tx = new global::Autodesk.Revit.DB.Transaction(__document, \"Reviewed API operation\");\n" +
                "try {\nif (__tx.Start() != global::Autodesk.Revit.DB.TransactionStatus.Started) throw new global::System.InvalidOperationException(\"Transaction did not start.\");\n" +
                (method.ReturnsVoid ? invocation + ";\n" : "var __result = " + invocation + ";\n") +
                "if (__tx.Commit() != global::Autodesk.Revit.DB.TransactionStatus.Committed) throw new global::System.InvalidOperationException(\"Transaction did not commit.\");\n" +
                (method.ReturnsVoid ? "" : "return __result;\n") +
                "} catch { if (__tx.GetStatus() == global::Autodesk.Revit.DB.TransactionStatus.Started) __tx.RollBack(); throw; }";
        }
        string scaffold = "public static class Candidate { public static " + TypeName(method.ReturnType) + " Run(" + string.Join(", ", parameters) + ") {\n" + body + "\n} }";
        string enums = string.Join("\n", method.Parameters.Select(p => p.Type).Append(method.ReturnType).OfType<INamedTypeSymbol>()
            .Where(t => t.TypeKind == TypeKind.Enum).Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>()
            .Select(t => TypeName(t) + " allowed names: " + string.Join(", ", t.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).Select(f => f.Name))));
        return CandidateValidator.PublicSignature(member) + "\n" + enums + "\n" +
            (transaction ? "TRANSACTION POLICY: own a transaction using the supplied scaffold. Conservative research policy, not a proven runtime requirement. Caller must supply the SAME document that owns all target elements.\n" : "TRANSACTION POLICY: none in this scaffold; actual read/write behavior still needs manual review.\n") +
            "CONSTRAINED SCAFFOLD:\n" + scaffold;
    }
}
