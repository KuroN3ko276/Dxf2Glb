using DXF2GLB.Models;

namespace DXF2GLB.Algorithms;

/// <summary>
/// Converts splines to polylines with adaptive sampling.
/// Samples more densely in high-curvature regions.
/// </summary>
public static class SplineSampler
{
    /// <summary>
    /// Samples a cubic Bezier curve adaptively based on flatness tolerance.
    /// </summary>
    /// <param name="p0">Start point</param>
    /// <param name="p1">First control point</param>
    /// <param name="p2">Second control point</param>
    /// <param name="p3">End point</param>
    /// <param name="tolerance">Flatness tolerance</param>
    /// <returns>List of sampled points</returns>
    public static List<Vector3d> SampleCubicBezier(
        Vector3d p0, Vector3d p1, Vector3d p2, Vector3d p3,
        double tolerance)
    {
        var result = new List<Vector3d> { p0 };
        SampleCubicBezierRecursive(p0, p1, p2, p3, tolerance, result);
        result.Add(p3);
        return result;
    }

    private static void SampleCubicBezierRecursive(
        Vector3d p0, Vector3d p1, Vector3d p2, Vector3d p3,
        double tolerance,
        List<Vector3d> result)
    {
        // Check flatness using midpoint deviation
        if (IsFlatEnough(p0, p1, p2, p3, tolerance))
            return;

        // Subdivide using de Casteljau algorithm
        var p01 = Midpoint(p0, p1);
        var p12 = Midpoint(p1, p2);
        var p23 = Midpoint(p2, p3);
        var p012 = Midpoint(p01, p12);
        var p123 = Midpoint(p12, p23);
        var p0123 = Midpoint(p012, p123);

        // Recursively sample both halves
        SampleCubicBezierRecursive(p0, p01, p012, p0123, tolerance, result);
        result.Add(p0123);
        SampleCubicBezierRecursive(p0123, p123, p23, p3, tolerance, result);
    }

    /// <summary>
    /// Samples a quadratic Bezier curve adaptively.
    /// </summary>
    public static List<Vector3d> SampleQuadraticBezier(
        Vector3d p0, Vector3d p1, Vector3d p2,
        double tolerance)
    {
        // Convert to cubic Bezier
        var cp1 = p0 + (p1 - p0) * (2.0 / 3.0);
        var cp2 = p2 + (p1 - p2) * (2.0 / 3.0);
        return SampleCubicBezier(p0, cp1, cp2, p2, tolerance);
    }

    /// <summary>
    /// Samples a B-spline with uniform knot vector.
    /// </summary>
    /// <param name="controlPoints">Control points</param>
    /// <param name="degree">Spline degree (typically 3 for cubic)</param>
    /// <param name="sampleCount">Number of samples (higher = more accurate)</param>
    /// <returns>Sampled points</returns>
    public static List<Vector3d> SampleBSpline(
        IReadOnlyList<Vector3d> controlPoints,
        int degree,
        int sampleCount = 100)
    {
        if (controlPoints.Count < degree + 1)
            return controlPoints.ToList();

        var result = new List<Vector3d>(sampleCount);
        var n = controlPoints.Count - 1;
        var knotCount = n + degree + 2;

        // Create uniform knot vector
        var knots = CreateUniformKnots(n, degree);

        // Sample the spline
        for (var i = 0; i < sampleCount; i++)
        {
            var t = (double)i / (sampleCount - 1);
            var u = knots[degree] + t * (knots[n + 1] - knots[degree]);
            var point = EvaluateBSpline(controlPoints, degree, knots, u);
            result.Add(point);
        }

        return result;
    }

    /// <summary>
    /// Checks if a cubic Bezier segment is flat enough.
    /// </summary>
    private static bool IsFlatEnough(Vector3d p0, Vector3d p1, Vector3d p2, Vector3d p3, double tolerance)
    {
        // Check if control points are close to the line p0-p3
        var d1 = RamerDouglasPeucker.PerpendicularDistance(p1, p0, p3);
        var d2 = RamerDouglasPeucker.PerpendicularDistance(p2, p0, p3);
        return d1 <= tolerance && d2 <= tolerance;
    }

    private static Vector3d Midpoint(Vector3d a, Vector3d b) => (a + b) * 0.5;

    private static double[] CreateUniformKnots(int n, int degree)
    {
        var knotCount = n + degree + 2;
        var knots = new double[knotCount];

        for (var i = 0; i <= degree; i++)
            knots[i] = 0;

        for (var i = degree + 1; i <= n; i++)
            knots[i] = (double)(i - degree) / (n - degree + 1);

        for (var i = n + 1; i < knotCount; i++)
            knots[i] = 1;

        return knots;
    }

    private static Vector3d EvaluateBSpline(
        IReadOnlyList<Vector3d> controlPoints,
        int degree,
        double[] knots,
        double u)
    {
        var n = controlPoints.Count - 1;

        // Find knot span
        var span = degree;
        for (var i = degree; i <= n; i++)
        {
            if (u < knots[i + 1])
            {
                span = i;
                break;
            }
        }

        // Evaluate basis functions
        var basis = EvaluateBasisFunctions(span, u, degree, knots);

        // Compute point
        var point = Vector3d.Zero;
        for (var i = 0; i <= degree; i++)
        {
            var cpIndex = span - degree + i;
            if (cpIndex >= 0 && cpIndex <= n)
            {
                point = point + controlPoints[cpIndex] * basis[i];
            }
        }

        return point;
    }

    private static double[] EvaluateBasisFunctions(int span, double u, int degree, double[] knots)
    {
        var basis = new double[degree + 1];
        var left = new double[degree + 1];
        var right = new double[degree + 1];

        basis[0] = 1.0;

        for (var j = 1; j <= degree; j++)
        {
            left[j] = u - knots[span + 1 - j];
            right[j] = knots[span + j] - u;
            var saved = 0.0;

            for (var r = 0; r < j; r++)
            {
                var temp = basis[r] / (right[r + 1] + left[j - r]);
                basis[r] = saved + right[r + 1] * temp;
                saved = left[j - r] * temp;
            }

            basis[j] = saved;
        }

        return basis;
    }
}
