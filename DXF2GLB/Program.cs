using DXF2GLB;
using DXF2GLB.Export;
using DXF2GLB.Models;

// Check for input file
if (args.Length == 0)
{
    PrintUsage();
    return;
}

var dxfPath = args[0];
if (!File.Exists(dxfPath))
{
    Console.WriteLine($"File not found: {dxfPath}");
    return;
}

// Parse optional parameters
var options = new PreprocessorOptions();
string? outputPath = null;
var exportGlb = false;
var wireframe = false;

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
            default:
                // Check for flags without arguments
                if (arg == "-g" || arg == "--glb")
                    exportGlb = true;
                else if (arg == "-w" || arg == "--wireframe")
                    wireframe = true;
                break;
        }
    }
    else
    {
        // Handle flags (no value required)
        switch (arg)
        {
            case "-g" or "--glb":
                exportGlb = true;
                break;
            case "-w" or "--wireframe":
                wireframe = true;
                break;
            case "-h" or "--help":
                PrintUsage();
                return;
        }
    }
}

// Determine output path and format
if (outputPath == null)
{
    outputPath = Path.ChangeExtension(dxfPath, exportGlb ? ".glb" : ".json");
}
else if (exportGlb && !outputPath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
{
    // If -g flag is set but output doesn't end with .glb, force GLB
    outputPath = Path.ChangeExtension(outputPath, ".glb");
}
else if (outputPath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
{
    // If output ends with .glb, enable GLB export
    exportGlb = true;
}

Console.WriteLine($"Loading DXF: {dxfPath}");

// Use smart loader to pick the right library
var loadResult = DxfLoader.Load(dxfPath);

if (loadResult.Error != null)
{
    Console.WriteLine($"Error loading DXF: {loadResult.Error}");
    return;
}

Console.WriteLine($"DXF Version: {loadResult.Version}");
Console.WriteLine($"Library: {loadResult.LibraryUsed}");
if (loadResult.HasPolyfaceMeshes)
{
    Console.WriteLine($"PolyfaceMeshes: {loadResult.PolyfaceMeshCount}");
}
Console.WriteLine("Processing DXF entities...");
Console.WriteLine();

// Process with preprocessor
var preprocessor = new DxfPreprocessor(options);
var optimized = preprocessor.Process(loadResult);

// Show results
Console.WriteLine();
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
if (optimized.Stats.MeshCount > 0)
{
    Console.WriteLine($"  Output Meshes       : {optimized.Stats.MeshCount:N0}");
    Console.WriteLine($"  Total Triangles     : {optimized.Stats.TotalTriangles:N0}");
}
Console.WriteLine();

// Export
if (exportGlb)
{
    Console.WriteLine("=== EXPORTING GLB ===");
    GlbExporter.Export(optimized, outputPath, wireframe);
    var fileSize = new FileInfo(outputPath).Length;
    Console.WriteLine($"Exported to: {outputPath}");
    Console.WriteLine($"File size: {fileSize / 1024.0 / 1024.0:F2} MB");
}
else
{
    JsonExporter.Export(optimized, outputPath);
    Console.WriteLine($"Exported to: {outputPath}");
}

static void PrintUsage()
{
    Console.WriteLine("DXF2GLB - DXF to GLB Converter (Dual Library Support)");
    Console.WriteLine();
    Console.WriteLine("Usage: DXF2GLB <dxf-file> [options]");
    Console.WriteLine();
    Console.WriteLine("Libraries:");
    Console.WriteLine("  - IxMilia.Dxf  : Used for AC1009/R12 files");
    Console.WriteLine("  - netDxf       : Used for files with PolyfaceMesh data");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  -e, --epsilon <value>       Polyline simplification tolerance (default: 0.1)");
    Console.WriteLine("  -c, --chord-error <value>   Arc tessellation chord error (default: 0.01)");
    Console.WriteLine("  -s, --spline-tolerance <v>  Spline sampling tolerance (default: 0.05)");
    Console.WriteLine("  -m, --merge-distance <v>    Merge near points distance (default: 0.001)");
    Console.WriteLine("  -o, --output <path>         Output file path (.json or .glb)");
    Console.WriteLine("  -g, --glb                   Export directly to GLB format");
    Console.WriteLine("  -w, --wireframe             Export as wireframe (lines only, no faces)");
    Console.WriteLine("  -l, --layers <l1,l2,...>    Only process specified layers");
    Console.WriteLine("  -h, --help                  Show this help message");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  DXF2GLB model.dxf                      # Export to JSON");
    Console.WriteLine("  DXF2GLB model.dxf -g                   # Export to GLB");
    Console.WriteLine("  DXF2GLB model.dxf -e 10 -o model.glb   # Export to GLB with epsilon=10");
    Console.WriteLine("  DXF2GLB model.dxf -g -w                # Export GLB as wireframe");
}