"""
DXF2GLB - Blender Import & GLB Export Script
Imports optimized polylines from JSON, converts to mesh, optimizes, and exports to GLB.

Usage:
1. Open Blender
2. Go to Scripting workspace
3. Open this script or paste it
4. Modify the configuration variables at the top
5. Run the script (Alt+P)

Command-line usage:
blender --background --python blender_import.py -- input.json output.glb
"""

import bpy
import json
import os
import sys
from mathutils import Vector

# ============= CONFIGURATION =============
# These can be overridden by command-line arguments

JSON_PATH = r"D:\Sources\nobisoft\DXF2GLB\berm_output.json"
GLB_OUTPUT_PATH = r"D:\Sources\nobisoft\DXF2GLB\output.glb"

# Scale factor (coordinates are in meters/mm, may need scaling)
SCALE = 1.0

# Maximum polylines to import (set to None for all)
MAX_POLYLINES = None  # e.g., 1000 for testing

# Create separate collections per layer
ORGANIZE_BY_LAYER = True

# Merge all polylines in a layer into one curve object
MERGE_PER_LAYER = True

# Curve settings (before conversion to mesh)
CURVE_RESOLUTION = 12
CURVE_BEVEL_DEPTH = 0.5  # Set to 0 for wire-like curves

# ============= MESH OPTIMIZATION SETTINGS =============

# Convert curves to mesh before optimization
CONVERT_TO_MESH = True

# Merge vertices within this distance (in Blender units)
MERGE_DISTANCE = 0.001

# Limit dissolve angle threshold (radians) - removes unnecessary vertices
# Higher = more aggressive (0.0872 ≈ 5°, 0.1745 ≈ 10°)
LIMIT_DISSOLVE_ANGLE = 0.0872  # ~5 degrees

# Decimate ratio (0.0 to 1.0) - percentage of faces to keep
# Set to 1.0 to disable decimation
DECIMATE_RATIO = 0.5

# Decimate type: 'COLLAPSE', 'UNSUBDIV', or 'DISSOLVE'
DECIMATE_TYPE = 'COLLAPSE'

# Export to GLB after processing
EXPORT_GLB = True


# ============= MAIN SCRIPT =============

def clear_scene():
    """Remove all objects from the scene."""
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    
    # Also clear orphan data
    for block in bpy.data.meshes:
        if block.users == 0:
            bpy.data.meshes.remove(block)
    for block in bpy.data.curves:
        if block.users == 0:
            bpy.data.curves.remove(block)


def get_or_create_collection(name):
    """Get or create a collection with the given name."""
    if name in bpy.data.collections:
        return bpy.data.collections[name]
    
    collection = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    return collection


def calculate_center(polylines):
    """Calculate the center of all polylines for auto-centering."""
    min_x = min_y = min_z = float('inf')
    max_x = max_y = max_z = float('-inf')
    
    count = 0
    for pl in polylines:
        for point in pl.get('points', []):
            x, y, z = point
            min_x = min(min_x, x)
            min_y = min(min_y, y)
            min_z = min(min_z, z)
            max_x = max(max_x, x)
            max_y = max(max_y, y)
            max_z = max(max_z, z)
            count += 1
            if count > 100000:  # Sample first 100k points for speed
                break
        if count > 100000:
            break
    
    center_x = (min_x + max_x) / 2
    center_y = (min_y + max_y) / 2
    center_z = (min_z + max_z) / 2
    
    return center_x, center_y, center_z


def create_merged_curve_from_polylines(name, polylines_data, center, collection=None):
    """Create a single curve with multiple splines from a list of polylines."""
    center_x, center_y, center_z = center
    
    # Create curve data
    curve_data = bpy.data.curves.new(name=name, type='CURVE')
    curve_data.dimensions = '3D'
    curve_data.resolution_u = CURVE_RESOLUTION
    
    if CURVE_BEVEL_DEPTH > 0:
        curve_data.bevel_depth = CURVE_BEVEL_DEPTH
        curve_data.bevel_resolution = 2
    
    spline_count = 0
    for pl in polylines_data:
        points = pl.get('points', [])
        closed = pl.get('closed', False)
        
        if len(points) < 2:
            continue
        
        # Center the points
        centered_points = [
            (p[0] - center_x, p[1] - center_y, p[2] - center_z)
            for p in points
        ]
        
        # Create spline
        spline = curve_data.splines.new('POLY')
        spline.points.add(len(centered_points) - 1)  # Already has 1 point
        
        for i, point in enumerate(centered_points):
            x, y, z = point
            spline.points[i].co = (x * SCALE, y * SCALE, z * SCALE, 1)
        
        if closed:
            spline.use_cyclic_u = True
        
        spline_count += 1
    
    # Create object
    curve_obj = bpy.data.objects.new(name, curve_data)
    
    # Link to collection
    if collection:
        collection.objects.link(curve_obj)
    else:
        bpy.context.scene.collection.objects.link(curve_obj)
    
    return curve_obj, spline_count


