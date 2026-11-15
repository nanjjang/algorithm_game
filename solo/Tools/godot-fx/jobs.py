"""Job list for the Godot FX bake. Writes jobs.json (read by bake.gd and post.py).

Each job: id, orig (path under Assets/Resources/Art of the sheet it replaces),
w/h/frames (must match the original's frame size and count), shader + params,
ramp (palettes.py), ss (supersample factor)."""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True  # python -I ignores PYTHONDONTWRITEBYTECODE
JOBS = []


def job(orig, w, h, n, shader, ramp, params=None, **kw):
    jid = os.path.splitext(orig)[0].replace("/", "__")
    d = {"id": jid, "orig": orig, "w": w, "h": h, "frames": n, "shader": shader,
         "ramp": ramp, "params": params or {}}
    d.update(kw)
    JOBS.append(d)
    return d


ELEM = ["arcane", "fire", "lightning"]
SEED = {"arcane": 3.1, "fire": 1.7, "lightning": 5.3, "ice": 2.2}
JAG = {"arcane": 0.0, "fire": 0.25, "lightning": 1.0, "ice": 0.0}
EMB = {"arcane": 4, "fire": 10, "lightning": 2, "ice": 0}

# ---------------------------------------------------------------- player hits
for el in ELEM:
    job("effects/player/hit_%s_32x32_f3.png" % el, 32, 32, 3, "burst", el,
        {"rays": 8, "ray_len": 0.82, "ray_w": 2.2, "core": 3.5, "sparkles": 4,
         "spark_arm": 2.0, "jag": JAG[el] * 0.6, "embers": EMB[el] // 3}, seed=SEED[el], rim=2, floor=2)
    job("effects/player/hit_large_%s_64x64_f4.png" % el, 64, 64, 4, "burst", el,
        {"rays": 12, "ray_len": 0.9, "ray_w": 3.6, "core": 7.0, "sparkles": 5,
         "spark_arm": 2.5, "jag": JAG[el], "embers": EMB[el] // 2, "shard": 0.0, "ring": 0.5},
        seed=SEED[el], rim=2, floor=2)

# ---------------------------------------------------------------- explosions
WOB = {"arcane": (2.0, 6.0, 0.0), "fire": (4.0, 10.0, 0.0), "lightning": (4.0, 14.0, 1.0), "ice": (3.0, 12.0, 1.0)}
for el in ELEM + ["ice"]:
    w = WOB[el]
    job("effects/player/explosion_%s_256x256_f5.png" % el, 256, 256, 5, "ring", el,
        {"r0": 0.3, "r1": 0.96, "th0": 13.0, "th1": 3.0, "glow": 0.55,
         "breakup": 0.6, "fill": 0.0, "debris": 22, "inner": 0.6, "ticks": 0,
         "wob": w[0], "wob_freq": w[1], "wob_jag": w[2], "fade": 0.45},
        seed=SEED[el], ss=2, rim=1, floor=2)

GAM = {"arcane": 1.15, "fire": 1.45, "lightning": 1.2, "ice": 1.0}
for j in JOBS:
    j["gamma"] = GAM.get(j["ramp"], 1.0)

ALL4 = ELEM + ["ice"]

# ---------------------------------------------------------------- player misc
job("effects/player/critical_hit_32x32_f3.png", 32, 32, 3, "burst", "steam",
    {"rays": 8, "ray_len": 0.85, "ray_w": 2.0, "core": 3.0, "sparkles": 4, "spark_arm": 2.0},
    seed=4.4, rim=2, floor=2)
job("effects/player/critical_muzzle_32x32_f3.png", 32, 32, 3, "ring", "steam",
    {"r0": 0.35, "r1": 0.85, "th0": 4.0, "th1": 2.0, "glow": 0.4, "breakup": 0.5,
     "debris": 5, "inner": 0.0, "fade": 0.3}, seed=1.3, rim=1, floor=2)
job("effects/player/heat_lock_steam_32x32_f4.png", 32, 32, 4, "smoke", "steam",
    {"puffs": 4, "size0": 0.2, "grow": 0.1, "spread": 0.15, "rise": 0.25, "base_y": 0.12,
     "hi": 0.9, "lo": 0.3}, seed=2.0, rim=1)
job("effects/player/mana_bolt_16x16_f2.png", 16, 16, 2, "orb", "arcane",
    {"radius": 0.2, "pulse": 0.12, "halo": 0.3, "star": 0.75}, loop=True, seed=1.0, ss=6)
job("effects/player/fireball_32x24_f4.png", 32, 24, 4, "flame", "fire",
    {"horiz": 1.0, "base": 0.78, "radius": 0.36, "height": 0.85, "turb": 0.6, "core_ball": 0.7,
     "embers": 2, "scale": 1.6}, loop=True, seed=3.0, gamma=1.45, rim=1, floor=2)
job("effects/player/chain_lightning_64x16_f2.png", 64, 16, 2, "bolt", "lightning",
    {"horiz": 1.0, "amp": 4.0, "freq": 0.18, "thick": 1.8, "glow_w": 1.6, "branches": 1},
    seed=5.0, gamma=1.2)
job("effects/player/graze_strike_32x128_f2.png", 32, 128, 2, "bolt", "lightning",
    {"amp": 8.0, "freq": 0.07, "thick": 3.6, "glow_w": 2.2, "branches": 2},
    seed=6.0, gamma=1.2)

# ---------------------------------------------------------------- wand
for el in ALL4:
    job("effects/wand/muzzle_%s_32x32_f3.png" % el, 32, 32, 3, "muzzle", el,
        {"petals": 3, "spread": 0.6, "jag": 1.0 if el == "lightning" else 0.0, "sparkles": 3},
        seed=SEED[el], gamma=GAM[el], rim=1, floor=2)
    job("effects/wand/trail_%s_32x16_f4.png" % el, 32, 16, 4, "comet", el,
        {"head_x": 0.32, "head_r": 2.6, "tail_w": 2.6, "wave": 1.2 if el != "lightning" else 0.3,
         "sparks": 3, "jag": 1.0 if el == "lightning" else 0.0, "flame": 1.0 if el == "fire" else 0.0},
        loop=True, seed=SEED[el], gamma=GAM[el], floor=2)
    job("effects/wand/footprint_%s_32x16_f4.png" % el, 32, 16, 4, "smoke", el,
        {"puffs": 4, "size0": 0.3, "grow": 0.12, "spread": 0.5, "rise": 0.1, "base_y": 0.05,
         "flat_y": 0.55, "hi": 0.75, "lo": 0.15}, seed=SEED[el], gamma=GAM[el], rim=1)

# ---------------------------------------------------------------- special
for el in ALL4:
    job("effects/special/release_%s_64x64_f4.png" % el, 64, 64, 4, "ring", el,
        {"r0": 0.35, "r1": 0.95, "th0": 5.0, "th1": 1.5, "glow": 0.5, "breakup": 0.55,
         "debris": 8, "inner": 0.4, "ticks": 8, "wob": WOB[el][0] * 0.4, "wob_freq": WOB[el][1],
         "wob_jag": WOB[el][2], "fade": 0.4}, seed=SEED[el], gamma=GAM[el], rim=1, floor=2)
job("effects/special/dodge_dust_32x32_f4.png", 32, 32, 4, "smoke", "smoke",
    {"puffs": 5, "size0": 0.16, "grow": 0.1, "spread": 0.45, "rise": 0.08, "base_y": 0.22,
     "flat_y": 0.6, "hi": 0.85, "lo": 0.3}, seed=7.0, rim=1)
job("effects/special/purge_steam_32x32_f4.png", 32, 32, 4, "smoke", "steam",
    {"puffs": 5, "size0": 0.17, "grow": 0.1, "spread": 0.2, "rise": 0.35, "base_y": 0.2,
     "hi": 0.92, "lo": 0.35, "wisp": 0.6}, seed=8.0, rim=1)
job("effects/special/kiln_ember_8x8_f2.png", 8, 8, 2, "orb", "fire",
    {"radius": 0.26, "pulse": 0.2, "halo": 0.25, "star": 0.0, "mid": 0.8}, loop=True, seed=2.0,
    gamma=1.3, ss=8)
job("effects/special/just_star_48x48_f2.png", 48, 48, 2, "star", "steam",
    {"arm": 0.44, "waist": 0.12, "glow": 0.45, "diag": 0.45, "glints": 2, "scale_a": 0.75, "scale_b": 1.0},
    seed=1.0)
job("effects/special/lightning_dash_256x64_f3.png", 256, 64, 3, "bolt", "lightning",
    {"horiz": 1.0, "amp": 14.0, "freq": 0.035, "thick": 3.5, "glow_w": 3.0, "branches": 3, "fade": 0.35},
    seed=9.0, gamma=1.2, ss=3)
job("effects/special/kiln_border_320x320_f4.png", 320, 320, 4, "ring", "fire",
    {"r0": 0.55, "r1": 0.97, "th0": 5.0, "th1": 3.0, "glow": 0.6, "breakup": 0.0,
     "debris": 20, "inner": 0.35, "ticks": 0, "dashes": 10, "wob": 2.0, "wob_freq": 10.0, "fade": 0.3},
    seed=3.3, gamma=1.45, ss=2, rim=1, floor=2)

# ---------------------------------------------------------------- enemy
job("effects/enemy/death_smoke_48x48_f4.png", 48, 48, 4, "smoke", "smoke",
    {"puffs": 6, "size0": 0.17, "grow": 0.12, "spread": 0.35, "rise": 0.12, "base_y": 0.1,
     "hi": 0.85, "lo": 0.2}, seed=3.0, rim=1)
job("effects/enemy/explosion_128x128_f5.png", 128, 128, 5, "burst", "enemy_red",
    {"rays": 14, "ray_len": 0.95, "ray_w": 4.0, "core": 12.0, "sparkles": 6, "spark_arm": 3.0,
     "embers": 10, "ring": 0.7}, seed=2.0, rim=2, floor=2, gamma=1.5)
# impact_* play the moment an area attack lands (ThreatLayer), stretched over the area.
job("effects/enemy/impact_circle_128x128_f3.png", 128, 128, 3, "ring", "enemy_red",
    {"r0": 0.4, "r1": 0.97, "th0": 9.0, "th1": 2.5, "glow": 0.6, "breakup": 0.45, "debris": 14,
     "inner": 0.5, "fill": 0.0, "fade": 0.3}, seed=1.0, rim=1, floor=2, gamma=1.4)
job("effects/enemy/impact_lane_128x64_f3.png", 128, 64, 3, "ring", "enemy_red",
    {"r0": 0.35, "r1": 0.97, "th0": 7.0, "th1": 2.0, "glow": 0.6, "breakup": 0.45, "debris": 12,
     "inner": 0.5, "fill": 0.0, "squash": 0.5, "fade": 0.3}, seed=2.0, rim=1, floor=2, gamma=1.4)
job("effects/enemy/impact_fan_128x128_f3.png", 128, 128, 3, "ring", "enemy_mag",
    {"r0": 0.35, "r1": 0.95, "th0": 10.0, "th1": 3.0, "glow": 0.6, "breakup": 0.3, "debris": 8,
     "inner": 0.5, "fill": 0.2, "arc": 0.33, "arc_dir": 0.0, "fade": 0.3}, seed=3.0, rim=1, floor=2, gamma=1.3)
job("effects/enemy/frost_field_160x160_f2.png", 160, 160, 2, "telegraph", "enemy_mag",
    {"mode": 0, "size_a": 1.0, "size_b": 1.0, "rim_w": 1.2, "fill": 0.26, "charge": 0.0,
     "hatch": 0.0, "glints": 14, "fill_sel": 1.0}, loop=True, ramp2="ice", seed=4.0, alpha_gain=1.0)
job("effects/enemy/rage_smoke_32x48_f4.png", 32, 48, 4, "smoke", "shadow_e",
    {"puffs": 4, "size0": 0.24, "grow": 0.1, "spread": 0.12, "rise": 0.35, "base_y": 0.25,
     "hi": 0.95, "lo": 0.25, "wisp": 1.0}, seed=5.0, rim=1)
job("effects/enemy/round_large_24x24_f2.png", 24, 24, 2, "orb", "enemy_red",
    {"radius": 0.4, "pulse": 0.05, "halo": 0.25, "hollow": 1.0, "mid": 0.6}, loop=True, seed=1.0, rim=2)
job("effects/enemy/round_small_16x16_f2.png", 16, 16, 2, "orb", "enemy_red",
    {"radius": 0.38, "pulse": 0.06, "halo": 0.25, "hollow": 1.0, "mid": 0.6}, loop=True, seed=2.0, rim=2, ss=6)
job("effects/enemy/lightning_strike_64x128_f3.png", 64, 128, 3, "bolt", "enemy_red",
    {"amp": 11.0, "freq": 0.06, "thick": 5.0, "glow_w": 3.0, "branches": 3, "tile": 1.0, "fade": 0.2},
    seed=3.0, gamma=1.4, rim=1)
job("effects/enemy/elite_column_32x128_f3.png", 32, 128, 3, "pillar", "khaki",
    {"width": 0.32, "tip": 0.04, "base": 0.95, "grow0": 0.6, "motes": 4, "base_glow": 0.8},
    seed=2.0, rim=1)
job("effects/enemy/spore_cloud_128x128_f4.png", 128, 128, 4, "smoke", "spore",
    {"puffs": 8, "size0": 0.13, "grow": 0.1, "spread": 0.32, "rise": 0.05, "base_y": 0.0,
     "hi": 0.85, "lo": 0.25}, seed=6.0, rim=1)
job("effects/enemy/stain_64x64_f1.png", 64, 64, 1, "cracks", "smoke",
    {"reach": 0.95, "fissures": 7, "cell": 8.0, "web": 0.8, "stain": 0.3, "crack_h": 0.62}, seed=2.0)
job("effects/enemy/soil_crack_64x32_f2.png", 64, 32, 2, "cracks", "soil",
    {"reach": 0.95, "fissures": 6, "cell": 7.0, "web": 0.7, "stain": 0.15, "grow_a": 0.6, "crack_h": 0.58}, seed=3.0)
job("effects/enemy/soil_particle_8x8_f1.png", 8, 8, 1, "orb", "soil",
    {"radius": 0.34, "pulse": 0.0, "halo": 0.0, "mid": 0.6}, seed=1.0, ss=8, rim=2)
job("effects/enemy/mirror_silver_particle_8x8_f2.png", 8, 8, 2, "orb", "steam",
    {"radius": 0.16, "pulse": 0.2, "halo": 0.3, "star": 1.0, "mid": 0.85}, loop=True, seed=1.0, ss=8)

# ---------------------------------------------------------------- status
job("effects/status/frost_particle_4x4_f2.png", 4, 4, 2, "orb", "ice",
    {"radius": 0.3, "pulse": 0.25, "halo": 0.4, "mid": 0.85}, loop=True, seed=1.0, ss=8)
job("effects/status/crystal_particle_16x16_f3.png", 16, 16, 3, "star", "ice",
    {"arm": 0.46, "waist": 0.16, "glow": 0.3, "diag": 0.0, "glints": 0, "scale_a": 1.0, "scale_b": 0.55},
    seed=2.0, ss=6)
job("effects/status/static_outline_64x64_f2.png", 64, 64, 2, "ring", "lightning",
    {"r0": 0.62, "r1": 0.62, "th0": 2.2, "th1": 2.2, "glow": 0.5, "breakup": 0.0, "debris": 6,
     "inner": 0.0, "wob": 2.5, "wob_freq": 16.0, "wob_jag": 1.0, "fade": 0.0}, loop=True, seed=5.0,
    gamma=1.2, rim=1, floor=2)
job("effects/status/corrosion_32x32_f2.png", 32, 32, 2, "cracks", "dark",
    {"reach": 0.95, "fissures": 6, "cell": 6.0, "web": 0.45, "stain": 0.1, "grow_a": 0.75,
     "glow_edge": 0.0, "crack_h": 0.62}, seed=4.0)

# ---------------------------------------------------------------- loot
job("effects/loot/drop_column_16x64_f3.png", 16, 64, 3, "pillar", "gold",
    {"width": 0.34, "tip": 0.04, "base": 0.95, "grow0": 0.7, "motes": 3, "base_glow": 0.8},
    seed=1.0, rim=1)
job("effects/loot/exit_marker_64x64_f2.png", 64, 64, 2, "portal", "arcane",
    {"radius": 0.46, "squash": 0.5, "arms": 3, "twist": 2.0, "motes": 5, "cy": 0.12},
    loop=True, seed=2.0, gamma=1.15)
job("effects/loot/campfire_64x64_f4.png", 64, 64, 4, "flame", "fire",
    {"base": 0.86, "radius": 0.3, "height": 0.78, "turb": 0.6, "tongues": 2, "embers": 4, "scale": 1.0},
    loop=True, seed=2.0, gamma=1.45, rim=1, floor=1)

# ---------------------------------------------------------------- ritual
job("effects/ritual/failure_smoke_64x64_f5.png", 64, 64, 5, "smoke", "smoke",
    {"puffs": 7, "size0": 0.16, "grow": 0.12, "spread": 0.3, "rise": 0.2, "base_y": 0.12,
     "hi": 0.8, "lo": 0.15}, seed=9.0, rim=1)
job("effects/ritual/failure_dust_48x48_f4.png", 48, 48, 4, "smoke", "soil",
    {"puffs": 6, "size0": 0.13, "grow": 0.08, "spread": 0.4, "rise": 0.05, "base_y": 0.2,
     "flat_y": 0.6, "hi": 0.85, "lo": 0.3}, seed=10.0, rim=1)
job("effects/ritual/gold_burst_256x256_f8.png", 256, 256, 8, "burst", "gold",
    {"rays": 16, "ray_len": 0.92, "ray_w": 12.0, "core": 26.0, "sparkles": 8, "spark_arm": 5.0,
     "embers": 16, "ring": 0.0}, seed=1.0, ss=2, rim=2, floor=2)
job("effects/ritual/gold_aureole_256x256_f8.png", 256, 256, 8, "ring", "gold",
    {"r0": 0.35, "r1": 0.95, "th0": 9.0, "th1": 2.5, "glow": 0.5, "breakup": 0.45, "debris": 18,
     "inner": 0.5, "ticks": 16, "fade": 0.45}, seed=2.0, ss=2, rim=1, floor=2)
job("effects/ritual/artisan_ember_32x32_f6.png", 32, 32, 6, "flame", "fire",
    {"base": 0.86, "radius": 0.28, "height": 0.7, "turb": 0.6, "tongues": 2, "embers": 3, "scale": 1.8},
    loop=True, seed=4.0, gamma=1.45, rim=1, floor=1)
job("effects/ritual/ward_burn_64x96_f6.png", 64, 96, 6, "flame", "fire",
    {"base": 0.9, "radius": 0.34, "height": 0.85, "turb": 0.65, "tongues": 2, "embers": 5, "envelope": 1.0},
    seed=6.0, gamma=1.45, rim=1, floor=1)
for el in ELEM:
    job("effects/ritual/success_mote_%s_16x16_f3.png" % el, 16, 16, 3, "orb", el,
        {"radius": 0.17, "pulse": 0.0, "halo": 0.5, "star": 0.9, "shrink": 0.45}, seed=SEED[el],
        gamma=GAM[el], ss=6)
    common = {"tip": 0.08, "base": 0.93, "motes": 12, "base_glow": 1.0,
              "flame": 1.0 if el == "fire" else 0.0, "bolt": 1.0 if el == "lightning" else 0.0}
    job("effects/ritual/success_column_%s_192x540_f6.png" % el, 192, 540, 6, "pillar", el,
        dict(common, width=0.13 if el == "lightning" else 0.2, grow0=0.25, widen=0.25, fade_end=0.5), seed=SEED[el], gamma=GAM[el],
        ss=2, rim=1, floor=2)
    job("effects/ritual/max_column_%s_192x540_f8.png" % el, 192, 540, 8, "pillar", el,
        dict(common, width=0.18 if el == "lightning" else 0.26, tip=0.02, grow0=0.2, widen=0.35, fade_end=0.4, motes=16),
        seed=SEED[el] + 1.0, gamma=GAM[el], ss=2, rim=1, floor=2)

# ---------------------------------------------------------------- world ground textures
# Seamless 64x64 material textures; tiles.py recolours them per day/dusk/night and
# composites edge/corner cells with the masks of the original sheets.
GROUND = {"grass": (0, 4), "dirt": (1, 4), "packed": (2, 4), "paving": (3, 6), "slate": (4, 4)}
for name, (mat, nvar) in GROUND.items():
    for v in range(nvar):
        JOBS.append({"id": "ground__%s__v%d" % (name, v), "orig": None, "w": 64, "h": 64, "frames": 1,
                     "shader": "ground", "ramp": "smoke", "params": {"mat": mat, "variant": v},
                     "seed": 11.0 + mat, "ss": 4})

# ---------------------------------------------------------------- per-character attack styles (styles.py)
import sys as _sys  # noqa: E402
_sys.path.insert(0, HERE)
from styles import STYLES  # noqa: E402
JOBS.extend(STYLES)

if __name__ == "__main__":
    ids = [j["id"] for j in JOBS]
    assert len(ids) == len(set(ids)), "duplicate job ids"
    with open(os.path.join(HERE, "jobs.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(JOBS, f, indent=1)
    print("jobs:", len(JOBS))
