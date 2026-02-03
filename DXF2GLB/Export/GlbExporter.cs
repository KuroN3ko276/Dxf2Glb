using SharpGLTF.Schema2;
using DXF2GLB.Models;
using DXF2GLB.Algorithms;
using System.Numerics;

namespace DXF2GLB.Export;

/// <summary>
/// Exports optimized geometry directly to GLB format using SharpGLTF.
/// </summary>
public static class GlbExporter
{
    /// <summary>
    /// Exports optimized geometry to a GLB file.
    /// </summary>
    /// <param name="geometry">The optimized geometry to export</param>
    /// <param name="outputPath">Output GLB file path</param>
    /// <param name="wireframe">If true, export as lines (wireframe). If false, use meshes or triangulate.</param>
    public static void Export(OptimizedGeometry geometry, string outputPath, bool wireframe = false)
    {
        var model = CreateModel(geometry, wireframe);
        model.SaveGLB(outputPath);
    }

    /// <summary>
    /// Exports optimized geometry to a GLTF file (JSON + separate bin).
    /// </summary>
    public static void ExportGltf(OptimizedGeometry geometry, string outputPath, bool wireframe = false)
    {
        var model = CreateModel(geometry, wireframe);
        model.SaveGLTF(outputPath);
    }

    private static ModelRoot CreateModel(OptimizedGeometry geometry, bool wireframe)
    {
        var model = ModelRoot.CreateModel();
        var scene = model.UseScene("Default");
        var rootNode = scene.CreateNode("Root");
        
        // Calculate center for auto-centering
        var center = CalculateCenter(geometry);
        Console.WriteLine($"  Center: ({center.X:F2}, {center.Y:F2}, {center.Z:F2})");
        
        if (geometry.HasMeshData && !wireframe)
        {
            Console.WriteLine($"  Export mode: Mesh (using PolyfaceMesh triangles)");
            CreateMeshNodes(model, rootNode, geometry, center);
        }
        else if (wireframe)
        {
            Console.WriteLine($"  Export mode: Wireframe (LINES)");
            CreateLineNodes(model, rootNode, geometry, center);
        }
        else
        {
            Console.WriteLine($"  Export mode: Triangulated (ear clipping)");
            CreateTriangulatedNodes(model, rootNode, geometry, center);
        }
        
        return model;
    }

    /// <summary>
    /// Create mesh nodes from PolyfaceMesh data (proper triangle data)
    /// </summary>
    private static void CreateMeshNodes(
        ModelRoot model,
        Node rootNode,
        OptimizedGeometry geometry,
        Vector3d center)
    {
        // Group meshes by layer
        var layerGroups = geometry.Meshes.GroupBy(m => m.Layer);
        
        foreach (var layerGroup in layerGroups)
        {
            var layerName = layerGroup.Key;
            var meshList = layerGroup.ToList();
            
            var positions = new List<Vector3>();
            var triangleIndices = new List<int>();
            
            foreach (var meshData in meshList)
            {
                var startIndex = positions.Count;
                
                // Add vertices
                foreach (var vertex in meshData.Vertices)
                {
                    positions.Add(new Vector3(
                        (float)(vertex.X - center.X),
                        (float)(vertex.Y - center.Y),
                        (float)(vertex.Z - center.Z)
                    ));
                }
                
                // Add triangle indices (offset by startIndex)
                foreach (var idx in meshData.TriangleIndices)
                {
                    triangleIndices.Add(startIndex + idx);
                }
            }
            
            if (positions.Count == 0 || triangleIndices.Count == 0) continue;
            
            var triangleCount = triangleIndices.Count / 3;
            Console.WriteLine($"  Layer '{layerName}': {positions.Count:N0} vertices, {triangleCount:N0} triangles");
            
            var mesh = CreateTriangleMesh(model, layerName, positions, triangleIndices);
            var node = rootNode.CreateNode(layerName);
            node.Mesh = mesh;
        }
        
        // Also handle any polylines (as lines)
        if (geometry.Polylines.Count > 0)
        {
            CreateLineNodes(model, rootNode, geometry, center);
        }
    }

