using DXF2GLB.Models;

namespace DXF2GLB.Algorithms;

/// <summary>
/// Ramer-Douglas-Peucker algorithm for polyline simplification.
/// Reduces points while preserving shape within given tolerance.
/// </summary>
public static class RamerDouglasPeucker
{
    /// <summary>
    /// Simplifies a polyline by removing points that are within epsilon distance
    /// from the line segment connecting their neighbors.
    /// </summary>
    /// <param name="points">Input points</param>
    /// <param name="epsilon">Maximum allowed perpendicular distance</param>
    /// <returns>Simplified list of points</returns>
    public static List<Vector3d> Simplify(IReadOnlyList<Vector3d> points, double epsilon)
    {
        if (points.Count < 3)
            return points.ToList();

        var result = new List<Vector3d>();
        SimplifyRecursive(points, 0, points.Count - 1, epsilon, result);
        result.Add(points[^1]);
        return result;
    }

    private static void SimplifyRecursive(
        IReadOnlyList<Vector3d> points,
        int startIndex,
        int endIndex,
        double epsilon,
        List<Vector3d> result)
    {
        if (endIndex <= startIndex + 1)
        {
            result.Add(points[startIndex]);
            return;
        }

        // Find the point with maximum distance from line (start -> end)
        var start = points[startIndex];
        var end = points[endIndex];
        var maxDistance = 0.0;
        var maxIndex = startIndex;

        for (var i = startIndex + 1; i < endIndex; i++)
        {
            var distance = PerpendicularDistance(points[i], start, end);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                maxIndex = i;
            }
        }

        // If max distance is greater than epsilon, recursively simplify
        if (maxDistance > epsilon)
        {
            SimplifyRecursive(points, startIndex, maxIndex, epsilon, result);
            SimplifyRecursive(points, maxIndex, endIndex, epsilon, result);
        }
        else
        {
            // All points between start and end are within tolerance
            result.Add(start);
        }
    }

    /// <summary>
    /// Calculates perpendicular distance from point P to line segment AB
    /// </summary>
    public static double PerpendicularDistance(Vector3d p, Vector3d a, Vector3d b)
    {
        var ab = b - a;
        var abLengthSquared = Vector3d.Dot(ab, ab);

        if (abLengthSquared < 1e-12)
        {
            // A and B are the same point
            return p.DistanceTo(a);
        }

        // Project P onto line AB
        var ap = p - a;
        var t = Vector3d.Dot(ap, ab) / abLengthSquared;
        t = Math.Clamp(t, 0, 1);

        // Find closest point on segment
        var closest = a + ab * t;
        return p.DistanceTo(closest);
    }
}
