# DXF2GLB - DXF Preprocessing Pipeline

Công cụ tiền xử lí file DXF để tối ưu số vertex trước khi chuyển đổi sang GLB qua Blender.

## Installation

```powershell
cd d:\Sources\nobisoft\DXF2GLB
dotnet build
```

## Usage

```powershell
dotnet run --project DXF2GLB -- <dxf-file> [options]
```

### Options

| Option | Description | Default |
|--------|-------------|---------|
| `-e, --epsilon <value>` | Polyline simplification tolerance | 0.1 |
| `-c, --chord-error <value>` | Arc tessellation chord error | 0.01 |
| `-s, --spline-tolerance <value>` | Spline sampling tolerance | 0.05 |
| `-m, --merge-distance <value>` | Merge near points distance | 0.001 |
| `-o, --output <path>` | Output JSON file path | `<input>.json` |
| `-l, --layers <l1,l2,...>` | Only process specific layers | all |

---

## Thông số tiền xử lí chi tiết

### 1. Epsilon (Polyline Simplification) `-e`

**Thuật toán:** Ramer-Douglas-Peucker (RDP)

```
         *  <- Point bị loại bỏ nếu khoảng cách < epsilon
        /
       /
*-----*-----* <- Đường nối start-end
```

**Ý nghĩa:** Khoảng cách vuông góc tối đa cho phép từ một điểm đến đường thẳng nối 2 điểm đầu-cuối.
- **Giá trị nhỏ** (0.1): Giữ nhiều chi tiết, ít giảm vertex
- **Giá trị lớn** (10, 50, 100): Loại bỏ nhiều điểm, đường cong trở nên thẳng hơn

**Khuyến nghị theo đơn vị DXF:**
| Đơn vị | Epsilon khuyến nghị | Ghi chú |
|--------|---------------------|---------|
| mm | 0.1 - 1.0 | File CAD chi tiết |
| cm | 1.0 - 10.0 | File thiết kế |
| m (UTM) | 10 - 100 | Dữ liệu địa hình |

**Ví dụ:**
```powershell
# Dữ liệu UTM, loại bỏ điểm trong vòng 20m
dotnet run --project DXF2GLB -- topo.dxf -e 20

# File CAD mm, giữ chi tiết cao
dotnet run --project DXF2GLB -- part.dxf -e 0.5
```

---

### 2. Chord Error (Arc Tessellation) `-c`

**Áp dụng cho:** Arc, Circle, Ellipse

```
    Arc gốc
   .--------.
  /    ^     \
 /     |chord \
/      |error  \
*------+-------*  <- Polyline xấp xỉ
```

**Ý nghĩa:** Khoảng cách tối đa cho phép giữa arc thực và polyline xấp xỉ.
- **Giá trị nhỏ** (0.001): Nhiều segment, arc mịn
- **Giá trị lớn** (1.0): Ít segment, arc thô

**Công thức số segment:**
```
segments = ceil(sweepAngle / (2 * arccos(1 - chordError/radius)))
```

**Khuyến nghị:**
| Chord Error | Kết quả |
|-------------|---------|
| 0.001 | Rất mịn (nhiều vertex) |
| 0.01 | Mịn (mặc định) |
| 0.1 | Vừa |
| 1.0 | Thô (ít vertex) |

---

### 3. Spline Tolerance `-s`

**Áp dụng cho:** Spline, Bezier curves

**Thuật toán:** Adaptive subdivision dựa trên flatness

```
    Spline gốc (cong)
   ~~~~~~~~~
  /         \
 /           \
*-------------*  <- Kiểm tra flatness
```

**Ý nghĩa:** Ngưỡng "phẳng đủ" - nếu control points nằm trong tolerance từ đường thẳng, không cần chia nhỏ thêm.

**Khuyến nghị:**
- Spline chi tiết: 0.01 - 0.05
- Spline đơn giản: 0.1 - 0.5

---

### 4. Merge Distance `-m`

**Ý nghĩa:** Khoảng cách để gộp các điểm trùng/gần nhau.

```
*--*  <- 2 điểm cách nhau < merge distance
   ↓
   *  <- Gộp thành 1 điểm
```

**Khuyến nghị:** Thường giữ nhỏ (0.001) để không làm mất chi tiết.

---

## Các hàm xử lí chính

### `DxfPreprocessor.Process()`
Orchestrator chính, xử lí tất cả entity types:
- Lines → Copy trực tiếp
- Polylines2D/3D → RDP simplification
- Arcs → Tessellation
- Circles → Tessellation (closed)
- Ellipses → Tessellation (closed)
- Splines → Adaptive sampling + RDP

### `RamerDouglasPeucker.Simplify()`
```csharp
List<Vector3d> Simplify(IReadOnlyList<Vector3d> points, double epsilon)
```
Giảm số điểm trong polyline giữ nguyên hình dạng tổng thể.

### `ArcTessellator.TessellateArc()`
```csharp
List<Vector3d> TessellateArc(center, radius, startAngle, endAngle, normal, chordError, minSegments, maxSegments)
```
Chuyển arc thành polyline với số segment tối ưu.

### `SplineSampler.SampleBSpline()`
```csharp
List<Vector3d> SampleBSpline(controlPoints, degree, sampleCount)
```
Sample B-spline thành danh sách điểm.

---

## Output JSON Format

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
    "original_vertices": 4161526,
    "optimized_vertices": 2345678,
    "reduction_percent": 43.6,
    "original_entities": 19566,
    "optimized_polylines": 19566,
    "entity_counts": {
      "Lines": 0,
      "Polylines2D": 19566,
      ...
    }
  }
}
```

---

## Blender Import

Sử dụng script `Scripts/blender_import.py` để visualize trong Blender.

```python
# Cấu hình trong script
JSON_PATH = r"path/to/output.json"
MAX_POLYLINES = 1000  # Test trước với subset
MERGE_PER_LAYER = True  # Gộp thành 1 object/layer
```
