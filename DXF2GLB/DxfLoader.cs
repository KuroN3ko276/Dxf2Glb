using DXF2GLB.Models;
using IxMilia.Dxf;
using NetDxfDoc = netDxf.DxfDocument;

namespace DXF2GLB;

/// <summary>
/// Result of loading a DXF file with the appropriate library
/// </summary>
public class DxfLoadResult
{
    public DxfFile? IxMiliaFile { get; set; }
    public NetDxfDoc? NetDxfDocument { get; set; }
    public string Version { get; set; } = "";
    public string LibraryUsed { get; set; } = "";
    public bool HasPolyfaceMeshes { get; set; }
    public int PolyfaceMeshCount { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Smart DXF loader that uses the appropriate library based on file content:
/// - IxMilia.Dxf for AC1009/R12 files (not supported by netDxf)
/// - netDxf for files with PolyfaceMeshes (better mesh support)
/// - IxMilia.Dxf as fallback for other files
/// </summary>
public static class DxfLoader
{
    /// <summary>
    /// Load a DXF file using the most appropriate library.
    /// </summary>
    public static DxfLoadResult Load(string filePath)
    {
        var result = new DxfLoadResult();
        
        // First, try IxMilia.Dxf to detect version
        try
        {
            using var stream = File.OpenRead(filePath);
            var ixFile = DxfFile.Load(stream);
            result.Version = ixFile.Header.Version.ToString();
            
            // Check if this is an old version that netDxf doesn't support
            if (IsOldVersion(ixFile.Header.Version))
            {
                result.IxMiliaFile = ixFile;
                result.LibraryUsed = "IxMilia.Dxf";
                result.HasPolyfaceMeshes = false;
                Console.WriteLine($"  Using IxMilia.Dxf (AC1009/R12 support)");
                return result;
            }
        }
        catch (Exception ex)
        {
            result.Error = $"IxMilia.Dxf failed: {ex.Message}";
        }
        
        // For AC2000+, try netDxf to check for PolyfaceMeshes
        try
        {
            var netDoc = NetDxfDoc.Load(filePath);
            var polyfaceCount = netDoc.Entities.PolyfaceMeshes.Count();
            
            if (polyfaceCount > 0)
            {
                result.NetDxfDocument = netDoc;
                result.LibraryUsed = "netDxf";
                result.HasPolyfaceMeshes = true;
                result.PolyfaceMeshCount = polyfaceCount;
                Console.WriteLine($"  Using netDxf ({polyfaceCount} PolyfaceMeshes detected)");
                return result;
            }
        }
        catch (Exception ex)
        {
            // netDxf failed, fall back to IxMilia
            Console.WriteLine($"  netDxf failed: {ex.Message}, using IxMilia.Dxf");
        }
        
        // Fallback: use IxMilia.Dxf
        try
        {
            using var stream = File.OpenRead(filePath);
            result.IxMiliaFile = DxfFile.Load(stream);
            result.LibraryUsed = "IxMilia.Dxf";
            result.HasPolyfaceMeshes = false;
            Console.WriteLine($"  Using IxMilia.Dxf (default)");
        }
        catch (Exception ex)
        {
            result.Error = $"Both libraries failed. Last error: {ex.Message}";
        }
        
        return result;
    }

    private static bool IsOldVersion(DxfAcadVersion version)
    {
        return version switch
        {
            DxfAcadVersion.R9 => true,
            DxfAcadVersion.R10 => true,
            DxfAcadVersion.R11 => true,
            DxfAcadVersion.R12 => true,
            DxfAcadVersion.R13 => true,
            DxfAcadVersion.R14 => true,
            _ => false
        };
    }
}
