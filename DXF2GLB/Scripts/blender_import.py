"""
DXF2GLB - Blender Import Script
Imports optimized polylines from JSON and creates curve objects.

Usage:
1. Open Blender
2. Go to Scripting workspace
3. Open this script or paste it
4. Modify the JSON_PATH variable at the top
5. Run the script (Alt+P)
"""

import bpy
import json
import os
from mathutils import Vector

# ============= CONFIGURATION =============
JSON_PATH = r"D:\Sources\nobisoft\DXF2GLB\output_e10.json"

# Scale factor (coordinates are in meters/mm, may need scaling)
# For large coordinates like UTM (466xxx, 7102xxx), we'll auto-center
SCALE = 1.0

# Maximum polylines to import (set to None for all)
# Use this to test with a subset first
MAX_POLYLINES = None  # e.g., 1000 for testing

# Create separate collections per layer
ORGANIZE_BY_LAYER = True

# Merge all polylines in a layer into one curve object
# This significantly improves performance (1 object per layer instead of 1 per polyline)
MERGE_PER_LAYER = True

# Curve settings
CURVE_RESOLUTION = 12
CURVE_BEVEL_DEPTH = 0.5  # Set to 0 for wire-like display


# ============= MAIN SCRIPT =============

def clear_scene():
    """Remove all mesh objects from the scene."""
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)


def get_or_create_collection(name):
    """Get or create a collection with the given name."""
    if name in bpy.data.collections:
        return bpy.data.collections[name]
    
    collection = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    return collection


def create_curve_from_points(name, points, closed=False, collection=None):
    """Create a Blender curve from a list of points."""
    # Create curve data
    curve_data = bpy.data.curves.new(name=name, type='CURVE')
    curve_data.dimensions = '3D'
    curve_data.resolution_u = CURVE_RESOLUTION
    
    if CURVE_BEVEL_DEPTH > 0:
        curve_data.bevel_depth = CURVE_BEVEL_DEPTH
        curve_data.bevel_resolution = 2
    
    # Create spline
    spline = curve_data.splines.new('POLY')
    spline.points.add(len(points) - 1)  # Already has 1 point
    
    for i, point in enumerate(points):
        x, y, z = point
        spline.points[i].co = (x * SCALE, y * SCALE, z * SCALE, 1)
    
    if closed:
        spline.use_cyclic_u = True
    
    # Create object
    curve_obj = bpy.data.objects.new(name, curve_data)
    
    # Link to collection
    if collection:
        collection.objects.link(curve_obj)
    else:
        bpy.context.scene.collection.objects.link(curve_obj)
    
    return curve_obj


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


def import_json(filepath):
    """Import polylines from JSON file."""
    print(f"Loading JSON from: {filepath}")
    
    with open(filepath, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    polylines = data.get('polylines', [])
    stats = data.get('stats', {})
    
    print(f"Loaded {len(polylines)} polylines")
    print(f"Stats: {stats}")
    
    if not polylines:
        print("No polylines found in JSON!")
        return
    
    # Calculate center for large coordinates
    print("Calculating center for auto-centering...")
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
    
    print(f"Found {len(layers)} layers")
    print(f"Merge mode: {'ON - 1 object per layer' if MERGE_PER_LAYER else 'OFF - 1 object per polyline'}")
    
    # Create curves
    total_objects = 0
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
            total_objects += 1
            total_splines += spline_count
            print(f"  Created merged curve with {spline_count} splines")
        else:
            # Create separate curves for each polyline (original behavior)
            for i, pl in enumerate(layer_polylines):
                points = pl.get('points', [])
                closed = pl.get('closed', False)
                
                if len(points) < 2:
                    continue
                
                # Center the points
                centered_points = [
                    [p[0] - center_x, p[1] - center_y, p[2] - center_z]
                    for p in points
                ]
                
                curve_name = f"{layer_name}_{i:06d}"
                create_curve_from_points(curve_name, centered_points, closed, collection)
                total_objects += 1
                total_splines += 1
                
                if total_objects % 1000 == 0:
                    print(f"  Created {total_objects} curves...")
    
    print(f"Done! Created {total_objects} curve objects with {total_splines} total splines")
    
    # Zoom to fit all (Blender 4.0+ API)
    for area in bpy.context.screen.areas:
        if area.type == 'VIEW_3D':
            for region in area.regions:
                if region.type == 'WINDOW':
                    with bpy.context.temp_override(area=area, region=region):
                        bpy.ops.view3d.view_all()
                    break


# Run the import
if __name__ == "__main__":
    # Clear existing objects (optional - comment out to keep existing)
    # clear_scene()
    
    if os.path.exists(JSON_PATH):
        import_json(JSON_PATH)
    else:
        print(f"ERROR: JSON file not found: {JSON_PATH}")
