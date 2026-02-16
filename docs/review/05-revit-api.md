# Revit API Usage Patterns

## Document Purpose

This document describes how the Revit API is used in the LECG plugin, including abstraction patterns, transaction management, element filtering, and geometric operations.

**Target Audience**: Developers

**Last Updated**: February 2026

---

## API Abstraction Strategy

### Service Layer Isolation

**Principle**: All Revit API calls are isolated in the Service layer.

**Architecture**:
```
Commands (UI Orchestration)
    ↓
Services (Revit API Abstraction) ← All API calls here
    ↓
Revit API (RevitAPI.dll, RevitAPIUI.dll)
```

**Benefits**:
- ViewModels and Views are Revit-agnostic (testable without Revit)
- API changes only affect Services
- Commands remain thin and focused on UI flow

**Example**: `SexyRevitCommand` never touches Revit API directly:
```csharp
// Command: No Revit API calls
var service = ServiceLocator.GetRequiredService<ISexyRevitService>();
service.ApplyBeauty(doc, view, settings, Log, UpdateProgress);

// Service: All Revit API calls
public void ApplyBeauty(Document doc, View view, ...)
{
    view.DisplayStyle = DisplayStyle.Realistic; // Revit API
    view.DetailLevel = ViewDetailLevel.Fine;    // Revit API
}
```

---

## Transaction Management

### Pattern 1: Service-Managed Transactions

**Use Case**: Services that encapsulate complete operations

**Command**:
```csharp
protected override string? TransactionName => null; // Service handles it

public override void Execute(UIDocument uiDoc, Document doc)
{
    var service = ServiceLocator.GetRequiredService<ISexyRevitService>();
    service.ApplyBeauty(doc, view, settings, Log, UpdateProgress);
}
```

**Service**:
```csharp
public void ApplyBeauty(Document doc, View view, ...)
{
    using (Transaction t = new Transaction(doc, "Apply Beauty"))
    {
        t.Start();
        // Modify view
        view.DisplayStyle = DisplayStyle.Realistic;
        t.Commit();
    }
}
```

**Pros**: Service controls transaction scope, can have multiple transactions
**Cons**: More boilerplate in service

### Pattern 2: Command Auto-Transaction

**Use Case**: Simple operations that need one transaction

**Command**:
```csharp
protected override string? TransactionName => "Reset Slabs"; // Auto-managed

public override void Execute(UIDocument uiDoc, Document doc)
{
    // Base class wraps in transaction
    var service = ServiceLocator.GetRequiredService<ISlabService>();
    service.ResetSlabElevations(doc, slabs);
}
```

**Service**:
```csharp
public void ResetSlabElevations(Document doc, IEnumerable<Element> slabs)
{
    // No transaction needed - command handles it
    foreach (var slab in slabs)
    {
        slab.get_Parameter(param).Set(value);
    }
}
```

**Pros**: Less boilerplate
**Cons**: Single transaction only, less flexible

---

## Element Filtering Patterns

### Basic Filtering

**Get All Elements of Category**:
```csharp
var walls = new FilteredElementCollector(doc)
    .OfCategory(BuiltInCategory.OST_Walls)
    .WhereElementIsNotElementType()
    .ToElements();
```

**Get All Element Types**:
```csharp
var wallTypes = new FilteredElementCollector(doc)
    .OfCategory(BuiltInCategory.OST_Walls)
    .WhereElementIsElementType()
    .Cast<WallType>()
    .ToList();
```

### Filtering by Class

**Get Specific Element Type**:
```csharp
var floors = new FilteredElementCollector(doc)
    .OfClass(typeof(Floor))
    .WhereElementIsNotElementType()
    .Cast<Floor>()
    .ToList();
```

**Get Views**:
```csharp
var views = new FilteredElementCollector(doc)
    .OfClass(typeof(View))
    .Cast<View>()
    .Where(v => !v.IsTemplate)
    .ToList();
```

### Filtering by Parameter

**Elements with Specific Parameter Value**:
```csharp
var collector = new FilteredElementCollector(doc)
    .OfCategory(BuiltInCategory.OST_Walls)
    .WhereElementIsNotElementType();

var filteredWalls = collector
    .Where(e =>
    {
        var param = e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
        return param != null && param.AsString() == "A";
    })
    .ToList();
```

