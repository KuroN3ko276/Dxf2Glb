using DXF2GLB.Models;

namespace DXF2GLB.Algorithms;

/// <summary>
/// Converts arcs and circles to polylines with optimal segment count.
/// Uses chord error tolerance to determine number of segments.
/// </summary>
public static class ArcTessellator
{
    /// <summary>
    /// Tessellates an arc into a polyline.
    /// </summary>
    /// <param name="center">Arc center point</param>
    /// <param name="radius">Arc radius</param>
    /// <param name="startAngle">Start angle in radians</param>
    /// <param name="endAngle">End angle in radians</param>
    /// <param name="normal">Normal vector (for 3D arcs)</param>
    /// <param name="chordError">Maximum chord error tolerance</param>
    /// <param name="minSegments">Minimum number of segments</param>
    /// <param name="maxSegments">Maximum number of segments</param>
    /// <returns>List of points forming the tessellated arc</returns>
    public static List<Vector3d> TessellateArc(
        Vector3d center,
        double radius,
        double startAngle,
        double endAngle,
        Vector3d normal,
        double chordError,
        int minSegments = 8,
        int maxSegments = 128)
    {
        // Calculate sweep angle
        var sweepAngle = endAngle - startAngle;
        if (sweepAngle < 0) sweepAngle += 2 * Math.PI;

        // Calculate optimal segment count based on chord error
        var segments = CalculateSegmentCount(radius, sweepAngle, chordError, minSegments, maxSegments);

        // Generate points
        var points = new List<Vector3d>(segments + 1);
        var angleStep = sweepAngle / segments;

        // Calculate basis vectors for the arc plane
        var (uAxis, vAxis) = GetArcBasis(normal);

        for (var i = 0; i <= segments; i++)
        {
            var angle = startAngle + i * angleStep;
            var x = radius * Math.Cos(angle);
            var y = radius * Math.Sin(angle);

            var point = center + uAxis * x + vAxis * y;
            points.Add(point);
        }

        return points;
    }

    /// <summary>
    /// Tessellates a full circle into a polyline.
    /// </summary>
    public static List<Vector3d> TessellateCircle(
        Vector3d center,
        double radius,
        Vector3d normal,
        double chordError,
        int minSegments = 8,
        int maxSegments = 128)
    {
        return TessellateArc(center, radius, 0, 2 * Math.PI, normal, chordError, minSegments, maxSegments);
    }

    /// <summary>
    /// Tessellates an ellipse into a polyline.
    /// </summary>
    public static List<Vector3d> TessellateEllipse(
        Vector3d center,
        double majorRadius,
        double minorRadius,
        double rotation,
        Vector3d normal,
        double chordError,
        int minSegments = 16,
        int maxSegments = 256)
    {
        // Use the larger radius for segment calculation
        var maxRadius = Math.Max(majorRadius, minorRadius);
        var segments = CalculateSegmentCount(maxRadius, 2 * Math.PI, chordError, minSegments, maxSegments);

        var points = new List<Vector3d>(segments + 1);
        var angleStep = 2 * Math.PI / segments;

        var (uAxis, vAxis) = GetArcBasis(normal);

        // Apply rotation to basis vectors
        var cosRot = Math.Cos(rotation);
        var sinRot = Math.Sin(rotation);
        var rotatedU = uAxis * cosRot - vAxis * sinRot;
        var rotatedV = uAxis * sinRot + vAxis * cosRot;

        for (var i = 0; i <= segments; i++)
        {
            var angle = i * angleStep;
            var x = majorRadius * Math.Cos(angle);
            var y = minorRadius * Math.Sin(angle);

            var point = center + rotatedU * x + rotatedV * y;
            points.Add(point);
        }

        return points;
    }

    /// <summary>
    /// Calculates optimal segment count based on chord error tolerance.
    /// Formula: segments = ceil(sweepAngle / (2 * arccos(1 - chordError/radius)))
    /// </summary>
    private static int CalculateSegmentCount(
        double radius,
        double sweepAngle,
        double chordError,
        int minSegments,
        int maxSegments)
    {
        if (radius <= 0 || chordError <= 0)
            return minSegments;

        // Prevent division by zero and invalid arccos
        var ratio = 1.0 - chordError / radius;
        if (ratio < -1) ratio = -1;
        if (ratio > 1) ratio = 1;

        var maxAnglePerSegment = 2 * Math.Acos(ratio);
        if (maxAnglePerSegment <= 0)
            return maxSegments;

        var segments = (int)Math.Ceiling(sweepAngle / maxAnglePerSegment);
        return Math.Clamp(segments, minSegments, maxSegments);
    }

    /// <summary>
    /// Gets orthonormal basis vectors for the arc plane given a normal.
    /// </summary>
    private static (Vector3d uAxis, Vector3d vAxis) GetArcBasis(Vector3d normal)
    {
        // Find a vector not parallel to normal
        var reference = Math.Abs(normal.Z) < 0.9
            ? new Vector3d(0, 0, 1)
            : new Vector3d(1, 0, 0);

        var uAxis = Vector3d.Cross(normal, reference).Normalized();
        var vAxis = Vector3d.Cross(normal, uAxis).Normalized();

        return (uAxis, vAxis);
    }
}
