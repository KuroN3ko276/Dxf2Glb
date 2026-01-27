using netDxf;
using netDxf.Entities;
using DXF2GLB.Algorithms;
using DXF2GLB.Models;

namespace DXF2GLB;

/// <summary>
/// Main preprocessing orchestrator for DXF files.
/// Converts DXF entities to optimized polylines for GLB export.
/// </summary>
public class DxfPreprocessor
{
    private readonly PreprocessorOptions _options;

    public DxfPreprocessor(PreprocessorOptions? options = null)
    {
        _options = options ?? new PreprocessorOptions();
    }

    /// <summary>
    /// Processes a DXF document and returns optimized geometry.
    /// </summary>
    public OptimizedGeometry Process(DxfDocument doc)
    {
        var result = new OptimizedGeometry();
        var originalVertexCount = 0;

        // Count entities for stats
        result.Stats.EntityCounts["Lines"] = doc.Entities.Lines.Count();
        result.Stats.EntityCounts["Polylines2D"] = doc.Entities.Polylines2D.Count();
        result.Stats.EntityCounts["Polylines3D"] = doc.Entities.Polylines3D.Count();
        result.Stats.EntityCounts["Arcs"] = doc.Entities.Arcs.Count();
        result.Stats.EntityCounts["Circles"] = doc.Entities.Circles.Count();
        result.Stats.EntityCounts["Ellipses"] = doc.Entities.Ellipses.Count();
        result.Stats.EntityCounts["Splines"] = doc.Entities.Splines.Count();

        // Process Lines
        foreach (var line in doc.Entities.Lines)
        {
            if (!ShouldProcess(line)) continue;
            originalVertexCount += 2;
            result.Polylines.Add(new OptimizedPolyline(
                line.Layer.Name,
                new List<Vector3d>
                {
                    ToVector3d(line.StartPoint),
                    ToVector3d(line.EndPoint)
                },
                false
            ));
        }

        // Process Polylines2D
        foreach (var pl in doc.Entities.Polylines2D)
        {
            if (!ShouldProcess(pl)) continue;
            var points = pl.Vertexes.Select(v => ToVector3d(v.Position)).ToList();
            originalVertexCount += points.Count;
            var simplified = RamerDouglasPeucker.Simplify(points, _options.PolylineEpsilon);
            result.Polylines.Add(new OptimizedPolyline(pl.Layer.Name, simplified, pl.IsClosed));
        }

        // Process Polylines3D
        foreach (var pl in doc.Entities.Polylines3D)
        {
            if (!ShouldProcess(pl)) continue;
            var points = pl.Vertexes.Select(v => ToVector3d(v)).ToList();
            originalVertexCount += points.Count;
            var simplified = RamerDouglasPeucker.Simplify(points, _options.PolylineEpsilon);
            result.Polylines.Add(new OptimizedPolyline(pl.Layer.Name, simplified, pl.IsClosed));
        }

        // Process Arcs
        foreach (var arc in doc.Entities.Arcs)
        {
            if (!ShouldProcess(arc)) continue;
            var center = ToVector3d(arc.Center);
            var normal = ToVector3d(arc.Normal);
            var startAngle = arc.StartAngle * Math.PI / 180.0;
            var endAngle = arc.EndAngle * Math.PI / 180.0;

            var points = ArcTessellator.TessellateArc(
                center, arc.Radius, startAngle, endAngle, normal,
                _options.ArcChordError, _options.MinArcSegments, _options.MaxArcSegments);

            originalVertexCount += (int)Math.Ceiling((endAngle - startAngle) / (Math.PI / 18)); // Estimate
            result.Polylines.Add(new OptimizedPolyline(arc.Layer.Name, points, false));
        }

        // Process Circles
        foreach (var circle in doc.Entities.Circles)
        {
            if (!ShouldProcess(circle)) continue;
            var center = ToVector3d(circle.Center);
            var normal = ToVector3d(circle.Normal);

            var points = ArcTessellator.TessellateCircle(
                center, circle.Radius, normal,
                _options.ArcChordError, _options.MinArcSegments, _options.MaxArcSegments);

            originalVertexCount += 36; // Estimate typical circle tessellation
            result.Polylines.Add(new OptimizedPolyline(circle.Layer.Name, points, true));
        }

        // Process Ellipses
        foreach (var ellipse in doc.Entities.Ellipses)
        {
            if (!ShouldProcess(ellipse)) continue;
            var center = ToVector3d(ellipse.Center);
            var normal = ToVector3d(ellipse.Normal);
            // MajorAxis and MinorAxis are the radii (double values)
            var majorRadius = ellipse.MajorAxis;
            var minorRadius = ellipse.MinorAxis;
            // Rotation angle in radians (convert from degrees if needed)
            var rotation = ellipse.Rotation * Math.PI / 180.0;

            var points = ArcTessellator.TessellateEllipse(
                center, majorRadius, minorRadius, rotation, normal,
                _options.ArcChordError, _options.MinArcSegments * 2, _options.MaxArcSegments * 2);

            originalVertexCount += 72; // Estimate
            result.Polylines.Add(new OptimizedPolyline(ellipse.Layer.Name, points, true));
        }

        // Process Splines
        foreach (var spline in doc.Entities.Splines)
        {
            if (!ShouldProcess(spline)) continue;
            var controlPoints = spline.ControlPoints.Select(cp => ToVector3d(cp)).ToList();
            originalVertexCount += controlPoints.Count * 10; // Estimate typical sampling

            List<Vector3d> sampled;
            if (spline.Degree == 3 && controlPoints.Count == 4)
            {
                // Cubic Bezier
                sampled = SplineSampler.SampleCubicBezier(
                    controlPoints[0], controlPoints[1],
                    controlPoints[2], controlPoints[3],
                    _options.SplineTolerance);
            }
            else
            {
                // General B-spline
                var sampleCount = Math.Max(20, controlPoints.Count * 5);
                sampled = SplineSampler.SampleBSpline(controlPoints, spline.Degree, sampleCount);
            }

            // Apply RDP simplification after sampling
            var simplified = RamerDouglasPeucker.Simplify(sampled, _options.PolylineEpsilon);
            result.Polylines.Add(new OptimizedPolyline(spline.Layer.Name, simplified, spline.IsClosed));
        }

        // Merge near points if enabled
        if (_options.MergeDistance > 0)
        {
            result.Polylines = MergeNearPoints(result.Polylines, _options.MergeDistance);
        }

        // Calculate stats
        result.Stats.OriginalVertices = originalVertexCount;
        result.Stats.OptimizedVertices = result.Polylines.Sum(p => p.Points.Count);
        result.Stats.OriginalEntities = result.Stats.EntityCounts.Values.Sum();
        result.Stats.OptimizedPolylines = result.Polylines.Count;

        return result;
    }

