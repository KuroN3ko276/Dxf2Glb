using DXF2GLB.Models;

namespace DXF2GLB.Algorithms;

/// <summary>
/// Filters out junk geometry based on bounding box and component size.
/// </summary>
public static class JunkFilter
{
    /// <summary>
    /// Filter mesh by bounding box, removing outlier vertices/triangles.
    /// Uses percentile-based bounds to avoid outliers affecting the main geometry.
    /// </summary>
    public static OptimizedMesh FilterByBoundingBox(OptimizedMesh mesh, double percentile = 0.95, double padding = 0.1)
    {
        if (mesh.Vertices.Count == 0) return mesh;
        
        // Calculate bounds using percentile to exclude outliers
        var bounds = CalculatePercentileBounds(mesh.Vertices, percentile);
        
        // Expand bounds by padding
        var size = new Vector3d(bounds.Max.X - bounds.Min.X, bounds.Max.Y - bounds.Min.Y, bounds.Max.Z - bounds.Min.Z);
        var padVec = size * padding;
        bounds.Min = bounds.Min - padVec;
        bounds.Max = bounds.Max + padVec;
        
        Console.WriteLine($"    Junk filter bounds: ({bounds.Min.X:F1}, {bounds.Min.Y:F1}, {bounds.Min.Z:F1}) - ({bounds.Max.X:F1}, {bounds.Max.Y:F1}, {bounds.Max.Z:F1})");
        
        // Filter triangles - keep if at least one vertex is inside bounds
        var result = new OptimizedMesh { Layer = mesh.Layer };
        var oldToNew = new Dictionary<int, int>();
        var keptTriangles = 0;
        var removedTriangles = 0;
        
        for (int t = 0; t < mesh.TriangleIndices.Count; t += 3)
        {
            var i0 = mesh.TriangleIndices[t];
            var i1 = mesh.TriangleIndices[t + 1];
            var i2 = mesh.TriangleIndices[t + 2];
            
            var v0 = mesh.Vertices[i0];
            var v1 = mesh.Vertices[i1];
            var v2 = mesh.Vertices[i2];
            
            // Keep triangle if any vertex is inside bounds
            if (IsInsideBounds(v0, bounds) || IsInsideBounds(v1, bounds) || IsInsideBounds(v2, bounds))
            {
                // Add vertices if not already added
                if (!oldToNew.ContainsKey(i0))
                {
                    oldToNew[i0] = result.Vertices.Count;
                    result.Vertices.Add(v0);
                }
                if (!oldToNew.ContainsKey(i1))
                {
                    oldToNew[i1] = result.Vertices.Count;
                    result.Vertices.Add(v1);
                }
                if (!oldToNew.ContainsKey(i2))
                {
                    oldToNew[i2] = result.Vertices.Count;
                    result.Vertices.Add(v2);
                }
                
                result.TriangleIndices.Add(oldToNew[i0]);
                result.TriangleIndices.Add(oldToNew[i1]);
                result.TriangleIndices.Add(oldToNew[i2]);
                keptTriangles++;
            }
            else
            {
                removedTriangles++;
            }
        }
        
        Console.WriteLine($"    Junk filter: kept {keptTriangles:N0}, removed {removedTriangles:N0} triangles");
        
        return result;
    }
    
