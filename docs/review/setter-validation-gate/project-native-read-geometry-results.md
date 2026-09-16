# Native MCP geometry-read results

The Revit 2026 campaign ran the complete 45-read production dispatcher against fresh detached disposable copies of the four named CASA EUCALIPTO discipline models. The harness closed every copy without saving, deleted it, and verified the original model hash. No returned coordinates, geometry or model values were retained.

## Evidence

- Run: `project-native-read-runs/20260916T041102/`
- TRX: `projectnativeread-20260916T040509.trx`
- Tested Copilot assembly SHA-256: `2F4F26C3AB46B16D8640146F49F0955B492BE6E21AE9A5CB04B7A84C2D47F5D8`
- Receipt digest: `9B0F21F3C45D401B699AEE603D675FED05DA6ABB829DF7CFDF16F443DCFEDE26`
- Result: 45 operations × 4 models = 180 contexts; zero `read_failed` outcomes.

## New adapters

| Operation | Architecture | Topography | Structure | MEP |
| --- | --- | --- | --- | --- |
| `base_points` | `read_succeeded` | `read_succeeded` | `read_succeeded` | `read_succeeded` |
| `face_split_boundaries` | `read_succeeded` on `Autodesk.Revit.DB.FaceSplitter` | `missing_fixture` | `missing_fixture` | `missing_fixture` |

`face_split_boundaries` returns bounded summaries—loop index, segment count and approximate millimeter length—rather than unbounded curve geometry. These results establish only the named binaries, models, inputs and revision recorded in the manifest and receipts.
