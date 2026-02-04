using DXF2GLB.Models;

namespace DXF2GLB.Algorithms;

/// <summary>
/// Fast mesh simplification using Vertex Clustering (Grid Decimation).
/// O(n) complexity - extremely fast for large meshes.
/// </summary>
public static class VertexClustering
{
    /// <summary>
    /// Simplify mesh by clustering vertices into a 3D grid.
    /// </summary>
    /// <param name="mesh">Input mesh</param>
    /// <param name="gridResolution">Grid resolution (e.g., 256 = 256x256x256 cells)</param>
    /// <returns>Simplified mesh</returns>
    public static OptimizedMesh Simplify(OptimizedMesh mesh, int gridResolution = 256)
    {
        if (mesh.Vertices.Count == 0 || mesh.TriangleCount == 0)
            return mesh;
        
        var originalTriangles = mesh.TriangleCount;
        Console.WriteLine($"    Vertex Clustering: {originalTriangles:N0} triangles, grid {gridResolution}³");
        
        // Calculate bounding box
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        
        foreach (var v in mesh.Vertices)
        {
            minX = Math.Min(minX, v.X);
            minY = Math.Min(minY, v.Y);
            minZ = Math.Min(minZ, v.Z);
            maxX = Math.Max(maxX, v.X);
            maxY = Math.Max(maxY, v.Y);
            maxZ = Math.Max(maxZ, v.Z);
        }
        
        // Add small padding to avoid edge cases
        var padding = 0.001;
        var sizeX = (maxX - minX) + padding;
        var sizeY = (maxY - minY) + padding;
        var sizeZ = (maxZ - minZ) + padding;
        
        var cellSizeX = sizeX / gridResolution;
        var cellSizeY = sizeY / gridResolution;
        var cellSizeZ = sizeZ / gridResolution;
        
        // Map each vertex to a cell
        var vertexToCell = new int[mesh.Vertices.Count];
        var cellToVertices = new Dictionary<int, List<int>>();
        
        for (int i = 0; i < mesh.Vertices.Count; i++)
        {
            var v = mesh.Vertices[i];
            var cx = (int)Math.Min((v.X - minX) / cellSizeX, gridResolution - 1);
            var cy = (int)Math.Min((v.Y - minY) / cellSizeY, gridResolution - 1);
            var cz = (int)Math.Min((v.Z - minZ) / cellSizeZ, gridResolution - 1);
            
            var cellId = cx + cy * gridResolution + cz * gridResolution * gridResolution;
            vertexToCell[i] = cellId;
            
            if (!cellToVertices.ContainsKey(cellId))
                cellToVertices[cellId] = new List<int>();
            cellToVertices[cellId].Add(i);
        }
        
        // Create new vertices (centroid of each cell)
        var result = new OptimizedMesh { Layer = mesh.Layer };
        var cellToNewVertex = new Dictionary<int, int>();
        
        foreach (var (cellId, vertexIndices) in cellToVertices)
        {
            double sumX = 0, sumY = 0, sumZ = 0;
            foreach (var idx in vertexIndices)
            {
                var v = mesh.Vertices[idx];
                sumX += v.X;
                sumY += v.Y;
                sumZ += v.Z;
            }
            
            var count = vertexIndices.Count;
            var centroid = new Vector3d(sumX / count, sumY / count, sumZ / count);
            
            cellToNewVertex[cellId] = result.Vertices.Count;
            result.Vertices.Add(centroid);
        }
        
        // Remap triangles, skip degenerate ones
        var keptTriangles = 0;
        var removedTriangles = 0;
        
        for (int t = 0; t < mesh.TriangleIndices.Count; t += 3)
        {
            var i0 = mesh.TriangleIndices[t];
            var i1 = mesh.TriangleIndices[t + 1];
            var i2 = mesh.TriangleIndices[t + 2];
            
            var c0 = vertexToCell[i0];
            var c1 = vertexToCell[i1];
            var c2 = vertexToCell[i2];
            
            // Skip degenerate triangles (all vertices in same cell or two in same)
            if (c0 == c1 || c1 == c2 || c2 == c0)
            {
                removedTriangles++;
                continue;
            }
            
            var n0 = cellToNewVertex[c0];
            var n1 = cellToNewVertex[c1];
            var n2 = cellToNewVertex[c2];
            
            result.TriangleIndices.Add(n0);
            result.TriangleIndices.Add(n1);
            result.TriangleIndices.Add(n2);
            keptTriangles++;
        }
        
        var reduction = 100.0 * (1.0 - (double)keptTriangles / originalTriangles);
        Console.WriteLine($"    Result: {keptTriangles:N0} triangles ({reduction:F1}% reduction), {result.Vertices.Count:N0} vertices");
        
        return result;
    }
    
    /// <summary>
    /// Calculate recommended grid resolution based on target reduction.
    /// </summary>
    public static int CalculateGridResolution(int triangleCount, double targetReduction)
    {
        // Empirical formula: higher resolution = less reduction
        // Target ~50% reduction: start with cube root of triangle count
        var baseRes = (int)Math.Pow(triangleCount, 1.0 / 3.0);
        
        // Adjust based on target reduction
        // Lower reduction (0.3) = higher resolution
        // Higher reduction (0.8) = lower resolution
        var factor = 1.0 / (1.0 - targetReduction * 0.5);
        var resolution = (int)(baseRes * factor);
        
        // Clamp to reasonable range
        return Math.Clamp(resolution, 32, 1024);
    }
}
