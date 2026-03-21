# Technology Stack

> Auto-generated deep mapping on 2026-03-21

## Framework Backbone

| Technology | Version | Architectural Purpose |
|------------|---------|-----------------------|
| .NET | `8.0-windows` | Core runtime container. C# 12 specification target. Compiles using aggressive static analyzers `AnalysisLevel: latest`. |
| Revit API | `2026` | Interoperability platform. Strict single-threaded CAD engine bridge. Linked non-locally via dynamic DLL reflection mapping. |
| WPF Core | `8.0` | High performance graphics desktop presentation matrix. Leveraged using extended generic syntax. |

## External Dependencies (Nuget)

### Core Computing & Geometry Processing
These libraries power the spatial and mathematical brains of the plugin:

| Package | Version | Purpose & Internal Usage |
|---------|---------|--------------------------|
| **Clipper2** | `2.0.0` | Extremely fast 2D polygon clipper. Used heavily in boundary generation, overlap detection, and boolean subtraction logic inside `AlignEdges` and `SplitBoundaries`. |
| **geometry3Sharp** | `1.0.324` | 3D procedural mesh generation and heavy computational geometry. Primary backend for interpreting CAD polylines, surface interpolation, and complex Toposolid mathematical flattening. |
| **Unofficial.Triangle.NET** | `0.0.1` | Specialized 2D Delaunay mesh engine. Triangulates point clouds when performing interpolation calculations across Toposolids. |

### Presentation & Pattern Implementations

| Package | Version | Purpose & Internal Usage |
|---------|---------|--------------------------|
| **CommunityToolkit.Mvvm** | `8.2.2` | Core structural implementation of `ObservableObject`, `RelayCommand`, and the `IMessenger` protocol. Driven entirely by source-generators (`CommunityToolkitMvvmSourceGeneratorIsEnabled=true`) to avoid reflection lag at runtime. |
| **Microsoft.Extensions.DependencyInjection** | `Internal` | Provides `IServiceCollection` standard dependency mappings matching enterprise container systems (ASP.NET). |
| **System.Windows.Extensions** | `8.0.0` | Required cross-implementation extensions for deploying pure WPF inside a specialized DLL host like Revit. Fixes generic marshaling contexts. |

### Analysis Tooling

| Package | Version | Purpose & Internal Usage |
|---------|---------|--------------------------|
| **Microsoft.CodeAnalysis.NetAnalyzers** | `8.0.0` | Development-only strict static analyzer. Warns continuously inside Visual Studio/Rider across dead code paths and memory leaks. |

## Application Subsystems

| System | Technology Layer | Behavior |
|--------|------------------|----------|
| **Logging** | Internal Sink | App-defined `Logger.Instance` mapped to text tracking system in `AppData`. |
| **Theming** | Pure XAML | Encapsulated custom styling configuration via `LecgTheme.xaml` loaded physically into `Application.Current.Resources`. Bypasses relying on standard Windows UI. |
| **Error Handling** | Pure C# Delegates | Overrides globally `DispatcherUnhandledExceptionEventArgs` allowing safety catch-nets instead of raw exceptions returning completely out of the AddIn boundary. |
