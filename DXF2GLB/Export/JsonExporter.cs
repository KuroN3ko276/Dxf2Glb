using System.Text.Json;
using System.Text.Json.Serialization;
using DXF2GLB.Models;

namespace DXF2GLB.Export;

/// <summary>
/// Exports optimized geometry to JSON format for Blender consumption.
/// </summary>
public static class JsonExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Exports optimized geometry to a JSON file.
    /// </summary>
    public static void Export(OptimizedGeometry geometry, string outputPath)
    {
        var exportData = ConvertToExportFormat(geometry);
        var json = JsonSerializer.Serialize(exportData, JsonOptions);
        File.WriteAllText(outputPath, json);
    }

    /// <summary>
    /// Exports optimized geometry to a JSON string.
    /// </summary>
    public static string ExportToString(OptimizedGeometry geometry)
    {
        var exportData = ConvertToExportFormat(geometry);
        return JsonSerializer.Serialize(exportData, JsonOptions);
    }

    private static ExportData ConvertToExportFormat(OptimizedGeometry geometry)
    {
        return new ExportData
        {
            Polylines = geometry.Polylines.Select(p => new ExportPolyline
            {
                Layer = p.Layer,
                Points = p.Points.Select(v => new[] { v.X, v.Y, v.Z }).ToList(),
                Closed = p.IsClosed
            }).ToList(),
            Stats = new ExportStats
            {
                OriginalVertices = geometry.Stats.OriginalVertices,
                OptimizedVertices = geometry.Stats.OptimizedVertices,
                ReductionPercent = Math.Round(geometry.Stats.ReductionPercent, 2),
                OriginalEntities = geometry.Stats.OriginalEntities,
                OptimizedPolylines = geometry.Stats.OptimizedPolylines,
                EntityCounts = geometry.Stats.EntityCounts
            }
        };
    }
}

// Export DTOs
internal class ExportData
{
    public List<ExportPolyline> Polylines { get; set; } = new();
    public ExportStats Stats { get; set; } = new();
}

internal class ExportPolyline
{
    public string Layer { get; set; } = "";
    public List<double[]> Points { get; set; } = new();
    public bool Closed { get; set; }
}

internal class ExportStats
{
    public int OriginalVertices { get; set; }
    public int OptimizedVertices { get; set; }
    public double ReductionPercent { get; set; }
    public int OriginalEntities { get; set; }
    public int OptimizedPolylines { get; set; }
    public Dictionary<string, int> EntityCounts { get; set; } = new();
}
