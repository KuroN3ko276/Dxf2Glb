# DXF2GLB - DXF to GLB Converter

Công cụ chuyển đổi file DXF sang GLB trực tiếp bằng C#.

## Tính năng

- **Dual Library Support:** Tự động chọn thư viện phù hợp dựa trên nội dung file
  - `IxMilia.Dxf` cho AC1009/R12 (phiên bản cũ)
  - `netDxf` cho file có PolyfaceMesh (mesh với face data)
- **Direct GLB Export:** Xuất trực tiếp sang GLB sử dụng SharpGLTF (không cần Blender)
- **Tối ưu hóa:** Ramer-Douglas-Peucker simplification, Arc/Spline tessellation, Ear Clipping Triangulation

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
# Export GLB với cài đặt mặc định (Triangulated Mesh)
dotnet run --project DXF2GLB -- model.dxf -g

# Export với tối ưu hóa (epsilon càng lớn = file nhỏ hơn)
dotnet run --project DXF2GLB -- model.dxf -g -e 10

# Export dạng wireframe (chỉ đường, không có faces)
dotnet run --project DXF2GLB -- model.dxf -g -w

# Chỉ định output path
dotnet run --project DXF2GLB -- model.dxf -o output.glb
```

## Tham số

| Option | Description | Default |
|--------|-------------|---------|
| `-g, --glb` | Export trực tiếp sang GLB | false |
| `-w, --wireframe` | Export dạng wireframe (LINES) | false |
| `-e, --epsilon <value>` | Polyline simplification tolerance | 0.1 |
| `-c, --chord-error <value>` | Arc tessellation chord error | 0.01 |
| `-s, --spline-tolerance <value>` | Spline sampling tolerance | 0.05 |
| `-m, --merge-distance <value>` | Merge near points distance | 0.001 |
| `-o, --output <path>` | Output file path | `<input>.glb` |
| `-l, --layers <l1,l2,...>` | Chỉ xử lý các layer cụ thể | all |

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
    │  DxfPreprocessor │  ← Tối ưu hóa geometry
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