    /// <summary>
    /// Create line nodes from polylines (wireframe mode)
    /// </summary>
    private static void CreateLineNodes(
        ModelRoot model,
        Node rootNode,
        OptimizedGeometry geometry,
        Vector3d center)
    {
        var layerGroups = geometry.Polylines.GroupBy(p => p.Layer);
        
        foreach (var layerGroup in layerGroups)
        {
            var layerName = layerGroup.Key;
            var polylines = layerGroup.ToList();
            
            var positions = new List<Vector3>();
            var lineIndices = new List<(int, int)>();
            
            foreach (var polyline in polylines)
            {
                if (polyline.Points.Count < 2) continue;
                
                var startIndex = positions.Count;
                
                foreach (var point in polyline.Points)
                {
                    positions.Add(new Vector3(
                        (float)(point.X - center.X),
                        (float)(point.Y - center.Y),
                        (float)(point.Z - center.Z)
                    ));
                }
                
                for (int i = 0; i < polyline.Points.Count - 1; i++)
                {
                    lineIndices.Add((startIndex + i, startIndex + i + 1));
                }
                
                if (polyline.IsClosed && polyline.Points.Count > 2)
                {
                    lineIndices.Add((startIndex + polyline.Points.Count - 1, startIndex));
                }
            }
            
            if (positions.Count == 0) continue;
            
            Console.WriteLine($"  Layer '{layerName}': {positions.Count:N0} vertices, {lineIndices.Count:N0} line segments");
            
            var mesh = CreateLineMesh(model, layerName, positions, lineIndices);
            var node = rootNode.CreateNode(layerName + "_lines");
            node.Mesh = mesh;
        }
    }

    /// <summary>
    /// Create nodes from polylines - closed as triangles, open as lines
    /// </summary>
    private static void CreateTriangulatedNodes(
        ModelRoot model,
        Node rootNode,
        OptimizedGeometry geometry,
        Vector3d center)
    {
        var layerGroups = geometry.Polylines.GroupBy(p => p.Layer);
        
        foreach (var layerGroup in layerGroups)
        {
            var layerName = layerGroup.Key;
            var polylines = layerGroup.ToList();
            
            // Separate closed and open polylines
            var closedPolylines = polylines.Where(p => p.IsClosed && p.Points.Count >= 3).ToList();
            var openPolylines = polylines.Where(p => !p.IsClosed && p.Points.Count >= 2).ToList();
            
            // Process CLOSED polylines as triangles
            if (closedPolylines.Count > 0)
            {
                var positions = new List<Vector3>();
                var triangleIndices = new List<int>();
                
                foreach (var polyline in closedPolylines)
                {
                    var startIndex = positions.Count;
                    
                    foreach (var point in polyline.Points)
                    {
                        positions.Add(new Vector3(
                            (float)(point.X - center.X),
                            (float)(point.Y - center.Y),
                            (float)(point.Z - center.Z)
                        ));
                    }
                    
                    var localTriangles = EarClipperTriangulator.Triangulate(polyline.Points);
                    foreach (var idx in localTriangles)
                    {
                        triangleIndices.Add(startIndex + idx);
                    }
                }
                
                if (positions.Count > 0 && triangleIndices.Count > 0)
                {
                    var triangleCount = triangleIndices.Count / 3;
                    Console.WriteLine($"  Layer '{layerName}' (closed): {positions.Count:N0} vertices, {triangleCount:N0} triangles");
                    
                    var mesh = CreateTriangleMesh(model, layerName, positions, triangleIndices);
                    var node = rootNode.CreateNode(layerName);
                    node.Mesh = mesh;
                }
            }
            
            // Process OPEN polylines as lines
            if (openPolylines.Count > 0)
            {
                var positions = new List<Vector3>();
                var lineIndices = new List<(int, int)>();
                
                foreach (var polyline in openPolylines)
                {
                    var startIndex = positions.Count;
                    
                    foreach (var point in polyline.Points)
                    {
                        positions.Add(new Vector3(
                            (float)(point.X - center.X),
                            (float)(point.Y - center.Y),
                            (float)(point.Z - center.Z)
                        ));
                    }
                    
                    for (int i = 0; i < polyline.Points.Count - 1; i++)
                    {
                        lineIndices.Add((startIndex + i, startIndex + i + 1));
                    }
                }
                
                if (positions.Count > 0 && lineIndices.Count > 0)
                {
                    Console.WriteLine($"  Layer '{layerName}' (open): {positions.Count:N0} vertices, {lineIndices.Count:N0} line segments");
                    
                    var mesh = CreateLineMesh(model, layerName + "_lines", positions, lineIndices);
                    var node = rootNode.CreateNode(layerName + "_lines");
                    node.Mesh = mesh;
                }
            }
        }
    }