def convert_curve_to_mesh(curve_obj):
    """Convert a curve object to a mesh object."""
    # Make sure it's selected and active
    bpy.ops.object.select_all(action='DESELECT')
    curve_obj.select_set(True)
    bpy.context.view_layer.objects.active = curve_obj
    
    # Convert to mesh
    bpy.ops.object.convert(target='MESH')
    
    return bpy.context.active_object


def merge_vertices(mesh_obj, distance):
    """Merge vertices within a distance."""
    bpy.ops.object.select_all(action='DESELECT')
    mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = mesh_obj
    
    # Enter edit mode
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    
    # Merge by distance
    bpy.ops.mesh.remove_doubles(threshold=distance)
    
    # Return to object mode
    bpy.ops.object.mode_set(mode='OBJECT')
    
    return mesh_obj


def apply_limit_dissolve(mesh_obj, angle_limit):
    """Apply limited dissolve to remove collinear vertices."""
    bpy.ops.object.select_all(action='DESELECT')
    mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = mesh_obj
    
    # Enter edit mode
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    
    # Limited dissolve
    bpy.ops.mesh.dissolve_limited(angle_limit=angle_limit)
    
    # Return to object mode
    bpy.ops.object.mode_set(mode='OBJECT')
    
    return mesh_obj


def apply_decimate(mesh_obj, ratio, decimate_type='COLLAPSE'):
    """Apply decimate modifier to reduce polygon count."""
    if ratio >= 1.0:
        return mesh_obj  # No decimation needed
    
    # Add decimate modifier
    modifier = mesh_obj.modifiers.new(name="Decimate", type='DECIMATE')
    modifier.decimate_type = decimate_type
    
    if decimate_type == 'COLLAPSE':
        modifier.ratio = ratio
    elif decimate_type == 'DISSOLVE':
        modifier.angle_limit = 0.0872  # ~5 degrees
    
    # Apply modifier
    bpy.ops.object.select_all(action='DESELECT')
    mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = mesh_obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    
    return mesh_obj


def get_vertex_count():
    """Get total vertex count of all mesh objects in scene."""
    total = 0
    for obj in bpy.data.objects:
        if obj.type == 'MESH':
            total += len(obj.data.vertices)
    return total


def export_glb(filepath):
    """Export the scene to GLB format."""
    print(f"Exporting to GLB: {filepath}")
    
    # Select all mesh objects
    bpy.ops.object.select_all(action='DESELECT')
    for obj in bpy.data.objects:
        if obj.type == 'MESH':
            obj.select_set(True)
    
    # Export settings for optimized GLB (Blender 5.0+ compatible)
    bpy.ops.export_scene.gltf(
        filepath=filepath,
        export_format='GLB',
        use_selection=True,
        export_apply=True,
        export_animations=False,
        export_materials='NONE',  # No materials for geometry-only export
    )
    
    print(f"GLB exported successfully: {filepath}")
    file_size = os.path.getsize(filepath)
    print(f"File size: {file_size / 1024 / 1024:.2f} MB")


