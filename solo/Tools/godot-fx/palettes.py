"""Colour ramps (dark -> light). dark/ice are pulled from the GPT v2 sheets
(median-cut of opaque pixels); the other ramps are built on the same value
structure (deep shadow, 4 body tones, pale tint, near-white core).
Player-side ramps avoid the danger hue range (330..15 deg with S,V >= 70%)."""

RAMPS = {
    # extracted from effects/*dark*_v2.png
    "dark":      ["2e1248", "4a2170", "6a3599", "8240b9", "9b61dd", "ab82e7", "dbbef3", "f6eeff"],
    # extracted from effects/*ice*_v2.png
    "ice":       ["2c4a8c", "4f78c6", "5b90c0", "7aa7df", "8cc1ed", "a1ddf7", "d0ecfa", "fafdfe"],
    "fire":      ["3c1a0c", "6e3014", "a4501c", "d47622", "f09a2e", "f8c25a", "fde49a", "fff6dc"],
    "lightning": ["3a3412", "6a5c1c", "a68e24", "d8bc2c", "f0d444", "f8e672", "fdf3aa", "fffce8"],
    "arcane":    ["0e3a3a", "176258", "21887c", "31ae9c", "56cdb8", "8ae6d2", "c4f6ea", "f2fffb"],
    "gold":      ["3a2a0c", "6e4c12", "a6781c", "d6a42a", "eec444", "f6da7a", "fcefb8", "fffbea"],
    "smoke":     ["1c1a26", "2c2a3a", "403e54", "56546c", "6e6c86", "8c8aa2", "b2b0c4", "dcdae6"],
    "steam":     ["2a3240", "3e4858", "566274", "707e90", "8e9cac", "b0bcc8", "d4dde6", "f2f6fa"],
    "soil":      ["231a14", "35281e", "4a3a2a", "604c36", "786046", "927a5a", "b09a78", "d2c2a4"],
    "khaki":     ["3a3418", "5c5226", "807236", "a49448", "c4b45e", "dccc7e", "eee2a8", "fbf6dc"],
    "heal":      ["3a2418", "64402a", "8e5c3a", "b47e50", "d4a070", "e8c49a", "f6e2c8", "fffaf2"],
    # enemy-only (danger colours allowed)
    "enemy_red": ["3a0a12", "6a1020", "a0182a", "d42234", "f03c44", "f8766c", "fdb4a8", "fff0ec"],
    "enemy_mag": ["38082a", "661046", "9c1a66", "cc2486", "ec44a2", "f67cc2", "fcb8de", "fff0f8"],
    "spore":     ["2a0c26", "4e1840", "7a2460", "a63480", "c8509c", "de80ba", "f0b4d8", "fde8f4"],
    "shadow_e":  ["0c0a14", "16121f", "201a2c", "2c243a", "3a304a", "4c4060", "62567a", "807698"],
}

DANGER_EXEMPT = {"enemy_red", "enemy_mag", "spore", "shadow_e"}

def rgb(hexs):
    return tuple(int(hexs[i:i + 2], 16) for i in (0, 2, 4))

def is_danger(r, g, b):
    import colorsys
    h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    deg = h * 360
    return (deg >= 330 or deg <= 15) and s >= 0.70 and v >= 0.70
