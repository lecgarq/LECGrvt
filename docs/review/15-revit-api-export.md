# Revit API Export Summary

## Current Output

- Export folder: `docs/review/revit-api/`
- Full assembly export:
  - `revitapi.types.jsonl`: `20144` type records
  - `revitapi.members.jsonl`: `53285` member records
  - `revitapiui.types.jsonl`: `7010` type records
  - `revitapiui.members.jsonl`: `14840` member records
- Cleaner LLM-oriented export:
  - `revitapi.public.types.jsonl`: `4851` externally visible type records
  - `revitapi.public.members.jsonl`: `31015` public/protected member records
  - `revitapiui.public.types.jsonl`: `646` externally visible type records
  - `revitapiui.public.members.jsonl`: `1441` public/protected member records
  - `revit.autodesk.revit.public.types.jsonl`: `7852` public types limited to `Autodesk.Revit*` namespaces across both assemblies
  - `revit.autodesk.revit.public.members.jsonl`: `96765` public/protected members limited to `Autodesk.Revit*` namespaces across both assemblies
- Focused subsets and indexes:
  - `revit.autodesk.revit.public.types.jsonl`
  - `revit.autodesk.revit.public.members.jsonl`
  - `revit.autodesk.revit.db.public.types.jsonl`
  - `revit.autodesk.revit.db.public.members.jsonl`
  - `revit.autodesk.revit.ui.public.types.jsonl`
  - `revit.autodesk.revit.ui.public.members.jsonl`
  - `revit.public.namespaces.json`
  - `revit.llm.index.json`

## Notes

- Source DLLs are read directly from `C:\Program Files\Autodesk\Revit 2026`
- No browser usage
- No Revit launch
- Output is based on local DLL metadata only
- `RevitAPI.dll` exports through runtime reflection.
- `RevitAPIUI.dll` exports through metadata fallback when native module resolution prevents runtime loading.
- The metadata fallback now classifies enums/delegates/structs more accurately and filters property/event accessor noise more closely to the runtime export path.

## Regeneration

```powershell
dotnet run --project tools/RevitApiExtractor/RevitApiExtractor.csproj -- "C:\Program Files\Autodesk\Revit 2026" "docs/review/revit-api"
```

## Practical Use

- Prefer `revit.llm.index.json` as the machine-readable starting point.
- Prefer `revit.autodesk.revit.public.types.jsonl` and `revit.autodesk.revit.public.members.jsonl` for the cleanest broad Revit-facing prompting and retrieval.
- Prefer the `revit.autodesk.revit.db.*` and `revit.autodesk.revit.ui.*` files when you want smaller, more focused context windows.
- Use `revitapi.public.*` and `revitapiui.public.*` when you explicitly want all externally visible symbols per assembly, including non-`Autodesk.Revit*` namespace noise.
- Use `revit.public.namespaces.json` to choose namespaces before loading large JSONL files.
- Use the full files only when you explicitly need internal or mixed-mode metadata noise that is not part of the public surface.