    /// <summary>
    /// Remove small disconnected components (islands) from mesh.
    /// </summary>
    public static OptimizedMesh RemoveSmallIslands(OptimizedMesh mesh, int minTriangles = 100)
    {
        if (mesh.TriangleCount < minTriangles * 2) return mesh;
        
        var triangleCount = mesh.TriangleCount;
        
        // Build adjacency graph using Union-Find
        var parent = new int[triangleCount];
        var rank = new int[triangleCount];
        for (int i = 0; i < triangleCount; i++) parent[i] = i;
        
        // Build edge-to-triangle mapping
        var edgeToTriangles = new Dictionary<(int, int), List<int>>();
        for (int t = 0; t < triangleCount; t++)
        {
            var i0 = mesh.TriangleIndices[t * 3];
            var i1 = mesh.TriangleIndices[t * 3 + 1];
            var i2 = mesh.TriangleIndices[t * 3 + 2];
            
            AddTriangleToEdge(edgeToTriangles, i0, i1, t);
            AddTriangleToEdge(edgeToTriangles, i1, i2, t);
            AddTriangleToEdge(edgeToTriangles, i2, i0, t);
        }
        
        // Union triangles sharing edges
        foreach (var triangles in edgeToTriangles.Values)
        {
            for (int i = 1; i < triangles.Count; i++)
            {
                Union(parent, rank, triangles[0], triangles[i]);
            }
        }
        
        // Count component sizes
        var componentSize = new Dictionary<int, int>();
        for (int t = 0; t < triangleCount; t++)
        {
            var root = Find(parent, t);
            componentSize.TryGetValue(root, out var count);
            componentSize[root] = count + 1;
        }
        
        var largeComponents = componentSize.Where(kv => kv.Value >= minTriangles).Select(kv => kv.Key).ToHashSet();
        
        Console.WriteLine($"    Island filter: {componentSize.Count} components, {largeComponents.Count} with >= {minTriangles} triangles");
        
        // Keep only triangles from large components
        var result = new OptimizedMesh { Layer = mesh.Layer };
        var oldToNew = new Dictionary<int, int>();
        
        for (int t = 0; t < triangleCount; t++)
        {
            var root = Find(parent, t);
            if (!largeComponents.Contains(root)) continue;
            
            var i0 = mesh.TriangleIndices[t * 3];
            var i1 = mesh.TriangleIndices[t * 3 + 1];
            var i2 = mesh.TriangleIndices[t * 3 + 2];
            
            if (!oldToNew.ContainsKey(i0))
            {
                oldToNew[i0] = result.Vertices.Count;
                result.Vertices.Add(mesh.Vertices[i0]);
            }
            if (!oldToNew.ContainsKey(i1))
            {
                oldToNew[i1] = result.Vertices.Count;
                result.Vertices.Add(mesh.Vertices[i1]);
            }
            if (!oldToNew.ContainsKey(i2))
            {
                oldToNew[i2] = result.Vertices.Count;
                result.Vertices.Add(mesh.Vertices[i2]);
            }
            
            result.TriangleIndices.Add(oldToNew[i0]);
            result.TriangleIndices.Add(oldToNew[i1]);
            result.TriangleIndices.Add(oldToNew[i2]);
        }
        
        Console.WriteLine($"    Island filter: kept {result.TriangleCount:N0} triangles");
        
        return result;
    }
    
    private static (Vector3d Min, Vector3d Max) CalculatePercentileBounds(List<Vector3d> vertices, double percentile)
    {
        var xs = vertices.Select(v => v.X).OrderBy(x => x).ToList();
        var ys = vertices.Select(v => v.Y).OrderBy(y => y).ToList();
        var zs = vertices.Select(v => v.Z).OrderBy(z => z).ToList();
        
        var lowIdx = (int)((1 - percentile) / 2 * vertices.Count);
        var highIdx = (int)((1 + percentile) / 2 * vertices.Count) - 1;
        highIdx = Math.Max(highIdx, lowIdx + 1);
        
        return (
            new Vector3d(xs[lowIdx], ys[lowIdx], zs[lowIdx]),
            new Vector3d(xs[highIdx], ys[highIdx], zs[highIdx])
        );
    }
    
    private static bool IsInsideBounds(Vector3d v, (Vector3d Min, Vector3d Max) bounds)
    {
        return v.X >= bounds.Min.X && v.X <= bounds.Max.X &&
               v.Y >= bounds.Min.Y && v.Y <= bounds.Max.Y &&
               v.Z >= bounds.Min.Z && v.Z <= bounds.Max.Z;
    }
    
    private static void AddTriangleToEdge(Dictionary<(int, int), List<int>> map, int v1, int v2, int triangle)
    {
        if (v1 > v2) (v1, v2) = (v2, v1);
        if (!map.TryGetValue((v1, v2), out var list))
        {
            list = new List<int>();
            map[(v1, v2)] = list;
        }
        list.Add(triangle);
    }
    
    private static int Find(int[] parent, int i)
    {
        if (parent[i] != i)
            parent[i] = Find(parent, parent[i]);
        return parent[i];
    }
    
    private static void Union(int[] parent, int[] rank, int x, int y)
    {
        var xRoot = Find(parent, x);
        var yRoot = Find(parent, y);
        
        if (xRoot == yRoot) return;
        
        if (rank[xRoot] < rank[yRoot])
            parent[xRoot] = yRoot;
        else if (rank[xRoot] > rank[yRoot])
            parent[yRoot] = xRoot;
        else
        {
            parent[yRoot] = xRoot;
            rank[xRoot]++;
        }
    }
}
