# Native MCP relationship-read results

The Revit 2026 campaign ran the complete 39-read production dispatcher against fresh detached disposable copies of the four named CASA EUCALIPTO discipline models. The harness closed every copy without saving, deleted it, and verified the original model hash. No returned model values were retained.

## Evidence

- Run: `project-native-read-runs/20260916T032947/`
- TRX: `projectnativeread-20260916T032627.trx`
- Tested Copilot assembly SHA-256: `3F8EE52DB7DD7D01068C89E3503F2320A42EABB1B4BD138EE33B883030D69A44`
- Receipt digest: `7F269270B6586D71EF7D998881C16E6928A647433067D1767063B45B3931FCBE`
- Result: 39 operations × 4 models = 156 contexts; zero `read_failed` outcomes.

## New adapters

| Operation | Architecture | Topography | Structure | MEP |
| --- | --- | --- | --- | --- |
| `spatial_contains_point` | `read_succeeded` on `Autodesk.Revit.DB.Architecture.Room` | `missing_fixture` | `missing_fixture` | `missing_fixture` |
| `mep_connectors` | `read_succeeded` on `Autodesk.Revit.DB.FamilyInstance` | `missing_fixture` | `missing_fixture` | `read_succeeded` on `Autodesk.Revit.DB.Plumbing.Pipe` |

`missing_fixture` means the bounded corpus probe found no compatible target. It is not an API failure or evidence that the operation is unsupported in that discipline. These results establish only the named binaries, models, inputs and revision recorded in the manifest and receipts.