**Using ParameterFilter** (more efficient):
```csharp
var pvp = new ParameterValueProvider(new ElementId(BuiltInParameter.ALL_MODEL_MARK));
var evaluator = new FilterStringEquals();
var rule = new FilterStringRule(pvp, evaluator, "A", false);
var filter = new ElementParameterFilter(rule);

var walls = new FilteredElementCollector(doc)
    .OfCategory(BuiltInCategory.OST_Walls)
    .WherePasses(filter)
    .ToElements();
```

### Filtering in Active View

**Elements Visible in View**:
```csharp
var elementsInView = new FilteredElementCollector(doc, view.Id)
    .OfCategory(BuiltInCategory.OST_Walls)
    .WhereElementIsNotElementType()
    .ToElements();
```

---

## Parameter Access Patterns

### Reading Parameters

**By BuiltInParameter**:
```csharp
var mark = element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString();
var comments = element.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString();
```

**By Parameter Name**:
```csharp
var param = element.LookupParameter("Comments");
string value = param?.AsString();
```

**By GUID (Shared Parameters)**:
```csharp
var guid = new Guid("12345678-1234-1234-1234-123456789abc");
var param = element.get_Parameter(guid);
```

**Parameter Value Types**:
```csharp
switch (param.StorageType)
{
    case StorageType.String:
        string strValue = param.AsString();
        break;
    case StorageType.Integer:
        int intValue = param.AsInteger();
        break;
    case StorageType.Double:
        double doubleValue = param.AsDouble(); // in feet
        break;
    case StorageType.ElementId:
        ElementId idValue = param.AsElementId();
        break;
}
```

### Writing Parameters

**Set Parameter Value** (within transaction):
```csharp
using (Transaction t = new Transaction(doc, "Set Parameter"))
{
    t.Start();

    var param = element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
    if (param != null && !param.IsReadOnly)
    {
        param.Set("New Value");
    }

    t.Commit();
}
```

**Check if Parameter is Modifiable**:
```csharp
if (param != null && !param.IsReadOnly && !param.IsShared)
{
    param.Set(value);
}
```

---

## Geometric Operations

### Toposolid Point Manipulation

**Get Toposolid Points**:
```csharp
if (element is Toposolid topo)
{
    var points = topo.GetPoints(); // IList<XYZ>

    Log($"Toposolid has {points.Count} points");

    foreach (var pt in points)
    {
        Log($"  Point: ({pt.X}, {pt.Y}, {pt.Z})");
    }
}
```

**Modify Toposolid Points** (Offset):
```csharp
using (Transaction t = new Transaction(doc, "Offset Points"))
{
    t.Start();

    var points = topo.GetPoints();
    var newPoints = new List<XYZ>();

    foreach (var pt in points)
    {
        var newPt = new XYZ(pt.X, pt.Y, pt.Z + offsetValue);
        newPoints.Add(newPt);
    }

    topo.SetPoints(newPoints);

    t.Commit();
}
```

### Point Simplification (Douglas-Peucker Algorithm)

**Pattern from SimplifyPointsService**:
```csharp
public List<XYZ> SimplifyPoints(List<XYZ> points, double tolerance)
{
    if (points.Count < 3) return points;

    // Douglas-Peucker algorithm
    var simplified = new List<XYZ> { points.First() };

    SimplifyRecursive(points, 0, points.Count - 1, tolerance, simplified);

    simplified.Add(points.Last());

    return simplified;
}

private void SimplifyRecursive(List<XYZ> points, int start, int end, double tolerance, List<XYZ> result)
{
    double maxDistance = 0;
    int maxIndex = start;

    // Find point with maximum distance from line
    for (int i = start + 1; i < end; i++)
    {
        double dist = DistanceToLine(points[i], points[start], points[end]);
        if (dist > maxDistance)
        {
            maxDistance = dist;
            maxIndex = i;
        }
    }

    // If max distance > tolerance, split and recurse
    if (maxDistance > tolerance)
    {
        SimplifyRecursive(points, start, maxIndex, tolerance, result);
        result.Add(points[maxIndex]);
        SimplifyRecursive(points, maxIndex, end, tolerance, result);
    }
}
```

### Alignment Operations

