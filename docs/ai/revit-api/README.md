# Revit API Reference

Ground truth for the Revit 2026.4.10 API surface, generated from the reference assemblies this project compiles against. Never answer a Revit API question from memory when the answer is in here.

| File | What it holds |
|------|---------------|
| `members.txt` | 33,089 public/protected members, one per line, with full signatures |
| `types.txt` | 2,670 public types with base class and interfaces |
| `SEMANTICS.md` | The rules signatures cannot encode — transactions, API context, batch, units, geometry |
| `revit.public.namespaces.json` | Namespace inventory with type/member counts |

## Query it

Grep. It is flat text on purpose — `rg` answers in milliseconds and nothing loads into context.

```bash
rg "^Autodesk\.Revit\.DB\.TransactionGroup\." docs/ai/revit-api/members.txt
```

```bash
rg -i "IsAlmostEqualTo" docs/ai/revit-api/members.txt
```

```bash
rg "^class Autodesk\.Revit\.DB\.Wall " docs/ai/revit-api/types.txt
```

Line format:

```
Autodesk.Revit.DB.UnitUtils.ConvertToInternalUnits(Double value, ForgeTypeId unitTypeId) -> Double [static method]
Autodesk.Revit.DB.ElementId.Value -> Int64 [get property]
Autodesk.Revit.DB.BuiltInCategory.OST_Walls = -2000011 -> BuiltInCategory [field]
```

Enum fields carry their literal value — often the thing being looked up.

**Absence is evidence.** These are the assemblies the build compiles against, so a member that is not in `members.txt` does not exist in 2026 and will not compile. That is how `ElementId.IntegerValue` and `DisplayUnitType` were confirmed removed.

## Regenerate

Only needed when the Revit version changes. Takes about a second.

```bash
dotnet run --project scripts/revit-api-index -c Release -- docs/ai/revit-api "$HOME/.nuget/packages/nice3point.revit.api.revitapi/2026.4.10/ref/net8.0-windows7.0/RevitAPI.dll" "$HOME/.nuget/packages/nice3point.revit.api.revitapiui/2026.4.10/ref/net8.0-windows7.0/RevitAPIUI.dll" "/c/Program Files/Autodesk/Revit 2026"
```

Update the version in all three paths first. The generator is a standalone tool, not part of `LECG.sln`, excluded from compilation by `<Compile Remove="scripts\**\*.cs" />`, and never deployed.

Arguments after the output directory: a `.dll` is **indexed**; a directory is used for **resolution only**. The Revit install directory is required for full coverage — `RibbonButton..ctor` and `PointCloudType.GetReCapProject` reference `AdWindows.dll` and the ReCap interop, which the NuGet reference packages do not carry. Without it those two index with `signature-undecodable` instead of a real signature.

The current index has **0 undecodable**. If a regeneration reports otherwise, the probe directory is wrong or Revit is not installed — the run still succeeds and the members stay findable by name, so absence never silently means "missing from the index".
