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
        
        // First, peek at the version without loading the whole file
        string? versionString = null;
        try
        {
            versionString = PeekDxfVersion(filePath);
            result.Version = versionString;
            Console.WriteLine($"  Detected Version: {versionString}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Warning: Could not peek version: {ex.Message}");
            // Continue - we'll try loading with libraries anyway
        }

        // If we detected an old version, use IxMilia directly
        if (versionString != null && IsOldVersionString(versionString))
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                var ixFile = DxfFile.Load(stream);
                result.IxMiliaFile = ixFile;
                result.LibraryUsed = "IxMilia.Dxf";
                result.HasPolyfaceMeshes = false;
                Console.WriteLine($"  Using IxMilia.Dxf (Legacy version support)");
                return result;
            }
            catch (Exception ex)
            {
                result.Error = $"IxMilia.Dxf failed: {ex.Message}";
                return result;
            }
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

    /// <summary>
    /// Check if the DXF version string indicates a legacy version (AC1009/R12 and earlier)
    /// </summary>
    private static bool IsOldVersionString(string versionStr)
    {
        // AC1006 = R10
        // AC1009 = R11/R12
        // AC1012 = R13
        // AC1014 = R14
        // AC1015 = R2000 (first version netDxf supports well)
        
        if (versionStr.StartsWith("AC100")) return true; // R12 and older
        if (versionStr == "AC1012" || versionStr == "AC1014") return true; // R13/R14
        
        return false;
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

    /// <summary>
    /// Lightweight version detection - reads only the header section (first ~1000 lines) to find $ACADVER.
    /// This avoids loading the entire file just to check the version.
    /// </summary>
    private static string PeekDxfVersion(string filePath)
    {
        using var reader = new StreamReader(File.OpenRead(filePath));
        string? line;
        bool foundVarMarker = false;
        int linesRead = 0;
        const int MaxLines = 2000; // Header is typically in the first few hundred lines

        while ((line = reader.ReadLine()) != null && linesRead < MaxLines)
        {
            line = line.Trim();
            linesRead++;

            // Look for the $ACADVER variable
            if (line == "$ACADVER")
            {
                foundVarMarker = true;
                continue;
            }

            // After finding $ACADVER, the next non-group-code line should be the version
            if (foundVarMarker)
            {
                // Skip group codes (numbers like "1", "9", etc.)
                if (int.TryParse(line, out _))
                {
                    continue;
                }

                // This should be the AC version string (e.g., "AC1015")
                if (line.StartsWith("AC"))
                {
                    return line;
                }
            }
        }

        // Default to AC1015 (R2000) if we can't find it - most common modern version
        return "AC1015";
    }
}