    private bool ShouldProcess(EntityObject entity)
    {
        if (_options.IncludeLayers == null || _options.IncludeLayers.Length == 0)
            return true;

        return _options.IncludeLayers.Contains(entity.Layer.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static Vector3d ToVector3d(netDxf.Vector3 v) => new(v.X, v.Y, v.Z);
    private static Vector3d ToVector3d(netDxf.Vector2 v) => new(v.X, v.Y, 0);

    /// <summary>
    /// Merges points that are very close together within each polyline.
    /// </summary>
    private static List<OptimizedPolyline> MergeNearPoints(List<OptimizedPolyline> polylines, double mergeDistance)
    {
        var result = new List<OptimizedPolyline>(polylines.Count);
        var mergeDistanceSquared = mergeDistance * mergeDistance;

        foreach (var pl in polylines)
        {
            if (pl.Points.Count < 2)
            {
                result.Add(pl);
                continue;
            }

            var merged = new List<Vector3d> { pl.Points[0] };
            for (var i = 1; i < pl.Points.Count; i++)
            {
                if (pl.Points[i].DistanceSquaredTo(merged[^1]) > mergeDistanceSquared)
                {
                    merged.Add(pl.Points[i]);
                }
            }

            // Keep at least 2 points for a line
            if (merged.Count < 2 && pl.Points.Count >= 2)
            {
                merged = new List<Vector3d> { pl.Points[0], pl.Points[^1] };
            }

            result.Add(new OptimizedPolyline(pl.Layer, merged, pl.IsClosed));
        }

        return result;
    }
}
