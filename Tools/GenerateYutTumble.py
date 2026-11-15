"""Render the original yut artwork on a textured half-cylinder, without AI frames.

Requires Python 3, Pillow and NumPy. Run from any working directory.
96 frames, per-variant playback speed and 3D axes, original-size artwork.
Fixed 688 x 1344px frames. The original back/landing images are pasted 1:1.
Use --variant Yut_BackToBack, Yut_BellyToBelly or Yut_BellyToBack.
Add --belly-x to render a separate _X sheet using Yut_BellyX.png.
"""
from pathlib import Path
import argparse
import json
import math

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets/Art/Yut"
PREVIEW = ROOT / "Docs/Previews"
CELL_W, CELL_H, COLS, COUNT, AIR_LAST = 688, 1344, 12, 96, 79
REST_X, REST_Y, RISE, BOUNCE = 343.5, 1016.5, 592.45, 52.0
PREVIEW_SIZE = (344, 672)
SS = 2
W, LENGTH, DEPTH = 1.0, 3.60, 0.50
PIXELS_X, PIXELS_Y = 170.0, 163.0
PRESETS = {
    "Yut_BackToBelly": dict(start="back", end="belly", turns=2.5,
                           axis=(.92, .25, .30), fps=60),
    "Yut_BackToBack": dict(start="back", end="back", turns=3,
                          axis=(.20, .94, .28), fps=52),
    "Yut_BellyToBelly": dict(start="belly", end="belly", turns=3,
                            axis=(.56, -.55, .62), fps=56),
    "Yut_BellyToBack": dict(start="belly", end="back", turns=2.5,
                           axis=(.59, -.51, -.626), fps=64),
}
LANDING_ALIGN_START = .75


def unit(v):
    v = np.asarray(v, dtype=float)
    return v / np.linalg.norm(v)


def rotation(axis, angle):
    x, y, z = unit(axis)
    skew = np.array([[0, -z, y], [z, 0, -x], [-y, x, 0]])
    axis = np.array([x, y, z])
    return (math.cos(angle) * np.eye(3) + math.sin(angle) * skew
            + (1 - math.cos(angle)) * np.outer(axis, axis))


def orientation(spec, t):
    """Fixed 3D primary spin, with a smooth target-face alignment before impact.

    An arbitrary axis with a Z component does not swap faces after a half-turn.
    Only the final quarter of flight adds the residual rotation needed to land
    upright. All of this is baked into PNG frames; no runtime axis selection.
    """
    axis = unit(spec["axis"])
    degrees = 360*spec["turns"]*t
    start = np.eye(3) if spec["start"] == "back" else rotation([1, 0, 0], math.pi)
    matrix = rotation(axis, math.radians(degrees)) @ start
    if t > LANDING_ALIGN_START:
        target = np.eye(3) if spec["end"] == "back" else rotation([1, 0, 0], math.pi)
        raw_end = rotation(axis, math.radians(360*spec["turns"])) @ start
        residual = target @ raw_end.T
        angle = math.acos(float(np.clip((np.trace(residual)-1)/2, -1, 1)))
        if angle > 1e-7:
            residual_axis = unit([residual[2, 1]-residual[1, 2],
                                  residual[0, 2]-residual[2, 0],
                                  residual[1, 0]-residual[0, 1]])
            progress = (t-LANDING_ALIGN_START)/(1-LANDING_ALIGN_START)
            progress = progress*progress*(3-2*progress)
            matrix = rotation(residual_axis, angle*progress) @ matrix
    return matrix, degrees


def mesh():
    """Half elliptical cylinder: painted curved back, flat belly, two end caps."""
    faces = []

    def quad(points, uv, normal, material):
        for ids in ((0, 1, 2), (0, 2, 3)):
            faces.append((np.array([points[i] for i in ids]),
                          np.array([uv[i] for i in ids]), unit(normal), material))

    angles = np.linspace(0, math.pi, 33)
    for a, b in zip(angles[:-1], angles[1:]):
        xa, za = W / 2 * math.cos(a), DEPTH * (math.sin(a) - .5)
        xb, zb = W / 2 * math.cos(b), DEPTH * (math.sin(b) - .5)
        points = [(xa, LENGTH/2, za), (xa, -LENGTH/2, za),
                  (xb, -LENGTH/2, zb), (xb, LENGTH/2, zb)]
        ua, ub = xa/W + .5, xb/W + .5
        normal = [math.cos((a+b)/2)/(W/2), 0, math.sin((a+b)/2)/DEPTH]
        quad(points, [(ua, 0), (ua, 1), (ub, 1), (ub, 0)], normal, "back")

    # Belly UV is oriented to match the supplied upright belly after 2.5 turns.
    quad([(-W/2, LENGTH/2, -DEPTH/2), (W/2, LENGTH/2, -DEPTH/2),
          (W/2, -LENGTH/2, -DEPTH/2), (-W/2, -LENGTH/2, -DEPTH/2)],
         [(0, 1), (1, 1), (1, 0), (0, 0)], [0, 0, -1], "belly")
    for sign in (-1, 1):
        center = [0, sign * LENGTH/2, -DEPTH/2]
        for a, b in zip(angles[:-1], angles[1:]):
            ring = [[W/2 * math.cos(v), sign * LENGTH/2,
                     DEPTH * (math.sin(v) - .5)] for v in (a, b)]
            uv = [[.5, 1]] + [[math.cos(v)/2+.5, 1-math.sin(v)] for v in (a, b)]
            faces.append((np.array([center] + ring), np.array(uv),
                          np.array([0, sign, 0]), "cap"))
    return faces


