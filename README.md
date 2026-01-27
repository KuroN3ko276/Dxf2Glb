# DXF2GLB

A .NET tool for converting DXF files to GLB format via optimized polylines. The pipeline processes CAD geometry, simplifies curves, and exports to JSON for Blender import.

## Features

- **DXF Parsing**: Lines, Polylines (2D/3D), Splines, Arcs, Circles, Ellipses
- **Geometry Optimization**: Douglas-Peucker simplification, arc tessellation, spline sampling
- **Layer Filtering**: Process only specific layers
- **Blender Integration**: Python script for importing into Blender

## Requirements

- .NET 9.0+
- Blender 4.0+ (for GLB export)

## Installation

```bash
dotnet restore
dotnet build
```

## Usage

### Basic

```bash
DXF2GLB input.dxf
```

### With Options

```bash
DXF2GLB input.dxf -e 0.5 -o output.json
DXF2GLB input.dxf -l Layer1,Layer2 -e 0.2
```

### Options

| Option | Description | Default |
|--------|-------------|---------|
| `-e, --epsilon` | Polyline simplification tolerance | 0.1 |
| `-c, --chord-error` | Arc tessellation chord error | 0.01 |
| `-s, --spline-tolerance` | Spline sampling tolerance | 0.05 |
| `-m, --merge-distance` | Merge near points distance | 0.001 |
| `-o, --output` | Output JSON file path | `<input>.json` |
| `-l, --layers` | Only process specified layers | All |

## Pipeline

```
DXF File → DXF2GLB (C#) → JSON → Blender Script → GLB
```

1. **DXF2GLB**: Reads DXF, optimizes geometry, exports JSON
2. **Blender Script**: Imports JSON as curves, export as GLB

## Blender Import

1. Open Blender → Scripting workspace
2. Open `DXF2GLB/Scripts/blender_import.py`
3. Edit `JSON_PATH` to your output file
4. Run script (Alt+P)
5. Export as GLB: File → Export → glTF 2.0

## Output Format

```json
{
  "polylines": [
    {
      "layer": "LayerName",
      "points": [[x, y, z], ...],
      "closed": false
    }
  ],
  "stats": {
    "originalVertices": 100000,
    "optimizedVertices": 50000,
    "reductionPercent": 50.0
  }
}
```

## License

MIT
