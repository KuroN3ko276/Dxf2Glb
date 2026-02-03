namespace DXF2GLB.Models;

/// <summary>
/// Statistics about the preprocessing operation
/// </summary>
public class GeometryStats
{
    public int OriginalVertices { get; set; }
    public int OptimizedVertices { get; set; }
    public double ReductionPercent => OriginalVertices > 0 
        ? (1.0 - (double)OptimizedVertices / OriginalVertices) * 100.0 
        : 0.0;

    public int OriginalEntities { get; set; }
    public int OptimizedPolylines { get; set; }
    public int MeshCount { get; set; }
    public int TotalTriangles { get; set; }

    public Dictionary<string, int> EntityCounts { get; set; } = new();
}

/// <summary>
/// A simplified polyline ready for export
/// </summary>
public record OptimizedPolyline(
    string Layer,
    List<Vector3d> Points,
    bool IsClosed
);

/// <summary>
/// A mesh with vertices and triangle indices (from PolyfaceMesh)
/// </summary>
public class OptimizedMesh
{
    public string Layer { get; set; } = "";
    public List<Vector3d> Vertices { get; set; } = new();
    /// <summary>
    /// Triangle indices - every 3 consecutive integers form a triangle
    /// </summary>
    public List<int> TriangleIndices { get; set; } = new();
    
    public int TriangleCount => TriangleIndices.Count / 3;
}

/// <summary>
/// 3D vector for geometry representation
/// </summary>
public record struct Vector3d(double X, double Y, double Z)
{
    public static Vector3d Zero => new(0, 0, 0);

    public double DistanceTo(Vector3d other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        var dz = Z - other.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public double DistanceSquaredTo(Vector3d other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        var dz = Z - other.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    public static Vector3d operator +(Vector3d a, Vector3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3d operator -(Vector3d a, Vector3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3d operator *(Vector3d v, double s) => new(v.X * s, v.Y * s, v.Z * s);
    public static Vector3d operator *(double s, Vector3d v) => v * s;

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

    public Vector3d Normalized()
    {
        var len = Length;
        return len > 0 ? this * (1.0 / len) : Zero;
    }

    public static double Dot(Vector3d a, Vector3d b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static Vector3d Cross(Vector3d a, Vector3d b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X
    );
}

/// <summary>
/// Container for all optimized geometry data
/// </summary>
public class OptimizedGeometry
{
    public List<OptimizedPolyline> Polylines { get; set; } = new();
    public List<OptimizedMesh> Meshes { get; set; } = new();
    public GeometryStats Stats { get; set; } = new();
    
    /// <summary>
    /// True if geometry came from PolyfaceMesh (has proper face data)
    /// </summary>
    public bool HasMeshData => Meshes.Count > 0;
}
