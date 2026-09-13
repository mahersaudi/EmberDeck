"""
Renders enemy sprites from procedural geometry.

    blender --background --python art/render_enemies.py

Art as a script rather than as binary files. Every sprite is reproducible from this file,
diffs are readable, and a palette change is one edit and one command instead of reopening
and re-exporting a dozen source files. The same reason the card content is generated rather
than committed.

Output: art/out/<id>.png, transparent, orthographic front view.
"""

import math
import os
import sys

import bpy
from mathutils import Vector

OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")
RESOLUTION = 512

# Matches Palette.cs. Two places hold these colours, so they are stated in both and checked
# by eye; if a third appears, they belong in one data file instead.
ENEMIES = [
    {
        "id": "emberling",
        "colour": (0.85, 0.58, 0.25),
        "glow": 2.5,
        "parts": [
            # (kind, location, scale)
            ("sphere", (0.0, 0.0, 0.0), (1.0, 0.85, 1.0)),
            ("cone",   (0.0, 0.0, 1.15), (0.45, 0.45, 0.8)),
            ("cone",   (-0.75, 0.0, 0.75), (0.3, 0.3, 0.6)),
            ("cone",   (0.75, 0.0, 0.75), (0.3, 0.3, 0.6)),
        ],
        "eyes": [(-0.32, -0.75, 0.2), (0.32, -0.75, 0.2)],
        "eye_scale": 0.14,
    },
    {
        "id": "cinder_rat",
        "colour": (0.70, 0.33, 0.26),
        "glow": 0.8,
        "parts": [
            ("sphere", (0.0, 0.0, -0.1), (1.25, 0.9, 0.75)),
            ("sphere", (0.0, -0.95, 0.25), (0.62, 0.62, 0.55)),
            ("cone",   (-0.42, -0.9, 0.8), (0.28, 0.28, 0.42)),
            ("cone",   (0.42, -0.9, 0.8), (0.28, 0.28, 0.42)),
            ("sphere", (0.0, 1.15, -0.2), (0.7, 0.35, 0.18)),
        ],
        "eyes": [(-0.24, -1.42, 0.32), (0.24, -1.42, 0.32)],
        "eye_scale": 0.1,
    },
    {
        "id": "ash_hound",
        "colour": (0.45, 0.30, 0.42),
        "glow": 1.2,
        "parts": [
            ("sphere", (0.0, 0.0, 0.15), (1.5, 1.0, 0.85)),
            ("sphere", (0.0, -1.2, 0.45), (0.75, 0.7, 0.65)),
            ("cone",   (-0.5, -1.15, 1.15), (0.3, 0.3, 0.55)),
            ("cone",   (0.5, -1.15, 1.15), (0.3, 0.3, 0.55)),
            ("cylinder", (-0.95, -0.75, -0.95), (0.2, 0.2, 0.8)),
            ("cylinder", (0.95, -0.75, -0.95), (0.2, 0.2, 0.8)),
            ("cylinder", (-1.05, 0.75, -0.95), (0.2, 0.2, 0.8)),
            ("cylinder", (1.05, 0.75, -0.95), (0.2, 0.2, 0.8)),
        ],
        "eyes": [(-0.3, -1.75, 0.6), (0.3, -1.75, 0.6)],
        "eye_scale": 0.12,
    },
]


def pick_engine():
    """Engine identifiers move between Blender versions; ask this build what it has."""
    items = [i.identifier for i in
             bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    for preferred in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "CYCLES"):
        if preferred in items:
            return preferred
    return items[0]


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (bpy.data.meshes, bpy.data.materials, bpy.data.lights, bpy.data.cameras):
        for block in list(collection):
            if block.users == 0:
                collection.remove(block)


def make_material(name, colour, emission_strength):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    if bsdf is None:
        return material

    bsdf.inputs["Base Color"].default_value = (*colour, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.65

    # A little self-emission keeps the silhouette readable against the very dark board.
    # Pure diffuse shapes disappear into a 0.07-value background.
    if "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value = (*colour, 1.0)
    if "Emission Strength" in bsdf.inputs:
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    return material


def add_part(kind, location, scale, material):
    if kind == "sphere":
        bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, location=location)
    elif kind == "cone":
        bpy.ops.mesh.primitive_cone_add(vertices=24, location=location)
    elif kind == "cylinder":
        bpy.ops.mesh.primitive_cylinder_add(vertices=20, location=location)
    else:
        raise ValueError(f"unknown part kind: {kind}")

    obj = bpy.context.active_object
    obj.scale = scale
    bpy.ops.object.shade_smooth()
    obj.data.materials.append(material)
    return obj


