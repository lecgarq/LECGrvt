# Native MCP coordination-read results

The Revit 2026 campaign ran the complete 41-read production dispatcher against fresh detached disposable copies of the four named CASA EUCALIPTO discipline models. The harness closed every copy without saving, deleted it, and verified the original model hash. No returned model values or external paths were retained.

## Evidence

- Run: `project-native-read-runs/20260916T034358/`
- TRX: `projectnativeread-20260916T034210.trx`
- Tested Copilot assembly SHA-256: `195B90355A6F4581DDE2B55DD9D92733B4562B629D66DE7A80E94DACD7060ECA`
- Receipt digest: `14D95F4287AA1CAB482EC04F13E28552AA2C93C17CDD157302084851E258490C`
- Result: 41 operations × 4 models = 164 contexts; zero `read_failed` outcomes.

## New adapters

| Operation | Architecture | Topography | Structure | MEP |
| --- | --- | --- | --- | --- |
| `external_files_list` | `read_succeeded` | `read_succeeded` | `read_succeeded` | `read_succeeded` |
| `panel_host` | `read_succeeded` on `Autodesk.Revit.DB.Panel` | `missing_fixture` | `missing_fixture` | `missing_fixture` |

`missing_fixture` means the bounded corpus probe found no compatible target. It is not an API failure or evidence that the operation is unsupported in that discipline. These results establish only the named binaries, models, inputs and revision recorded in the manifest and receipts.