**Align Text Notes** (from AlignElementsService):
```csharp
public void AlignLeft(Document doc, IEnumerable<Element> elements)
{
    using (Transaction t = new Transaction(doc, "Align Left"))
    {
        t.Start();

        // Find leftmost position
        double minX = elements
            .OfType<TextNote>()
            .Min(tn => tn.Coord.X);

        // Align all to leftmost
        foreach (var elem in elements.OfType<TextNote>())
        {
            var newCoord = new XYZ(minX, elem.Coord.Y, elem.Coord.Z);
            elem.Coord = newCoord;
        }

        t.Commit();
    }
}
```

---

## Category and View Manipulation

### Hiding Categories in View

**Pattern from SexyRevitService**:
```csharp
private void HideCategory(Document doc, View view, BuiltInCategory category)
{
    try
    {
        Category cat = Category.GetCategory(doc, category);
        if (cat != null)
        {
            view.SetCategoryHidden(cat.Id, true);
        }
    }
    catch (Exception ex)
    {
        Log($"⚠ Could not hide {category}: {ex.Message}");
    }
}

// Usage:
HideCategory(doc, view, BuiltInCategory.OST_Grids);
HideCategory(doc, view, BuiltInCategory.OST_Levels);
```

### View Display Settings

**Set Display Style**:
```csharp
view.DisplayStyle = DisplayStyle.Realistic; // or Wireframe, Shaded, etc.
```

**Set Detail Level**:
```csharp
view.DetailLevel = ViewDetailLevel.Fine; // or Coarse, Medium
```

**Sun and Shadow Settings** (3D Views):
```csharp
if (view is View3D v3d)
{
    SunAndShadowSettings sunSettings = v3d.SunAndShadowSettings;
    if (sunSettings != null)
    {
        sunSettings.SunAndShadowType = SunAndShadowType.StillImage;
    }
}
```

---

## Material Operations

### Get Materials

**All Materials in Document**:
```csharp
var materials = new FilteredElementCollector(doc)
    .OfClass(typeof(Material))
    .Cast<Material>()
    .ToList();
```

**Material by Name**:
```csharp
var material = new FilteredElementCollector(doc)
    .OfClass(typeof(Material))
    .Cast<Material>()
    .FirstOrDefault(m => m.Name == "Concrete");
```

### Assign Material

**To Toposolid/Floor**:
```csharp
using (Transaction t = new Transaction(doc, "Assign Material"))
{
    t.Start();

    if (element is Floor floor)
    {
        // Get floor type
        var floorType = doc.GetElement(floor.GetTypeId()) as FloorType;

        if (floorType != null)
        {
            // Assign material to structure layer (example)
            var compoundStructure = floorType.GetCompoundStructure();
            if (compoundStructure != null)
            {
                var layers = compoundStructure.GetLayers();
                if (layers.Count > 0)
                {
                    var layer = layers[0];
                    layer.MaterialId = material.Id;
                    compoundStructure.SetLayers(layers);
                    floorType.SetCompoundStructure(compoundStructure);
                }
            }
        }
    }

    t.Commit();
}
```

---

## Purge Operations

### Purge Unused Elements

**Pattern from PurgeService**:
```csharp
public void PurgeUnused(Document doc)
{
    using (Transaction t = new Transaction(doc, "Purge Unused"))
    {
        t.Start();

        // Use PerformanceAdviser to find purgeable elements
        var adviser = PerformanceAdviser.GetPerformanceAdviser();
        var ruleIds = adviser.GetAllRuleIds();

        foreach (var ruleId in ruleIds)
        {
            var results = adviser.ExecuteRules(doc, new List<PerformanceAdviserRuleId> { ruleId });

            foreach (var result in results)
            {
                var elementIds = result.GetFailingElements();

                foreach (var elemId in elementIds)
                {
                    try
                    {
                        doc.Delete(elemId);
                    }
                    catch
                    {
                        // Element can't be deleted, skip
                    }
                }
            }
        }

        t.Commit();
    }
}
```

---

## Error Handling Patterns

### Try-Catch Per Element

**Pattern**: Process each element in try-catch to avoid total failure

```csharp
foreach (var elem in elements)
{
    try
    {
        // Process element
        ProcessElement(elem);
        log($"✓ Processed: {elem.Name}");
    }
    catch (Exception ex)
    {
        log($"⚠ Failed {elem.Id}: {ex.Message}");
        // Continue to next element
    }
}
```

