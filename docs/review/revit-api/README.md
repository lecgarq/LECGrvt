# Revit API Export

Source directory: `C:\Program Files\Autodesk\Revit 2026`

Generated files:

- `revitapi.types.jsonl`: 20144 types, 4851 externally visible, 227 namespaces
- `revitapi.members.jsonl`: 53285 members
- `revitapi.public.types.jsonl`: filtered externally visible types only
- `revitapi.public.members.jsonl`: 31015 public/protected members on externally visible types
- `revitapiui.types.jsonl`: 7010 types, 646 externally visible, 809 namespaces
- `revitapiui.members.jsonl`: 14840 members
- `revitapiui.public.types.jsonl`: filtered externally visible types only
- `revitapiui.public.members.jsonl`: 1441 public/protected members on externally visible types

Load failures:

- RevitAPIUI.dll: runtime load failed with FileNotFoundException; exported via metadata fallback

Record shape:

- Type records include namespace, base type, interfaces, generic arguments, attributes, visibility, and lifecycle flags.
- Member records include kind, declaring type, signature, return type, parameters, attributes, and basic dispatch flags.

Regenerate with:

```powershell
dotnet run --project tools/RevitApiExtractor/RevitApiExtractor.csproj -- "C:\Program Files\Autodesk\Revit 2026" "docs/review/revit-api"
```
