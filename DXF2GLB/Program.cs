using netDxf;
using DXF2GLB;
using DXF2GLB.Models;
using DXF2GLB.Export;

// Parse command line arguments
if (args.Length == 0)
{
    PrintUsage();
    return;
}

var dxfPath = args[0];

if (!File.Exists(dxfPath))
{
    Console.WriteLine($"Error: File not found: {dxfPath}");
    return;
}

// Parse optional parameters
var options = new PreprocessorOptions();
var outputPath = Path.ChangeExtension(dxfPath, ".json");

for (var i = 1; i < args.Length; i++)
{
    var arg = args[i].ToLowerInvariant();
    if (i + 1 < args.Length)
    {
        switch (arg)
        {
            case "-e" or "--epsilon":
                if (double.TryParse(args[++i], out var epsilon))
                    options.PolylineEpsilon = epsilon;
                break;
            case "-c" or "--chord-error":
                if (double.TryParse(args[++i], out var chordError))
                    options.ArcChordError = chordError;
                break;
            case "-s" or "--spline-tolerance":
                if (double.TryParse(args[++i], out var splineTol))
                    options.SplineTolerance = splineTol;
                break;
            case "-m" or "--merge-distance":
                if (double.TryParse(args[++i], out var mergeDist))
                    options.MergeDistance = mergeDist;
                break;
            case "-o" or "--output":
                outputPath = args[++i];
                break;
            case "-l" or "--layers":
                options.IncludeLayers = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries);
                break;
        }
    }
    else if (arg == "-h" || arg == "--help")
    {
        PrintUsage();
        return;
    }
}

Console.WriteLine($"Loading DXF: {dxfPath}");

DxfDocument? doc;
try
{
    doc = DxfDocument.Load(dxfPath);
}
catch (Exception e)
{
    Console.WriteLine($"Error loading DXF: {e.Message}");
    return;
}

if (doc == null)
{
    Console.WriteLine("Failed to load DXF document");
    return;
}

Console.WriteLine("Processing DXF entities...");
Console.WriteLine();

// Show original entity counts
Console.WriteLine("=== ORIGINAL ENTITIES ===");
Console.WriteLine($"  Lines        : {doc.Entities.Lines.Count()}");
Console.WriteLine($"  Polyline2D   : {doc.Entities.Polylines2D.Count()}");
Console.WriteLine($"  Polyline3D   : {doc.Entities.Polylines3D.Count()}");
Console.WriteLine($"  Splines      : {doc.Entities.Splines.Count()}");
Console.WriteLine($"  Arcs         : {doc.Entities.Arcs.Count()}");
Console.WriteLine($"  Circles      : {doc.Entities.Circles.Count()}");
Console.WriteLine($"  Ellipses     : {doc.Entities.Ellipses.Count()}");
Console.WriteLine();

// Process with preprocessor
var preprocessor = new DxfPreprocessor(options);
var optimized = preprocessor.Process(doc);

// Show results
Console.WriteLine("=== PREPROCESSING OPTIONS ===");
Console.WriteLine($"  Polyline Epsilon    : {options.PolylineEpsilon}");
Console.WriteLine($"  Arc Chord Error     : {options.ArcChordError}");
Console.WriteLine($"  Spline Tolerance    : {options.SplineTolerance}");
Console.WriteLine($"  Merge Distance      : {options.MergeDistance}");
if (options.IncludeLayers != null)
    Console.WriteLine($"  Include Layers      : {string.Join(", ", options.IncludeLayers)}");
Console.WriteLine();

Console.WriteLine("=== OPTIMIZATION RESULTS ===");
Console.WriteLine($"  Original Vertices   : {optimized.Stats.OriginalVertices:N0}");
Console.WriteLine($"  Optimized Vertices  : {optimized.Stats.OptimizedVertices:N0}");
Console.WriteLine($"  Reduction           : {optimized.Stats.ReductionPercent:F1}%");
Console.WriteLine($"  Output Polylines    : {optimized.Stats.OptimizedPolylines:N0}");
Console.WriteLine();

// Export to JSON
JsonExporter.Export(optimized, outputPath);
Console.WriteLine($"Exported to: {outputPath}");

static void PrintUsage()
{
    Console.WriteLine("DXF2GLB - DXF Preprocessor for GLB Conversion");
    Console.WriteLine();
    Console.WriteLine("Usage: DXF2GLB <dxf-file> [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  -e, --epsilon <value>       Polyline simplification tolerance (default: 0.1)");
    Console.WriteLine("  -c, --chord-error <value>   Arc tessellation chord error (default: 0.01)");
    Console.WriteLine("  -s, --spline-tolerance <v>  Spline sampling tolerance (default: 0.05)");
    Console.WriteLine("  -m, --merge-distance <v>    Merge near points distance (default: 0.001)");
    Console.WriteLine("  -o, --output <path>         Output JSON file path");
    Console.WriteLine("  -l, --layers <l1,l2,...>    Only process specified layers");
    Console.WriteLine("  -h, --help                  Show this help message");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  DXF2GLB model.dxf");
    Console.WriteLine("  DXF2GLB model.dxf -e 0.5 -o output.json");
    Console.WriteLine("  DXF2GLB model.dxf -l Layer1,Layer2 -e 0.2");
}