def opaque_surface(image):
    """Extend the source's cutout edge colors to the physical mesh boundary.

    Alpha outlines belong to the source's front view. Reusing them as holes in
    a solid 3D body would disconnect its end caps at oblique camera angles.
    The mesh and the final silhouette outline define the rendered alpha.
    """
    pixels = np.array(image).copy()
    valid_rows = np.flatnonzero((pixels[..., 3] >= 128).any(axis=1))
    for yy in range(pixels.shape[0]):
        source_y = valid_rows[np.abs(valid_rows-yy).argmin()]
        row = pixels[source_y].copy()
        valid_x = np.flatnonzero(row[:, 3] >= 128)
        nearest = valid_x[np.abs(np.arange(pixels.shape[1])[:, None]-valid_x).argmin(axis=1)]
        missing = pixels[yy, :, 3] < 128
        pixels[yy, missing] = row[nearest[missing]]
    pixels[..., 3] = 255
    return pixels


def render(matrix, center_y, faces, textures):
    width, height = CELL_W * SS, CELL_H * SS
    rgba = np.zeros((height, width, 4), dtype=np.uint8)
    zbuffer = np.zeros((height, width), dtype=float)
    light = unit([-.45, .65, 1.3])
    for vertices, uv, normal, material in faces:
        points = vertices @ matrix.T
        normal = matrix @ normal
        # Back-face culling uses a camera at (0,0,8), not a fixed sprite swap.
        if np.dot(normal, np.array([0, 0, 8]) - points.mean(axis=0)) <= 0:
            continue
        inv_depth = 1 / (8 - points[:, 2])
        xy = np.column_stack((REST_X + points[:, 0]*PIXELS_X*8*inv_depth,
                              center_y - points[:, 1]*PIXELS_Y*8*inv_depth)) * SS
        lo = np.maximum(np.floor(xy.min(axis=0)).astype(int), 0)
        hi = np.minimum(np.ceil(xy.max(axis=0)).astype(int), [width-1, height-1])
        if np.any(lo > hi):
            continue
        xx, yy = np.meshgrid(np.arange(lo[0], hi[0]+1)+.5,
                             np.arange(lo[1], hi[1]+1)+.5)
        x0, y0 = xy[0]; x1, y1 = xy[1]; x2, y2 = xy[2]
        denom = (y1-y2)*(x0-x2)+(x2-x1)*(y0-y2)
        if abs(denom) < 1e-10:
            continue
        b0 = ((y1-y2)*(xx-x2)+(x2-x1)*(yy-y2))/denom
        b1 = ((y2-y0)*(xx-x2)+(x0-x2)*(yy-y2))/denom
        bary = np.stack([b0, b1, 1-b0-b1], axis=-1)
        inside = (bary >= -1e-7).all(axis=-1)
        depth = bary @ inv_depth
        target_depth = zbuffer[lo[1]:hi[1]+1, lo[0]:hi[0]+1]
        visible = inside & (depth > target_depth)
        if not visible.any():
            continue
        # A projected narrow mesh strip has a large empty bounding box. Sample
        # only covered pixels so high-resolution diagonal spins stay practical.
        tex_uv = (bary[visible] @ (uv * inv_depth[:, None])) / depth[visible, None]
        texture = textures[material]
        tx = np.clip(np.rint(tex_uv[..., 0]*(texture.shape[1]-1)),
                     0, texture.shape[1]-1).astype(int)
        ty = np.clip(np.rint(tex_uv[..., 1]*(texture.shape[0]-1)),
                     0, texture.shape[0]-1).astype(int)
        color = texture[ty, tx].copy()
        opaque = color[..., 3] > 100
        if not opaque.all():
            selected = np.flatnonzero(visible)
            visible.flat[selected[~opaque]] = False
            color = color[opaque]
        shade = .74 + .26 * max(0, np.dot(normal, light))
        color[..., :3] = np.clip(color[..., :3].astype(float)*shade, 0, 255)
        target = rgba[lo[1]:hi[1]+1, lo[0]:hi[0]+1]
        target[visible] = color
        target_depth[visible] = depth[visible]
    result = Image.fromarray(rgba)
    # Match the original dark silhouette, especially on the newly visible ends.
    outline = Image.new("RGBA", result.size, (53, 25, 12, 255))
    outline.putalpha(result.getchannel("A").filter(ImageFilter.MaxFilter(5)))
    outline.alpha_composite(result)
    return outline.resize((CELL_W, CELL_H), Image.Resampling.LANCZOS)


