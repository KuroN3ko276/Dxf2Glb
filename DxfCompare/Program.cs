using System;
using System.Linq;

// Compare DXF entity extraction between netDxf and IxMilia.Dxf

if (args.Length == 0)
{
    Console.WriteLine("Usage: DxfCompare <dxf-file>");
    return;
}

var dxfPath = args[0];
if (!File.Exists(dxfPath))
{
    Console.WriteLine($"File not found: {dxfPath}");
    return;
}

Console.WriteLine($"Comparing DXF libraries for: {dxfPath}");
Console.WriteLine("=" + new string('=', 60));
Console.WriteLine();

// ===== IxMilia.Dxf =====
Console.WriteLine("=== IxMilia.Dxf ===");
try
{
    using var stream = File.OpenRead(dxfPath);
    var ixFile = IxMilia.Dxf.DxfFile.Load(stream);
    
    Console.WriteLine($"Version: {ixFile.Header.Version}");
    Console.WriteLine($"Total Entities: {ixFile.Entities.Count}");
    Console.WriteLine();
    
    // Group by type
    var ixGroups = ixFile.Entities
        .GroupBy(e => e.GetType().Name)
        .OrderByDescending(g => g.Count())
        .ToList();
    
    Console.WriteLine("Entity Types:");
    foreach (var g in ixGroups)
    {
        Console.WriteLine($"  {g.Key,-25}: {g.Count():N0}");
    }
    Console.WriteLine();
    
    // Sample polylines
    var ixPolylines = ixFile.Entities.OfType<IxMilia.Dxf.Entities.DxfPolyline>().Take(3).ToList();
    if (ixPolylines.Any())
    {
        Console.WriteLine("Sample Polylines (first 3):");
        foreach (var pl in ixPolylines)
        {
            Console.WriteLine($"  Layer: {pl.Layer}, Vertices: {pl.Vertices.Count()}, IsClosed: {pl.IsClosed}");
            if (pl.Vertices.Any())
            {
                var first = pl.Vertices.First();
                Console.WriteLine($"    First vertex: ({first.Location.X:F2}, {first.Location.Y:F2}, {first.Location.Z:F2})");
            }
        }
    }
    
    // Check for mesh-related entities
    var ixMeshTypes = new[] { "DxfPolyline", "Dxf3DFace", "DxfSolid", "DxfMesh", "DxfPolyfaceMesh" };
    Console.WriteLine("\nMesh-related entity counts:");
    foreach (var meshType in ixMeshTypes)
    {
        var count = ixGroups.FirstOrDefault(g => g.Key == meshType)?.Count() ?? 0;
        Console.WriteLine($"  {meshType,-25}: {count:N0}");
    }
    
    // Check polyline flags for mesh indication
    var allPolylines = ixFile.Entities.OfType<IxMilia.Dxf.Entities.DxfPolyline>().ToList();
    var closedCount = allPolylines.Count(p => p.IsClosed);
    Console.WriteLine($"\nPolyline analysis:");
    Console.WriteLine($"  Total polylines: {allPolylines.Count}");
    Console.WriteLine($"  Closed polylines: {closedCount}");
    Console.WriteLine($"  Open polylines: {allPolylines.Count - closedCount}");
    
    if (allPolylines.Any())
    {
        var avgVertices = allPolylines.Average(p => p.Vertices.Count());
        var maxVertices = allPolylines.Max(p => p.Vertices.Count());
        var minVertices = allPolylines.Min(p => p.Vertices.Count());
        Console.WriteLine($"  Avg vertices/polyline: {avgVertices:F1}");
        Console.WriteLine($"  Min vertices: {minVertices}");
        Console.WriteLine($"  Max vertices: {maxVertices}");
        
        // Check for polyface meshes (polylines with M and N counts)
        var polyfaceCount = allPolylines.Count(p => p.Vertices.Any(v => 
            v.GetType().GetProperty("PolyfaceMeshVertex1Index") != null));
        Console.WriteLine($"  Polyface mesh candidates: {polyfaceCount}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error with IxMilia.Dxf: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("=" + new string('=', 60));
Console.WriteLine();

// ===== netDxf =====
Console.WriteLine("=== netDxf ===");
try
{
    var netDoc = netDxf.DxfDocument.Load(dxfPath);
    
    Console.WriteLine($"Version: {netDoc.DrawingVariables.AcadVer}");
    Console.WriteLine();
    
    Console.WriteLine("Entity Counts:");
    Console.WriteLine($"  Lines          : {netDoc.Entities.Lines.Count():N0}");
    Console.WriteLine($"  Polylines2D    : {netDoc.Entities.Polylines2D.Count():N0}");
    Console.WriteLine($"  Polylines3D    : {netDoc.Entities.Polylines3D.Count():N0}");
    Console.WriteLine($"  PolyfaceMeshes : {netDoc.Entities.PolyfaceMeshes.Count():N0}");
    Console.WriteLine($"  PolygonMeshes  : {netDoc.Entities.PolygonMeshes.Count():N0}");
    Console.WriteLine($"  Meshes         : {netDoc.Entities.Meshes.Count():N0}");
    Console.WriteLine($"  Faces3D        : {netDoc.Entities.Faces3D.Count():N0}");
    Console.WriteLine($"  Solids         : {netDoc.Entities.Solids.Count():N0}");
    Console.WriteLine($"  Arcs           : {netDoc.Entities.Arcs.Count():N0}");
    Console.WriteLine($"  Circles        : {netDoc.Entities.Circles.Count():N0}");
    Console.WriteLine($"  Splines        : {netDoc.Entities.Splines.Count():N0}");
    Console.WriteLine($"  Ellipses       : {netDoc.Entities.Ellipses.Count():N0}");
    Console.WriteLine($"  Points         : {netDoc.Entities.Points.Count():N0}");
    Console.WriteLine($"  Inserts        : {netDoc.Entities.Inserts.Count():N0}");
    
    // Sample PolyfaceMeshes
    var pfMeshes = netDoc.Entities.PolyfaceMeshes.Take(3).ToList();
    if (pfMeshes.Any())
    {
        Console.WriteLine("\nSample PolyfaceMeshes (first 3):");
        foreach (var mesh in pfMeshes)
        {
            Console.WriteLine($"  Layer: {mesh.Layer.Name}, Vertices: {mesh.Vertexes.Count()}, Faces: {mesh.Faces.Count()}");
        }
    }
    
    // Sample PolygonMeshes
    var pgMeshes = netDoc.Entities.PolygonMeshes.Take(3).ToList();
    if (pgMeshes.Any())
    {
        Console.WriteLine("\nSample PolygonMeshes (first 3):");
        foreach (var mesh in pgMeshes)
        {
            Console.WriteLine($"  Layer: {mesh.Layer.Name}, Vertices: {mesh.Vertexes.Count()}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error with netDxf: {ex.Message}");
}