def build_enemy(spec):
    body_material = make_material(f"mat_{spec['id']}", spec["colour"], spec["glow"] * 0.05)
    eye_material = make_material(f"mat_{spec['id']}_eye", (1.0, 0.92, 0.62), 4.0)

    parts = [add_part(kind, loc, scale, body_material) for kind, loc, scale in spec["parts"]]

    for i, eye_location in enumerate(spec.get("eyes", [])):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, location=eye_location)
        eye = bpy.context.active_object
        eye.scale = (spec["eye_scale"],) * 3
        eye.data.materials.append(eye_material)
        parts.append(eye)

    return parts


def setup_camera_and_lights():
    bpy.ops.object.camera_add(location=(0.0, -8.0, 2.6),
                              rotation=(math.radians(76), 0.0, 0.0))
    camera = bpy.context.active_object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 5.2
    bpy.context.scene.camera = camera

    # Key from the front-left, rim from behind. The rim is what separates the silhouette
    # from the background; without it every enemy reads as a flat blob.
    bpy.ops.object.light_add(type="AREA", location=(-4.0, -5.0, 4.0))
    key = bpy.context.active_object
    key.data.energy = 420
    key.data.size = 6
    key.rotation_euler = (math.radians(50), 0.0, math.radians(-35))

    bpy.ops.object.light_add(type="AREA", location=(2.6, 5.0, 3.2))
    rim = bpy.context.active_object
    rim.data.energy = 2200
    rim.data.size = 3
    rim.data.color = (1.0, 0.66, 0.38)
    rim.rotation_euler = (math.radians(118), 0.0, math.radians(158))

    # Fill from below, very dim, so the underside is not pure black against a dark board.
    bpy.ops.object.light_add(type="AREA", location=(0.0, -3.0, -4.0))
    fill = bpy.context.active_object
    fill.data.energy = 140
    fill.data.size = 8
    fill.data.color = (0.55, 0.62, 0.9)
    fill.rotation_euler = (math.radians(-40), 0.0, 0.0)


def configure_render(engine):
    scene = bpy.context.scene
    scene.render.engine = engine
    scene.render.resolution_x = RESOLUTION
    scene.render.resolution_y = RESOLUTION
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"

    # Blender defaults to the AgX view transform, which desaturates bright colours hard —
    # it is built for photographic realism, and it turns a saturated orange ember into
    # cream. Sprites want the colour they were authored in.
    try:
        scene.view_settings.view_transform = "Standard"
        scene.view_settings.look = "None"
    except TypeError:
        pass

    if engine == "CYCLES":
        scene.cycles.samples = 64


def render_to(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def render_idle_frames(spec, parts, frames=12, amplitude=0.18):
    """A bob cycle, written out as numbered frames for ffmpeg to pack into a sheet."""
    origins = [Vector(obj.location) for obj in parts]
    directory = os.path.join(OUT_DIR, f"{spec['id']}_idle")
    os.makedirs(directory, exist_ok=True)

    for frame in range(frames):
        offset = math.sin(2 * math.pi * frame / frames) * amplitude
        for obj, origin in zip(parts, origins):
            obj.location = origin + Vector((0.0, 0.0, offset))
        render_to(os.path.join(directory, f"{frame:02d}.png"))

    for obj, origin in zip(parts, origins):
        obj.location = origin


def main():
    animate = "--idle" in sys.argv
    os.makedirs(OUT_DIR, exist_ok=True)

    engine = pick_engine()
    print(f"[art] render engine: {engine}")

    for spec in ENEMIES:
        clear_scene()
        configure_render(engine)
        setup_camera_and_lights()
        parts = build_enemy(spec)

        render_to(os.path.join(OUT_DIR, f"{spec['id']}.png"))
        print(f"[art] wrote {spec['id']}.png")

        if animate:
            render_idle_frames(spec, parts)
            print(f"[art] wrote {spec['id']} idle frames")

    print("[art] done")


if __name__ == "__main__":
    main()
