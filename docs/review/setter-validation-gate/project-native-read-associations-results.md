# Native MCP association-read results

The Revit 2026 campaign ran the complete 43-read production dispatcher against fresh detached disposable copies of the four named CASA EUCALIPTO discipline models. The harness closed every copy without saving, deleted it, and verified the original model hash. No returned model values were retained.

## Evidence

- Run: `project-native-read-runs/20260916T035316/`
- TRX: `projectnativeread-20260916T035127.trx`
- Tested Copilot assembly SHA-256: `8DC33094D66E494E2F6F448674A014325BC270AFE5BD50CAAE22039013415028`
- Receipt digest: `E85E15F4EC9151C109D43721B0893AB76AEB300EED4AC3F5C041A9D162EA1FD7`
- Result: 43 operations × 4 models = 172 contexts; zero `read_failed` outcomes.

## New adapters

| Operation | Architecture | Topography | Structure | MEP |
| --- | --- | --- | --- | --- |
| `stairs_associated_railings` | `read_succeeded` on `Autodesk.Revit.DB.Architecture.Stairs` | `missing_fixture` | `missing_fixture` | `missing_fixture` |
| `group_attached_detail_types` | `read_succeeded` on `Autodesk.Revit.DB.Group` | `missing_fixture` | `missing_fixture` | `missing_fixture` |

The MEP corpus contains groups, but none in the model-group category required by this API; it therefore remains an explicit `missing_fixture`. These results establish only the named binaries, models, inputs and revision recorded in the manifest and receipts.
