using DXF2GLB.Models;

namespace DXF2GLB.Algorithms;

/// <summary>
/// Ramer-Douglas-Peucker algorithm for polyline simplification.
/// This version uses iterative approach with explicit stack to avoid stack overflow
/// for very large polylines (millions of points).
/// </summary>
public static class RamerDouglasPeucker
{
    /// <summary>
    /// Simplifies a polyline by removing points that are within epsilon distance
    /// from the line segment connecting their neighbors.
    /// Uses iterative algorithm to handle very large polylines without stack overflow.
    /// </summary>
    /// <param name="points">Input points</param>
    /// <param name="epsilon">Maximum allowed perpendicular distance</param>
    /// <returns>Simplified list of points</returns>
    public static List<Vector3d> Simplify(IReadOnlyList<Vector3d> points, double epsilon)
    {
        if (points.Count < 3)
            return points.ToList();

        // Use iterative approach with explicit stack to avoid stack overflow
        var keepPoints = new bool[points.Count];
        keepPoints[0] = true;
        keepPoints[points.Count - 1] = true;

        // Stack stores (startIndex, endIndex) pairs to process
        var stack = new Stack<(int start, int end)>();
        stack.Push((0, points.Count - 1));

        while (stack.Count > 0)
        {
            var (startIndex, endIndex) = stack.Pop();

            if (endIndex <= startIndex + 1)
                continue;

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

            // If max distance is greater than epsilon, keep this point and process both halves
            if (maxDistance > epsilon)
            {
                keepPoints[maxIndex] = true;
                stack.Push((startIndex, maxIndex));
                stack.Push((maxIndex, endIndex));
            }
            // else: all points between start and end are within tolerance, discard them
        }

        // Build result from keep flags
        var result = new List<Vector3d>();
        for (var i = 0; i < points.Count; i++)
        {
            if (keepPoints[i])
                result.Add(points[i]);
        }

        return result;
    }

    /// <summary>
    /// Simplifies a very large polyline using chunked processing with progress reporting.
    /// Divides the polyline into chunks, simplifies each, then merges results.
    /// </summary>
    /// <param name="points">Input points (can be millions)</param>
    /// <param name="epsilon">Maximum allowed perpendicular distance</param>
    /// <param name="chunkSize">Number of points per chunk (default: 100,000)</param>
    /// <param name="progressCallback">Optional callback: (processedPoints, totalPoints)</param>
    /// <returns>Simplified list of points</returns>
    public static List<Vector3d> SimplifyLarge(
        IReadOnlyList<Vector3d> points,
        double epsilon,
        int chunkSize = 100_000,
        Action<int, int>? progressCallback = null)
    {
        if (points.Count < 3)
            return points.ToList();

        // For small polylines, use regular simplify
        if (points.Count <= chunkSize * 2)
        {
            return Simplify(points, epsilon);
        }

        var result = new List<Vector3d>();
        var totalPoints = points.Count;
        var processedPoints = 0;

        // Calculate overlap size to ensure smooth transitions between chunks
        var overlapSize = Math.Min(1000, chunkSize / 10);

        var chunkStart = 0;
        while (chunkStart < totalPoints)
        {
            var chunkEnd = Math.Min(chunkStart + chunkSize, totalPoints);
            
            // Extract chunk with overlap
            var actualEnd = Math.Min(chunkEnd + overlapSize, totalPoints);
            var chunkPoints = new List<Vector3d>();
            for (var i = chunkStart; i < actualEnd; i++)
            {
                chunkPoints.Add(points[i]);
            }

            // Simplify this chunk
            var simplifiedChunk = Simplify(chunkPoints, epsilon);

            // Add to result (skip first point if not first chunk to avoid duplication)
            var startIdx = (chunkStart == 0) ? 0 : 1;
            
            // For non-last chunks, don't add the overlap region points yet
            var endIdx = (chunkEnd < totalPoints) 
                ? simplifiedChunk.Count - (int)(simplifiedChunk.Count * overlapSize / (double)chunkPoints.Count)
                : simplifiedChunk.Count;
            
            for (var i = startIdx; i < endIdx; i++)
            {
                result.Add(simplifiedChunk[i]);
            }

            processedPoints = chunkEnd;
            progressCallback?.Invoke(processedPoints, totalPoints);

            chunkStart = chunkEnd;
        }

        // Ensure last point is included
        if (result.Count > 0 && !result[^1].Equals(points[^1]))
        {
            result.Add(points[^1]);
        }

        return result;
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
