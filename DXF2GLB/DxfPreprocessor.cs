using IxMilia.Dxf;
using IxMilia.Dxf.Entities;
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
    /// Processes a DXF file and returns optimized geometry.
    /// </summary>
    public OptimizedGeometry Process(DxfFile file)
    {
        var result = new OptimizedGeometry();
        var originalVertexCount = 0;

        // Categorize entities
        var lines = file.Entities.OfType<DxfLine>().ToList();
        var lwPolylines = file.Entities.OfType<DxfLwPolyline>().ToList();
        var polylines = file.Entities.OfType<DxfPolyline>().ToList();
        var arcs = file.Entities.OfType<DxfArc>().ToList();
        var circles = file.Entities.OfType<DxfCircle>().ToList();
        var ellipses = file.Entities.OfType<DxfEllipse>().ToList();
        var splines = file.Entities.OfType<DxfSpline>().ToList();
        var faces3d = file.Entities.OfType<Dxf3DFace>().ToList();

        // Count entities for stats
        result.Stats.EntityCounts["Lines"] = lines.Count;
        result.Stats.EntityCounts["LwPolylines"] = lwPolylines.Count;
        result.Stats.EntityCounts["Polylines"] = polylines.Count;
        result.Stats.EntityCounts["Arcs"] = arcs.Count;
        result.Stats.EntityCounts["Circles"] = circles.Count;
        result.Stats.EntityCounts["Ellipses"] = ellipses.Count;
        result.Stats.EntityCounts["Splines"] = splines.Count;
        result.Stats.EntityCounts["Faces3D"] = faces3d.Count;

        // Process Lines
        foreach (var line in lines)
        {
            if (!ShouldProcess(line)) continue;
            originalVertexCount += 2;
            result.Polylines.Add(new OptimizedPolyline(
                line.Layer ?? "0",
                new List<Vector3d>
                {
                    ToVector3d(line.P1),
                    ToVector3d(line.P2)
                },
                false
            ));
        }

        // Process LwPolylines (2D lightweight polylines)
        foreach (var pl in lwPolylines)
        {
            if (!ShouldProcess(pl)) continue;
            var points = pl.Vertices.Select(v => new Vector3d(v.X, v.Y, pl.Elevation)).ToList();
            originalVertexCount += points.Count;
            
            List<Vector3d> simplified;
            if (points.Count > 500_000)
            {
                Console.WriteLine($"  Large LwPolyline detected: {points.Count:N0} points");
                simplified = RamerDouglasPeucker.SimplifyLarge(points, _options.PolylineEpsilon,
                    progressCallback: (done, total) => Console.Write($"\r    Processing: {done:N0}/{total:N0} points..."));
                Console.WriteLine($"\r    Simplified to {simplified.Count:N0} points                    ");
            }
            else
            {
                simplified = RamerDouglasPeucker.Simplify(points, _options.PolylineEpsilon);
            }
            
            result.Polylines.Add(new OptimizedPolyline(pl.Layer ?? "0", simplified, pl.IsClosed));
        }

        // Process Polylines (3D polylines)
        foreach (var pl in polylines)
        {
            if (!ShouldProcess(pl)) continue;
            var points = pl.Vertices.Select(v => ToVector3d(v.Location)).ToList();
            originalVertexCount += points.Count;
            
            List<Vector3d> simplified;
            if (points.Count > 500_000)
            {
                Console.WriteLine($"  Large Polyline detected: {points.Count:N0} points");
                simplified = RamerDouglasPeucker.SimplifyLarge(points, _options.PolylineEpsilon,
                    progressCallback: (done, total) => Console.Write($"\r    Processing: {done:N0}/{total:N0} points..."));
                Console.WriteLine($"\r    Simplified to {simplified.Count:N0} points                    ");
            }
            else
            {
                simplified = RamerDouglasPeucker.Simplify(points, _options.PolylineEpsilon);
            }
            
            result.Polylines.Add(new OptimizedPolyline(pl.Layer ?? "0", simplified, pl.IsClosed));
        }

        // Process Arcs
        foreach (var arc in arcs)
        {
            if (!ShouldProcess(arc)) continue;
            var center = ToVector3d(arc.Center);
            var normal = ToVector3d(arc.Normal);
            // IxMilia.Dxf uses degrees for arc angles
            var startAngle = arc.StartAngle * Math.PI / 180.0;
            var endAngle = arc.EndAngle * Math.PI / 180.0;

            var points = ArcTessellator.TessellateArc(
                center, arc.Radius, startAngle, endAngle, normal,
                _options.ArcChordError, _options.MinArcSegments, _options.MaxArcSegments);

            originalVertexCount += (int)Math.Ceiling((endAngle - startAngle) / (Math.PI / 18));
            result.Polylines.Add(new OptimizedPolyline(arc.Layer ?? "0", points, false));
        }

        // Process Circles
        foreach (var circle in circles)
        {
            if (!ShouldProcess(circle)) continue;
            var center = ToVector3d(circle.Center);
            var normal = ToVector3d(circle.Normal);

            var points = ArcTessellator.TessellateCircle(
                center, circle.Radius, normal,
                _options.ArcChordError, _options.MinArcSegments, _options.MaxArcSegments);

            originalVertexCount += 36;
            result.Polylines.Add(new OptimizedPolyline(circle.Layer ?? "0", points, true));
        }

        // Process Ellipses
        foreach (var ellipse in ellipses)
        {
            if (!ShouldProcess(ellipse)) continue;
            var center = ToVector3d(ellipse.Center);
            var normal = ToVector3d(ellipse.Normal);
            // IxMilia: MajorAxis is a DxfVector, get its length for the major radius
            var majorRadius = Math.Sqrt(
                ellipse.MajorAxis.X * ellipse.MajorAxis.X + 
                ellipse.MajorAxis.Y * ellipse.MajorAxis.Y + 
                ellipse.MajorAxis.Z * ellipse.MajorAxis.Z);
            var minorRadius = majorRadius * ellipse.MinorAxisRatio;
            // Calculate rotation from MajorAxis direction
            var rotation = Math.Atan2(ellipse.MajorAxis.Y, ellipse.MajorAxis.X);

            var points = ArcTessellator.TessellateEllipse(
                center, majorRadius, minorRadius, rotation, normal,
                _options.ArcChordError, _options.MinArcSegments * 2, _options.MaxArcSegments * 2);

            originalVertexCount += 72;
            result.Polylines.Add(new OptimizedPolyline(ellipse.Layer ?? "0", points, true));
        }

        // Process Splines
        foreach (var spline in splines)
        {
            if (!ShouldProcess(spline)) continue;
            var controlPoints = spline.ControlPoints.Select(cp => ToVector3d(cp.Point)).ToList();
            if (controlPoints.Count < 2) continue;
            
            originalVertexCount += controlPoints.Count * 10;

            List<Vector3d> sampled;
            if (spline.DegreeOfCurve == 3 && controlPoints.Count == 4)
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
                sampled = SplineSampler.SampleBSpline(controlPoints, spline.DegreeOfCurve, sampleCount);
            }

            // Apply RDP simplification after sampling
            var simplified = RamerDouglasPeucker.Simplify(sampled, _options.PolylineEpsilon);
            result.Polylines.Add(new OptimizedPolyline(spline.Layer ?? "0", simplified, spline.IsClosed));
        }

        // Process 3D Faces
        var faceVertices = new List<Vector3d>();
        foreach (var face in faces3d)
        {
            if (!ShouldProcess(face)) continue;
            faceVertices.Add(ToVector3d(face.FirstCorner));
            faceVertices.Add(ToVector3d(face.SecondCorner));
            faceVertices.Add(ToVector3d(face.ThirdCorner));
            if (face.FourthCorner != face.ThirdCorner)
                faceVertices.Add(ToVector3d(face.FourthCorner));
        }
        
        if (faceVertices.Count > 0)
        {
            Console.WriteLine($"  3DFace total: {faceVertices.Count:N0} vertices from {faces3d.Count} faces");
            originalVertexCount += faceVertices.Count;
            
            var uniqueVertices = faceVertices.Distinct().ToList();
            Console.WriteLine($"    Unique vertices: {uniqueVertices.Count:N0}");
            
            List<Vector3d> simplified;
            if (uniqueVertices.Count > 500_000)
            {
                simplified = RamerDouglasPeucker.SimplifyLarge(uniqueVertices, _options.PolylineEpsilon,
                    progressCallback: (done, total) => Console.Write($"\r    Processing: {done:N0}/{total:N0} vertices..."));
                Console.WriteLine($"\r    Simplified to {simplified.Count:N0} vertices                    ");
            }
            else
            {
                simplified = RamerDouglasPeucker.Simplify(uniqueVertices, _options.PolylineEpsilon);
            }
            
            result.Polylines.Add(new OptimizedPolyline("3DFace", simplified, false));
        }

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

    private bool ShouldProcess(DxfEntity entity)
    {
        if (_options.IncludeLayers == null || _options.IncludeLayers.Length == 0)
            return true;

        var layerName = entity.Layer ?? "0";
        return _options.IncludeLayers.Contains(layerName, StringComparer.OrdinalIgnoreCase);
    }

    private static Vector3d ToVector3d(DxfPoint p) => new(p.X, p.Y, p.Z);
    private static Vector3d ToVector3d(DxfVector v) => new(v.X, v.Y, v.Z);

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