def import_and_process(json_path, glb_output_path=None):
    """Main function: Import JSON, optimize, and export to GLB."""
    print("=" * 60)
    print("DXF2GLB - Blender Import & Export Pipeline")
    print("=" * 60)
    
    print(f"\nLoading JSON from: {json_path}")
    
    with open(json_path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    polylines = data.get('polylines', [])
    stats = data.get('stats', {})
    
    print(f"Loaded {len(polylines)} polylines")
    print(f"Original stats: {stats}")
    
    if not polylines:
        print("ERROR: No polylines found in JSON!")
        return
    
    # Calculate center for large coordinates
    print("\nCalculating center for auto-centering...")
    center_x, center_y, center_z = calculate_center(polylines)
    center = (center_x, center_y, center_z)
    print(f"Center: ({center_x:.2f}, {center_y:.2f}, {center_z:.2f})")
    
    # Limit polylines if needed
    if MAX_POLYLINES:
        polylines = polylines[:MAX_POLYLINES]
        print(f"Limiting to {MAX_POLYLINES} polylines for testing")
    
    # Group by layer
    layers = {}
    for pl in polylines:
        layer = pl.get('layer', 'Default')
        if layer not in layers:
            layers[layer] = []
        layers[layer].append(pl)
    
    print(f"\nFound {len(layers)} layers")
    print(f"Merge mode: {'ON - 1 object per layer' if MERGE_PER_LAYER else 'OFF - 1 object per polyline'}")
    
    # Create curves
    print("\n--- Creating Curves ---")
    curve_objects = []
    total_splines = 0
    
    for layer_name, layer_polylines in layers.items():
        print(f"Processing layer '{layer_name}' ({len(layer_polylines)} polylines)")
        
        # Get or create collection
        if ORGANIZE_BY_LAYER:
            collection = get_or_create_collection(layer_name)
        else:
            collection = None
        
        if MERGE_PER_LAYER:
            # Create one merged curve per layer
            curve_obj, spline_count = create_merged_curve_from_polylines(
                layer_name, layer_polylines, center, collection
            )
            curve_objects.append(curve_obj)
            total_splines += spline_count
            print(f"  Created merged curve with {spline_count} splines")
    
    print(f"\nCreated {len(curve_objects)} curve objects with {total_splines} total splines")
    
    # Convert to mesh and optimize
    if CONVERT_TO_MESH:
        print("\n--- Converting to Mesh ---")
        mesh_objects = []
        
        for curve_obj in curve_objects:
            mesh_obj = convert_curve_to_mesh(curve_obj)
            mesh_objects.append(mesh_obj)
        
        vertex_count_before = get_vertex_count()
        print(f"Mesh vertices before optimization: {vertex_count_before:,}")
        
        # Merge vertices
        if MERGE_DISTANCE > 0:
            print(f"\n--- Merging Vertices (distance: {MERGE_DISTANCE}) ---")
            for mesh_obj in mesh_objects:
                merge_vertices(mesh_obj, MERGE_DISTANCE)
            print(f"Vertices after merge: {get_vertex_count():,}")
        
        # Apply limit dissolve
        if LIMIT_DISSOLVE_ANGLE > 0:
            print(f"\n--- Applying Limit Dissolve (angle: {LIMIT_DISSOLVE_ANGLE:.4f} rad) ---")
            for mesh_obj in mesh_objects:
                apply_limit_dissolve(mesh_obj, LIMIT_DISSOLVE_ANGLE)
            print(f"Vertices after dissolve: {get_vertex_count():,}")
        
        # Apply decimate
        if DECIMATE_RATIO < 1.0:
            print(f"\n--- Applying Decimate (ratio: {DECIMATE_RATIO}, type: {DECIMATE_TYPE}) ---")
            for mesh_obj in mesh_objects:
                apply_decimate(mesh_obj, DECIMATE_RATIO, DECIMATE_TYPE)
            print(f"Vertices after decimate: {get_vertex_count():,}")
        
        vertex_count_after = get_vertex_count()
        reduction = (1 - vertex_count_after / vertex_count_before) * 100 if vertex_count_before > 0 else 0
        print(f"\n--- Optimization Summary ---")
        print(f"Before: {vertex_count_before:,} vertices")
        print(f"After:  {vertex_count_after:,} vertices")
        print(f"Reduction: {reduction:.2f}%")
    
    # Export to GLB
    if EXPORT_GLB and glb_output_path:
        print(f"\n--- Exporting GLB ---")
        export_glb(glb_output_path)
    
    # Zoom to fit all
    for area in bpy.context.screen.areas:
        if area.type == 'VIEW_3D':
            for region in area.regions:
                if region.type == 'WINDOW':
                    with bpy.context.temp_override(area=area, region=region):
                        bpy.ops.view3d.view_all()
                    break
    
    print("\n" + "=" * 60)
    print("Pipeline complete!")
    print("=" * 60)


def parse_command_line_args():
    """Parse command line arguments when running from blender --background."""
    global JSON_PATH, GLB_OUTPUT_PATH
    
    # Get arguments after '--'
    if '--' in sys.argv:
        argv = sys.argv[sys.argv.index('--') + 1:]
        if len(argv) >= 1:
            JSON_PATH = argv[0]
        if len(argv) >= 2:
            GLB_OUTPUT_PATH = argv[1]
        return True
    return False


# Run the import
if __name__ == "__main__":
    # Check for command line arguments
    is_batch = parse_command_line_args()
    
    if is_batch:
        # Running in batch mode - clear scene first
        clear_scene()
    
    if os.path.exists(JSON_PATH):
        import_and_process(JSON_PATH, GLB_OUTPUT_PATH if EXPORT_GLB else None)
    else:
        print(f"ERROR: JSON file not found: {JSON_PATH}")
