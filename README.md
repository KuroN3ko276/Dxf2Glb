# DXF2GLB - DXF to GLB Converter

Công cụ chuyển đổi file DXF sang GLB trực tiếp bằng C#.

## Tính năng

- **Dual Library Support:** Tự động chọn thư viện phù hợp dựa trên nội dung file
  - `IxMilia.Dxf` cho AC1009/R12 (phiên bản cũ)
  - `netDxf` cho file có PolyfaceMesh (mesh với face data)
- **Direct GLB Export:** Xuất trực tiếp sang GLB sử dụng SharpGLTF (không cần Blender)
- **Thông minh (Smart Triangulation):** Tự động xử lý:
  - Closed Polylines → Triangles (mặt kín)
  - Open Polylines → Lines (đường nét)
  - PolyfaceMesh → Native Triangles
- **Siêu Tối ưu hóa (Mesh Optimization):** 
  - **Vertex Clustering:** Giảm 90%+ số lượng triangles cực nhanh (O(n))
  - **Junk Filtering:** Tự động loại bỏ rác, outliers và các island nhỏ
- **Tối ưu hóa đường (Line Optimization):** Ramer-Douglas-Peucker simplification, Arc/Spline tessellation

## Yêu cầu

- **.NET 9.0 SDK**
- Hỗ trợ **tất cả các version DXF** từ R12 trở lên

## Cài đặt

```powershell
cd d:\Sources\nobisoft\DXF2GLB
dotnet build
```

## Sử dụng

### Export GLB trực tiếp (Default)

```powershell
# Export GLB với cài đặt mặc định
dotnet run --project DXF2GLB -- model.dxf -g

# Export với tối ưu hóa file KHỔNG LỒ (Vertex Clustering)
# -d 256: Grid resolution 256x256x256 (giảm ~90% triangles)
# -j: Bật Junk Filter (xóa rác)
dotnet run --project DXF2GLB -- model.dxf -g -d 256 -j

# Export dạng wireframe (chỉ khung dây)
dotnet run --project DXF2GLB -- model.dxf -g -w

# Chỉ định output path
dotnet run --project DXF2GLB -- model.dxf -o output.glb
```

## Tham số

| Option | Description | Default |
|--------|-------------|---------|
| `-g, --glb` | Export trực tiếp sang GLB | false |
| `-w, --wireframe` | Export dạng wireframe (LINES) | false |
| `-o, --output <path>` | Output file path | `<input>.glb` |
| `-d, --decimate <res>` | **Vertex Clustering Grid (32-1024)**. VD: 256 | 0 (disabled) |
| `-j, --junk-filter` | Bật bộ lọc rác (Bounding Box + Island) | false |
| `--min-component <n>` | Số triangles tối thiểu cho island | 100 |
| `-e, --epsilon <value>` | Polyline simplification tolerance | 0.1 |
| `-c, --chord-error <value>` | Arc tessellation chord error | 0.01 |
| `-l, --layers <list>` | Chỉ xử lý các layer cụ thể | all |

## Khuyến nghị Epsilon theo đơn vị

| Đơn vị DXF | Epsilon | Ghi chú |
|------------|---------|---------|
| mm | 0.1 - 1.0 | File CAD chi tiết |
| cm | 1.0 - 10.0 | File thiết kế |
| m (UTM) | 10 - 100 | Dữ liệu địa hình |

## Architecture

```
DXF File
    │
    ▼
┌─────────────┐
│  DxfLoader  │  ← Chọn library phù hợp
└─────────────┘
    │
    ├── R12/AC1009 ───────► IxMilia.Dxf
    │
    └── PolyfaceMesh ─────► netDxf
           │
           ▼
    ┌──────────────────┐
    │  DxfPreprocessor │  ← Smart Triangulation
    └──────────────────┘
           │
           ▼
    ┌──────────────────┐
    │ Mesh Optimizer   │  ← Vertex Clustering (-d)
    │                  │  ← Junk Filtering (-j)
    └──────────────────┘
           │
           ▼
    ┌─────────────┐
    │ GlbExporter │  ← SharpGLTF (Native C#)
    └─────────────┘
           │
           ▼
       GLB File
```

## Dependencies

- [IxMilia.Dxf](https://github.com/ixmilia/dxf) - DXF parsing (hỗ trợ AC1009/R12)
- [netDxf](https://github.com/haplokuon/netDxf) - DXF parsing (hỗ trợ PolyfaceMesh)
- [SharpGLTF](https://github.com/vpenades/SharpGLTF) - GLB/glTF export
