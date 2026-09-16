# Native MCP membership-read results

The Revit 2026 campaign ran the complete 49-read production dispatcher against fresh detached disposable copies of the four named CASA EUCALIPTO discipline models. The harness closed every copy without saving, deleted it, and verified the original model hash. No returned member identities, names or model values were retained.

## Evidence

- Run: `project-native-read-runs/20260916T042604`
- TRX: `projectnativeread-20260916T042514.trx`
- Tested Copilot assembly SHA-256: `99D4B16E925E2E2C15ED449AB11B2F1B2D4C1A78EA2DDEBFAAA4BC6973857ED2`
- Receipt digest: `83A3E5B0ED30A4D44BA3E76DB9B08A4F0D81F198993F8D2814FEBB49879031D8`
- Result: 49 operations × 4 models = 196 contexts; zero `read_failed` outcomes.
- Safety: 4/4 source hashes matched before and after; all copies report `copy_removed:true` and `cleanup_verified:true`; zero `.rvt` files remained in the run directory.

## New adapters

| Operation | Architecture | Topography | Structure | MEP |
| --- | --- | --- | --- | --- |
| `group_members` | `read_succeeded` on `Autodesk.Revit.DB.Group` | `missing_fixture` | `missing_fixture` | `read_succeeded` on `Autodesk.Revit.DB.Group` |
| `family_subcomponents` | `read_succeeded` | `read_succeeded` | `read_succeeded` | `read_succeeded` |
| `assembly_members` | `missing_fixture` | `missing_fixture` | `missing_fixture` | `read_succeeded` on `Autodesk.Revit.DB.AssemblyInstance` |
| `mep_system_members` | `missing_fixture` | `missing_fixture` | `missing_fixture` | `read_succeeded` on `Autodesk.Revit.DB.Plumbing.PipingSystem` |

Empty returned member pages are valid reads; the evidence establishes call-path execution in the named contexts, not that every compatible element has members. Family-document writes remain outside the production MCP contract.
