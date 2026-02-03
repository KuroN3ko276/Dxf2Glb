using DXF2GLB.Models;

namespace DXF2GLB.Algorithms;

/// <summary>
/// Ear clipping triangulation algorithm for simple polygons.
/// Converts a closed polygon (list of points) into triangles.
/// </summary>
public static class EarClipperTriangulator
{
    /// <summary>
    /// Triangulates a simple polygon using ear clipping algorithm.
    /// Returns list of triangle indices (triplets of indices into the original points list).
    /// </summary>
    /// <param name="points">Polygon vertices (should be closed, coplanar or nearly coplanar)</param>
    /// <returns>List of indices, every 3 consecutive indices form a triangle</returns>
    public static List<int> Triangulate(List<Vector3d> points)
    {
        if (points.Count < 3)
            return new List<int>();

        if (points.Count == 3)
            return new List<int> { 0, 1, 2 };

        // Project 3D points to 2D for triangulation
        var (points2D, _) = ProjectTo2D(points);
        
        // Create index list
        var indices = Enumerable.Range(0, points.Count).ToList();
        var triangles = new List<int>();
        
        // Ensure counter-clockwise winding
        if (GetSignedArea(points2D) < 0)
        {
            indices.Reverse();
        }
        
        // Ear clipping loop
        var maxIterations = points.Count * points.Count; // Safety limit
        var iterations = 0;
        
        while (indices.Count > 3 && iterations < maxIterations)
        {
            var earFound = false;
            
            for (int i = 0; i < indices.Count; i++)
            {
                var prev = (i - 1 + indices.Count) % indices.Count;
                var next = (i + 1) % indices.Count;
                
                var iPrev = indices[prev];
                var iCurr = indices[i];
                var iNext = indices[next];
                
                // Check if this vertex forms an ear
                if (IsEar(points2D, indices, prev, i, next))
                {
                    // Add triangle
                    triangles.Add(iPrev);
                    triangles.Add(iCurr);
                    triangles.Add(iNext);
                    
                    // Remove the ear tip
                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }
            }
            
            if (!earFound)
            {
                // No ear found, polygon might be degenerate
                // Try adding remaining as triangle fan
                break;
            }
            
            iterations++;
        }
        
        // Add remaining triangle
        if (indices.Count == 3)
        {
            triangles.Add(indices[0]);
            triangles.Add(indices[1]);
            triangles.Add(indices[2]);
        }
        
        return triangles;
    }

    /// <summary>
    /// Fast triangulation using fan method (works for convex polygons).
    /// </summary>
    public static List<int> TriangulateFan(int pointCount)
    {
        if (pointCount < 3) return new List<int>();
        
        var triangles = new List<int>();
        for (int i = 1; i < pointCount - 1; i++)
        {
            triangles.Add(0);
            triangles.Add(i);
            triangles.Add(i + 1);
        }
        return triangles;
    }

    private static bool IsEar(List<(double X, double Y)> points, List<int> indices, int prev, int curr, int next)
    {
        var a = points[indices[prev]];
        var b = points[indices[curr]];
        var c = points[indices[next]];
        
        // Check if the vertex is convex
        if (!IsConvex(a, b, c))
            return false;
        
        // Check if any other vertex is inside the triangle
        for (int i = 0; i < indices.Count; i++)
        {
            if (i == prev || i == curr || i == next)
                continue;
            
            if (PointInTriangle(points[indices[i]], a, b, c))
                return false;
        }
        
        return true;
    }

    private static bool IsConvex((double X, double Y) a, (double X, double Y) b, (double X, double Y) c)
    {
        // Cross product to determine if angle is convex
        var cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        return cross > 0;
    }

    private static bool PointInTriangle((double X, double Y) p, (double X, double Y) a, (double X, double Y) b, (double X, double Y) c)
    {
        var d1 = Sign(p, a, b);
        var d2 = Sign(p, b, c);
        var d3 = Sign(p, c, a);
        
        var hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        var hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        
        return !(hasNeg && hasPos);
    }

    private static double Sign((double X, double Y) p1, (double X, double Y) p2, (double X, double Y) p3)
    {
        return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
    }

    private static double GetSignedArea(List<(double X, double Y)> points)
    {
        double area = 0;
        for (int i = 0; i < points.Count; i++)
        {
            var j = (i + 1) % points.Count;
            area += points[i].X * points[j].Y;
            area -= points[j].X * points[i].Y;
        }
        return area / 2.0;
    }

    /// <summary>
    /// Projects 3D points onto a 2D plane for triangulation.
    /// Uses the first 3 non-collinear points to determine the plane.
    /// </summary>
    private static (List<(double X, double Y)>, Vector3d Normal) ProjectTo2D(List<Vector3d> points)
    {
        // Find the plane normal using first 3 points or best fit
        var normal = CalculatePlaneNormal(points);
        
        // Find two orthogonal axes on the plane
        var up = Math.Abs(normal.Z) < 0.9 ? new Vector3d(0, 0, 1) : new Vector3d(1, 0, 0);
        var uAxis = Vector3d.Cross(normal, up).Normalized();
        var vAxis = Vector3d.Cross(normal, uAxis).Normalized();
        
        // Project each point
        var result = new List<(double X, double Y)>();
        foreach (var p in points)
        {
            var x = Vector3d.Dot(p, uAxis);
            var y = Vector3d.Dot(p, vAxis);
            result.Add((x, y));
        }
        
        return (result, normal);
    }

    private static Vector3d CalculatePlaneNormal(List<Vector3d> points)
    {
        if (points.Count < 3)
            return new Vector3d(0, 0, 1);
        
        // Use Newell's method for robust normal calculation
        var normal = Vector3d.Zero;
        for (int i = 0; i < points.Count; i++)
        {
            var current = points[i];
            var next = points[(i + 1) % points.Count];
            
            normal = new Vector3d(
                normal.X + (current.Y - next.Y) * (current.Z + next.Z),
                normal.Y + (current.Z - next.Z) * (current.X + next.X),
                normal.Z + (current.X - next.X) * (current.Y + next.Y)
            );
        }
        
        var len = normal.Length;
        return len > 0 ? normal * (1.0 / len) : new Vector3d(0, 0, 1);
    }
}