### Transaction Rollback

**Pattern**: Rollback transaction on error

```csharp
using (Transaction t = new Transaction(doc, "Operation"))
{
    t.Start();

    try
    {
        // Risky operations
        DoWork();
        t.Commit();
    }
    catch (Exception ex)
    {
        t.RollBack();
        log($"✗ Transaction failed: {ex.Message}");
    }
}
```

---

## Performance Best Practices

### Use Filters, Not LINQ

**Bad** (slow):
```csharp
var walls = new FilteredElementCollector(doc)
    .WhereElementIsNotElementType()
    .ToElements()
    .Where(e => e.Category?.Name == "Walls");
```

**Good** (fast):
```csharp
var walls = new FilteredElementCollector(doc)
    .OfCategory(BuiltInCategory.OST_Walls)
    .WhereElementIsNotElementType()
    .ToElements();
```

### Batch Transactions

**Bad** (slow):
```csharp
foreach (var elem in elements)
{
    using (Transaction t = new Transaction(doc, "Modify"))
    {
        t.Start();
        elem.get_Parameter(param).Set(value);
        t.Commit();
    }
}
```

**Good** (fast):
```csharp
using (Transaction t = new Transaction(doc, "Modify All"))
{
    t.Start();

    foreach (var elem in elements)
    {
        elem.get_Parameter(param).Set(value);
    }

    t.Commit();
}
```

### Avoid Unnecessary API Calls

**Bad**:
```csharp
for (int i = 0; i < elements.Count; i++)
{
    var elem = doc.GetElement(elementIds[i]); // Called every iteration
    Process(elem);
}
```

**Good**:
```csharp
var elements = elementIds.Select(id => doc.GetElement(id)).ToList(); // Called once

foreach (var elem in elements)
{
    Process(elem);
}
```

---

## Unit Conversion

### Feet ↔ Millimeters

Revit API uses **feet** internally. Convert to/from metric:

```csharp
// Constants
const double MM_PER_FOOT = 304.8;

// Feet → Millimeters
double feetValue = 10.0;
double mmValue = feetValue * MM_PER_FOOT; // 3048 mm

// Millimeters → Feet
double mmValue = 3048.0;
double feetValue = mmValue / MM_PER_FOOT; // 10 ft
```

**Using UnitUtils** (Revit 2021+):
```csharp
// Feet → Millimeters
double mm = UnitUtils.ConvertFromInternalUnits(feetValue, UnitTypeId.Millimeters);

// Millimeters → Feet
double feet = UnitUtils.ConvertToInternalUnits(mmValue, UnitTypeId.Millimeters);
```

---

## Common Revit API Pitfalls

### 1. Forgetting Transactions

**Error**: "Cannot modify the model outside of a transaction"

**Fix**: Wrap modifications in `Transaction`:
```csharp
using (Transaction t = new Transaction(doc, "Modify"))
{
    t.Start();
    element.get_Parameter(param).Set(value);
    t.Commit();
}
```

### 2. Modifying Read-Only Parameters

**Error**: "The parameter is read-only"

**Fix**: Check before setting:
```csharp
if (!param.IsReadOnly)
{
    param.Set(value);
}
```

### 3. Accessing Disposed Elements

**Error**: "Element is invalid or deleted"

**Fix**: Check validity:
```csharp
if (element != null && element.IsValidObject)
{
    Process(element);
}
```

### 4. UI Thread Issues

**Error**: "Wrong thread" or UI freezing

**Fix**: Use `Dispatcher` for UI updates:
```csharp
Application.Current.Dispatcher.Invoke(() =>
{
    // Update UI
    textBlock.Text = "Updated";
});
```

---

## Conclusion

The LECG plugin follows these Revit API best practices:
- **Service Layer Abstraction**: All API calls isolated in services
- **Transaction Management**: Clear patterns for auto/manual transactions
- **Efficient Filtering**: Use `FilteredElementCollector` with proper filters
- **Error Handling**: Try-catch per element, graceful degradation
- **Performance**: Batch operations, avoid unnecessary API calls

For implementation examples, see:
- **Services**: `src/Services/*Service.cs`
- **Patterns**: `04-patterns.md`
- **Architecture**: `01-architecture.md`
