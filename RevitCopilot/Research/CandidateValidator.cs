using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KnowledgeLab;

internal sealed record Validation(bool SchemaValid, bool Compiles, bool ReferencesExpectedMember, bool RestrictedCode, string Status, string[] Errors);

internal static class CandidateValidator
{
    private static readonly Lazy<MetadataReference[]> References = new(() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Concat(new[] { Path.Combine(QuestionBank.ApiDirectory, "RevitAPI.dll"), Path.Combine(QuestionBank.ApiDirectory, "RevitAPIUI.dll") })
            .Distinct(StringComparer.OrdinalIgnoreCase).Select(p => MetadataReference.CreateFromFile(p)).ToArray());

    private static readonly Lazy<CSharpCompilation> ApiCompilation = new(() => CSharpCompilation.Create("ApiReference", references: References.Value));

    internal static IMethodSymbol? PublicMethod(string member)
    {
        if (DocumentationCommentId.GetFirstSymbolForDeclarationId(member, ApiCompilation.Value) is not IMethodSymbol method ||
            method.DeclaredAccessibility != Accessibility.Public || method.IsGenericMethod ||
            method.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.ObsoleteAttribute")) return null;
        for (INamedTypeSymbol? type = method.ContainingType; type is not null; type = type.ContainingType)
            if (type.DeclaredAccessibility != Accessibility.Public) return null;
        return method;
    }

    internal static string? PublicSignature(string member)
    {
        var method = PublicMethod(member);
        if (method is null) return null;
        var format = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            memberOptions: SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeModifiers,
            parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeParamsRefOut);
        return (method.IsStatic ? "STATIC: " : "INSTANCE: ") + method.ToDisplayString(format);
    }

    internal static Validation Validate(Question question, string output)
    {
        try
        {
            using var json = JsonDocument.Parse(output);
            JsonElement root = json.RootElement;
            if (root.GetProperty("member").GetString() != question.Member) return Fail("Member ID does not match the supplied authoritative API entry.");
            if (question.Kind == "lookup")
            {
                foreach (string language in new[] { "english_queries", "spanish_queries" })
                {
                    JsonElement list = root.GetProperty(language);
                    if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() != 2) return Fail("Require exactly two queries in each language.");
                    string[] values = list.EnumerateArray().Select(v => v.GetString() ?? "").ToArray();
                    if (values.Any(v => v.Length is < 8 or > 240 || v.Contains('\n') || v.Contains("$step:", StringComparison.Ordinal)) || values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 2)
                        return Fail("Lookup phrases are invalid, duplicated or too long.");
                }
                return new(true, false, false, true, "candidate_aliases_need_semantic_review", []);
            }
            string code = root.GetProperty("csharp").GetString() ?? "";
            if (code.Length is < 40 or > 14000 || code.Contains("TODO", StringComparison.OrdinalIgnoreCase)) return Fail("Missing, excessive or placeholder code.");
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.CSharp12));
            var compilation = CSharpCompilation.Create("Candidate_" + question.Id, [tree], References.Value,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: false));
            string[] errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Take(8).Select(d => d.ToString()).ToArray();
            SemanticModel model = compilation.GetSemanticModel(tree);
            bool referencesExpected = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Select(n => model.GetSymbolInfo(n).Symbol?.OriginalDefinition)
                .Any(symbol => symbol?.ContainingAssembly?.Name == "RevitAPI" && symbol.GetDocumentationCommentId() == question.Member);
            string[] banned = ["System.IO", "System.Net", "System.Reflection", "System.Diagnostics.Process", "System.Runtime.InteropServices", "Microsoft.Win32"];
            bool restricted = !tree.GetRoot().DescendantNodes().OfType<UnsafeStatementSyntax>().Any() &&
                !tree.GetRoot().DescendantNodes().OfType<AttributeSyntax>().Any(a => a.Name.ToString().Contains("DllImport", StringComparison.Ordinal)) &&
                !tree.GetRoot().DescendantNodes().OfType<ExpressionSyntax>().Select(n => model.GetSymbolInfo(n).Symbol?.ToDisplayString() ?? "")
                    .Any(symbol => banned.Any(prefix => symbol.StartsWith(prefix, StringComparison.Ordinal)));
            var calls = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Select(n => model.GetSymbolInfo(n).Symbol as IMethodSymbol).Where(m => m is not null).ToArray();
            bool ownsTransaction = tree.GetRoot().DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
                .Any(n => model.GetTypeInfo(n).Type?.ToDisplayString() == "Autodesk.Revit.DB.Transaction");
            bool TransactionCall(string name) => calls.Any(m => m!.ContainingType.ToDisplayString() == "Autodesk.Revit.DB.Transaction" && m.Name == name);
            bool requiresTransaction = question.Source.Contains("TRANSACTION POLICY: own", StringComparison.Ordinal);
            bool transactionContract = (!requiresTransaction || ownsTransaction) && (!ownsTransaction || (TransactionCall("Start") && TransactionCall("Commit") && TransactionCall("RollBack")));
            if (ownsTransaction)
            {
                var commits = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Where(n => model.GetSymbolInfo(n).Symbol is IMethodSymbol s && s.Name == "Commit" && s.ContainingType.ToDisplayString() == "Autodesk.Revit.DB.Transaction").ToArray();
                transactionContract &= commits.Length == 1 && tree.GetRoot().DescendantNodes().OfType<CatchClauseSyntax>().Any();
                if (commits.Length == 1)
                    transactionContract &= !tree.GetRoot().DescendantNodes().OfType<ReturnStatementSyntax>().Any(r => r.SpanStart < commits[0].SpanStart);
            }
            if (question.Source.Contains("CONSTRAINED SCAFFOLD:", StringComparison.Ordinal))
            {
                restricted &= !calls.Any(m => m!.ContainingAssembly.Name == "RevitAPI" && m.OriginalDefinition.GetDocumentationCommentId() != question.Member && m.ContainingType.ToDisplayString() != "Autodesk.Revit.DB.Transaction");
                var declarations = tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>().ToArray();
                restricted &= declarations.Length == 1 && declarations[0] is ClassDeclarationSyntax c && c.Identifier.Text == "Candidate" &&
                    c.Modifiers.Any(SyntaxKind.PublicKeyword) && c.Modifiers.Any(SyntaxKind.StaticKeyword) &&
                    !tree.GetRoot().DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().Any();
            }
            string[] reviewErrors = transactionContract ? errors : [.. errors, "Transaction candidate lacks explicit Start, Commit or RollBack; compilation alone does not validate document changes."];
            return new(true, errors.Length == 0, referencesExpected, restricted,
                errors.Length == 0 && referencesExpected && restricted && transactionContract ? "compiled_candidate_NOT_runtime_verified" : "rejected_candidate", reviewErrors);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        { return Fail(ex.Message); }
    }
    private static Validation Fail(string error) => new(false, false, false, false, "invalid_output", [error]);
}