    private static Mesh CreateTriangleMesh(
        ModelRoot model,
        string meshName,
        List<Vector3> positions,
        List<int> triangleIndices)
    {
        var mesh = model.CreateMesh(meshName);
        
        var positionBuffer = CreatePositionBuffer(positions);
        var positionBufferView = model.UseBufferView(positionBuffer);
        var positionAccessor = model.CreateAccessor();
        positionAccessor.SetData(positionBufferView, 0, positions.Count, DimensionType.VEC3, EncodingType.FLOAT, false);
        
        var indices = triangleIndices.Select(i => (uint)i).ToList();
        var indexBuffer = CreateIndexBuffer(indices);
        var indexBufferView = model.UseBufferView(indexBuffer);
        var indexAccessor = model.CreateAccessor();
        indexAccessor.SetData(indexBufferView, 0, indices.Count, DimensionType.SCALAR, EncodingType.UNSIGNED_INT, false);
        
        var primitive = mesh.CreatePrimitive();
        primitive.SetVertexAccessor("POSITION", positionAccessor);
        primitive.SetIndexAccessor(indexAccessor);
        primitive.DrawPrimitiveType = PrimitiveType.TRIANGLES;
        
        var material = model.CreateMaterial("SolidMaterial_" + meshName);
        material.WithUnlit();
        material.DoubleSided = true;
        primitive.Material = material;
        
        return mesh;
    }

    private static Mesh CreateLineMesh(
        ModelRoot model,
        string meshName,
        List<Vector3> positions,
        List<(int, int)> lineIndices)
    {
        var mesh = model.CreateMesh(meshName);
        
        var positionBuffer = CreatePositionBuffer(positions);
        var positionBufferView = model.UseBufferView(positionBuffer);
        var positionAccessor = model.CreateAccessor();
        positionAccessor.SetData(positionBufferView, 0, positions.Count, DimensionType.VEC3, EncodingType.FLOAT, false);
        
        var indices = new List<uint>();
        foreach (var (a, b) in lineIndices)
        {
            indices.Add((uint)a);
            indices.Add((uint)b);
        }
        
        var indexBuffer = CreateIndexBuffer(indices);
        var indexBufferView = model.UseBufferView(indexBuffer);
        var indexAccessor = model.CreateAccessor();
        indexAccessor.SetData(indexBufferView, 0, indices.Count, DimensionType.SCALAR, EncodingType.UNSIGNED_INT, false);
        
        var primitive = mesh.CreatePrimitive();
        primitive.SetVertexAccessor("POSITION", positionAccessor);
        primitive.SetIndexAccessor(indexAccessor);
        primitive.DrawPrimitiveType = PrimitiveType.LINES;
        
        var material = model.CreateMaterial("LineMaterial_" + meshName);
        material.WithUnlit();
        primitive.Material = material;
        
        return mesh;
    }

    private static byte[] CreatePositionBuffer(List<Vector3> positions)
    {
        var buffer = new byte[positions.Count * 12];
        for (int i = 0; i < positions.Count; i++)
        {
            var pos = positions[i];
            System.Buffer.BlockCopy(BitConverter.GetBytes(pos.X), 0, buffer, i * 12, 4);
            System.Buffer.BlockCopy(BitConverter.GetBytes(pos.Y), 0, buffer, i * 12 + 4, 4);
            System.Buffer.BlockCopy(BitConverter.GetBytes(pos.Z), 0, buffer, i * 12 + 8, 4);
        }
        return buffer;
    }

    private static byte[] CreateIndexBuffer(List<uint> indices)
    {
        var buffer = new byte[indices.Count * 4];
        for (int i = 0; i < indices.Count; i++)
        {
            System.Buffer.BlockCopy(BitConverter.GetBytes(indices[i]), 0, buffer, i * 4, 4);
        }
        return buffer;
    }

    private static Vector3d CalculateCenter(OptimizedGeometry geometry)
    {
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        var sampleCount = 0;
        const int maxSamples = 100000;
        
        // Sample from meshes
        foreach (var mesh in geometry.Meshes)
        {
            foreach (var vertex in mesh.Vertices)
            {
                minX = Math.Min(minX, vertex.X);
                minY = Math.Min(minY, vertex.Y);
                minZ = Math.Min(minZ, vertex.Z);
                maxX = Math.Max(maxX, vertex.X);
                maxY = Math.Max(maxY, vertex.Y);
                maxZ = Math.Max(maxZ, vertex.Z);
                
                sampleCount++;
                if (sampleCount >= maxSamples) break;
            }
            if (sampleCount >= maxSamples) break;
        }
        
        // Sample from polylines
        foreach (var polyline in geometry.Polylines)
        {
            foreach (var point in polyline.Points)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                minZ = Math.Min(minZ, point.Z);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
                maxZ = Math.Max(maxZ, point.Z);
                
                sampleCount++;
                if (sampleCount >= maxSamples) break;
            }
            if (sampleCount >= maxSamples) break;
        }
        
        if (sampleCount == 0) return Vector3d.Zero;
        
        return new Vector3d(
            (minX + maxX) / 2,
            (minY + maxY) / 2,
            (minZ + maxZ) / 2
        );
    }
}
