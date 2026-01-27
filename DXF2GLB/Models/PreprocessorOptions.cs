namespace DXF2GLB.Models;

/// <summary>
/// Configuration options for DXF preprocessing
/// </summary>
public class PreprocessorOptions
{
    /// <summary>
    /// Tolerance for Ramer-Douglas-Peucker polyline simplification (in DXF units)
    /// Higher values = fewer points but less accuracy
    /// </summary>
    public double PolylineEpsilon { get; set; } = 0.1;

    /// <summary>
    /// Maximum chord error for arc/circle tessellation (in DXF units)
    /// Smaller values = more segments but smoother curves
    /// </summary>
    public double ArcChordError { get; set; } = 0.01;

    /// <summary>
    /// Tolerance for spline to polyline conversion (in DXF units)
    /// </summary>
    public double SplineTolerance { get; set; } = 0.05;

    /// <summary>
    /// Distance threshold for merging duplicate/near points (in DXF units)
    /// </summary>
    public double MergeDistance { get; set; } = 0.001;

    /// <summary>
    /// If specified, only process entities on these layers. Null = all layers.
    /// </summary>
    public string[]? IncludeLayers { get; set; }

    /// <summary>
    /// Minimum number of segments for arc tessellation (to avoid flat arcs)
    /// </summary>
    public int MinArcSegments { get; set; } = 8;

    /// <summary>
    /// Maximum number of segments for arc tessellation (to cap very large arcs)
    /// </summary>
    public int MaxArcSegments { get; set; } = 128;
}
