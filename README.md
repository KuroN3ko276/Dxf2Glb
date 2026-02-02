# DXF2GLB - DXF Preprocessing Pipeline

Công cụ xử lý file DXF thành GLB thông qua 2 bước:
1. **Preprocessor (C#):** Tối ưu hóa geometry, giảm số lượng vertex, xuất ra JSON.
2. **Blender Importer (Python):** Nhập JSON vào Blender, tạo object, và xuất ra GLB.

## Yêu cầu (Requirements)
- **.NET 8.0 SDK** (để chạy Preprocessor)
- **Blender 3.6+** (đã thêm vào PATH để chạy command `blender`)
- **File DXF:** Phải là version **AutoCAD 2000 (AC1015)** trở lên.
  > [!WARNING]
  > Không hỗ trợ AutoCAD R12 (AC1009). Nếu gặp lỗi "DXF file version not supported", hãy mở file bằng AutoCAD/OdaViewer và lưu lại với version mới hơn (khuyên dùng 2010/2013/2018).

## Cài đặt

```powershell
cd d:\Sources\nobisoft\DXF2GLB
dotnet build
```

## Quy trình sử dụng (Workflow)

### Bước 1: Tiền xử lý (Convert DXF -> JSON)

Sử dụng tool C# để đọc DXF, tối ưu hóa đường nét và xuất ra file trung gian JSON.

```powershell
# Chạy với tham số mặc định
dotnet run --project DXF2GLB -- input.dxf

# Hoặc chỉ định output và tham số tối ưu
# -o: Output path
# -e: Epsilon (độ đơn giản hóa, càng lớn càng ít điểm)
# -l: Chỉ xử lý các layer cụ thể (cách nhau bởi dấu phẩy)
dotnet run --project DXF2GLB -- input.dxf -o data.json -e 0.5 -l "Layer1,Layer2"
```

### Bước 2: Blender Import (Convert JSON -> GLB)

Sử dụng script Python với Blender để convert JSON thành GLB. Script nằm tại `DXF2GLB\Scripts\blender_import.py`.

```powershell
# Cú pháp chạy background (không mở giao diện Blender)
blender --background --python DXF2GLB\Scripts\blender_import.py -- <input_json> <output_glb>

# Ví dụ thực tế:
blender --background --python DXF2GLB\Scripts\blender_import.py -- data.json model.glb
```

---

## Chi tiết tham số Preprocessor (C#)

### Options

| Option | Description | Default |
|--------|-------------|---------|
| `-e, --epsilon <value>` | Polyline simplification tolerance | 0.1 |
| `-c, --chord-error <value>` | Arc tessellation chord error | 0.01 |
| `-s, --spline-tolerance <value>` | Spline sampling tolerance | 0.05 |
| `-m, --merge-distance <value>` | Merge near points distance | 0.001 |
| `-o, --output <path>` | Output JSON file path | `<input>.json` |
| `-l, --layers <l1,l2,...>` | Only process specific layers | all |

### 1. Epsilon (Polyline Simplification) `-e`

**Thuật toán:** Ramer-Douglas-Peucker (RDP) - Giảm số điểm trên đường cong/gấp khúc.

- **Giá trị nhỏ** (0.1): Giữ nhiều chi tiết (tốt cho kiến trúc, cơ khí).
- **Giá trị lớn** (10 - 100): Giữ dáng chính, loại bỏ chi tiết nhỏ (tốt cho bản đồ địa hình lớn).

**Khuyến nghị theo đơn vị DXF:**
| Đơn vị | Epsilon khuyến nghị | Ghi chú |
|--------|---------------------|---------|
| mm | 0.1 - 1.0 | File CAD chi tiết |
| cm | 1.0 - 10.0 | File thiết kế |
| m (UTM) | 10 - 100 | Dữ liệu địa hình |

### 2. Chord Error (Arc Tessellation) `-c`

**Áp dụng cho:** Arc, Circle.

- **Giá trị nhỏ** (0.001): Đường tròn rất tròn (nhiều vertex).
- **Giá trị lớn** (0.1 - 1.0): Đường tròn bị gãy khúc (ít vertex).

### 3. Merge Distance `-m`

Khoảng cách để gộp các điểm trùng nhau hoặc quá gần nhau. Giữ mặc định `0.001` là tốt nhất cho hầu hết trường hợp.

---

## Cấu hình Blender Importer

Script `DXF2GLB\Scripts\blender_import.py` có các thông số cấu hình ở phần đầu file mà bạn có thể chỉnh sửa trực tiếp:

| Biến | Ý nghĩa | Default |
|------|---------|---------|
| `SCALE` | Tỉ lệ scale toàn bộ model (1.0 = giữ nguyên) | 1.0 |
| `MERGE_PER_LAYER` | Gộp tất cả polylines cùng layer thành 1 object | True |
| `CURVE_BEVEL_DEPTH` | Độ dày của đường (0 = wireframe) | 0.5 |
| `CONVERT_TO_MESH` | Chuyển curve thành mesh để tối ưu tiếp | True |
| `DECIMATE_RATIO` | Tỉ lệ giảm lưới sau khi import (0.0 - 1.0) | 0.5 |