def visible_bounds(image):
    return image.getchannel("A").point(lambda a: 255 if a >= 128 else 0).getbbox()


def source_frame(source, center_y):
    """Preserve original source pixels, without a resize or PPU compensation."""
    result = Image.new("RGBA", (CELL_W, CELL_H))
    result.paste(source, (round(REST_X-source.width/2), round(center_y-source.height/2)))
    return result


def calibrate_native_size(back, faces, textures):
    """Set a single camera scale for the entire animation, at native resolution."""
    global PIXELS_X, PIXELS_Y
    target = visible_bounds(back)
    target_w, target_h = target[2]-target[0], target[3]-target[1]
    for _ in range(6):
        bbox = visible_bounds(render(np.eye(3), REST_Y, faces, textures))
        actual_w, actual_h = bbox[2]-bbox[0], bbox[3]-bbox[1]
        if abs(actual_w-target_w) <= 1 and abs(actual_h-target_h) <= 1:
            return
        PIXELS_X *= (target_w-2)/(actual_w-2)
        PIXELS_Y *= (target_h-2)/(actual_h-2)
    raise AssertionError(("native camera calibration", target_w, target_h, bbox))


def main(name, belly_x=False):
    spec = PRESETS[name]
    base_name = name
    if belly_x:
        name += "_X"
    PREVIEW.mkdir(parents=True, exist_ok=True)
    back = Image.open(ART / "Yut_Back.png").convert("RGBA")
    belly_filename = "Yut_BellyX.png" if belly_x else "Yut_Belly.png"
    belly = Image.open(ART / belly_filename).convert("RGBA")
    assert belly.size == back.size, "Belly and back source dimensions must match"
    # Sample the original back's brown end-grain for both exposed end caps.
    cap = back.crop((20, 548, back.width-20, 590)).resize((160, 100))
    cap.putalpha(255)
    textures = {"back": opaque_surface(back), "belly": opaque_surface(belly), "cap": np.array(cap)}
    faces = mesh()
    calibrate_native_size(back, faces, textures)
    source_images = {"back": back, "belly": belly}
    start_source, end_source = source_images[spec["start"]], source_images[spec["end"]]
    expected_end = np.eye(3) if spec["end"] == "back" else rotation([1, 0, 0], math.pi)
    assert np.allclose(orientation(spec, 1)[0], expected_end), "Final face orientation mismatch"
    manifest, preview = [], []
    sheet = Image.new("RGBA", (COLS*CELL_W, (COUNT//COLS)*CELL_H))
    checker = Image.new("RGB", PREVIEW_SIZE, (52, 57, 65))
    draw = ImageDraw.Draw(checker)
    for yy in range(0, PREVIEW_SIZE[1], 16):
        for xx in range(0, PREVIEW_SIZE[0], 16):
            if (xx//16 + yy//16) % 2 == 0:
                draw.rectangle((xx, yy, xx+15, yy+15), fill=(60, 66, 75))
    for index in range(COUNT):
        if index <= AIR_LAST:
            t = index / AIR_LAST
            matrix, degrees = orientation(spec, t)
            center_y = REST_Y - RISE * 4*t*(1-t)
        else:
            t = (index-AIR_LAST)/(COUNT-1-AIR_LAST)
            matrix, degrees = orientation(spec, 1)
            center_y = REST_Y - BOUNCE * math.sin(math.pi*t) * (1-t)
        if index == 0:
            frame = source_frame(start_source, center_y)
        elif index >= AIR_LAST:
            frame = source_frame(end_source, center_y)
        else:
            frame = render(matrix, center_y, faces, textures)
        bbox = visible_bounds(frame)
        assert (bbox and bbox[0] > 2 and bbox[1] > 2
                and bbox[2] < CELL_W-2 and bbox[3] < CELL_H-2), (index, bbox)
        if index in (0, COUNT-1):
            source = start_source if index == 0 else end_source
            source_x, source_y = round(REST_X-source.width/2), round(REST_Y-source.height/2)
            copied = frame.crop((source_x, source_y, source_x+source.width, source_y+source.height))
            assert np.array_equal(np.array(copied), np.array(source)), "Original source pixels changed"
        sheet.paste(frame, ((index % COLS)*CELL_W, (index//COLS)*CELL_H))
        reduced = frame.resize(PREVIEW_SIZE, Image.Resampling.LANCZOS)
        image = checker.copy()
        image.paste(reduced, mask=reduced.getchannel("A"))
        preview.append(image)
        manifest.append({"frame": index, "angle_degrees": round(degrees, 4),
                         "center_y": round(center_y, 4), "bounds": bbox,
                         "back_normal_z": round(float((matrix @ [0, 0, 1])[2]), 6)})
        if (index+1) % 24 == 0:
            print(f"{name}: rendered {index+1}/{COUNT}", flush=True)
    assert abs(manifest[AIR_LAST]["angle_degrees"] - 360*spec["turns"]) < .001
    assert abs(manifest[0]["center_y"] - manifest[-1]["center_y"]) < .001
    apex_t = (AIR_LAST//2)/AIR_LAST
    sampled_rise = REST_Y - min(f["center_y"] for f in manifest)
    assert math.isclose(sampled_rise, RISE*4*apex_t*(1-apex_t), abs_tol=.001)
    staged_sheet = PREVIEW / f".{name}.rendering.png"
    sheet.save(staged_sheet, optimize=True)
    sheet.close()
    staged_sheet.replace(ART / f"{name}.png")
    fps = spec["fps"]
    durations = [10*(round((index+1)*100/fps)-round(index*100/fps))
                 for index in range(COUNT)]
    durations[0] += 250
    durations[-1] += 650
    preview[0].save(PREVIEW / f"{name}.gif", save_all=True,
                    append_images=preview[1:], loop=0, duration=durations,
                    disposal=2, optimize=False)
    contact_cell = (CELL_W//4, CELL_H//4)
    contact = Image.new("RGB", (contact_cell[0]*6, contact_cell[1]*3), (52, 57, 65))
    draw = ImageDraw.Draw(contact)
    samples = [0, 8, 16, 24, 32, 40, 48, 56, 64, 72, 79, 95,
               4, 12, 20, 28, 36, 44]
    for slot, index in enumerate(samples):
        x, y = slot % 6*contact_cell[0], slot//6*contact_cell[1]
        contact.paste(preview[index].resize(contact_cell), (x, y))
        draw.text((x+8, y+8), f'{index:02d} / {manifest[index]["angle_degrees"]:.0f} deg', fill="white")
    contact.save(PREVIEW / f"{name}_contact.png")
    start_bounds = visible_bounds(start_source)
    (PREVIEW / f"{name}.json").write_text(json.dumps({
        "base_variant": base_name, "marked_belly": belly_x,
        "belly_texture": belly_filename,
        "fps": fps, "duration_seconds": COUNT/fps,
        "airborne_seconds": AIR_LAST/fps,
        "rise_parameter_px": RISE, "sampled_rise_px": sampled_rise,
        "primary_revolutions_per_second": spec["turns"]*fps/AIR_LAST,
        "primary_degrees_per_second": spec["turns"]*360*fps/AIR_LAST,
        "frames": COUNT, "columns": COLS, "cell": [CELL_W, CELL_H],
        "pivot": [REST_X/CELL_W, (CELL_H-REST_Y)/CELL_H],
        "pixels_per_unit": 100, "first_frame_original_pixels_identical": True,
        "last_frame_original_pixels_identical": True,
        "source_visible_size": [start_bounds[2]-start_bounds[0], start_bounds[3]-start_bounds[1]],
        "projection_scale": [PIXELS_X, PIXELS_Y],
        "turns": spec["turns"], "primary_axis": unit(spec["axis"]).tolist(),
        "axis_space": "X right, Y up, Z toward camera",
        "axis_selection": "fixed at sprite generation; baked frames only",
        "landing_alignment_starts_at_flight_fraction": LANDING_ALIGN_START,
        "start_face": spec["start"], "end_face": spec["end"],
        "sources": ["Yut_Back.png", belly_filename], "frames_data": manifest
    }, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"sheet": str(ART / f"{name}.png"),
                      "preview": str(PREVIEW / f"{name}.gif"),
                      "frames": COUNT, "turns": spec["turns"], "fps": fps,
                      "primary_axis": unit(spec["axis"]).tolist(),
                      "minimum_center_y": min(f["center_y"] for f in manifest)}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--variant", choices=PRESETS, default="Yut_BackToBelly")
    parser.add_argument("--belly-x", action="store_true", help="Use Yut_BellyX.png and output separate _X assets")
    args = parser.parse_args()
    main(args.variant, belly_x=args.belly_x)
