import random

W, H = 1600, 1000


def make_paths(count, seed, stroke_color):
    random.seed(seed)
    out = []
    for i in range(count):
        band = i / max(count - 1, 1)
        y0 = H * (0.12 + 0.88 * band) + random.uniform(-55, 55)
        x0 = random.uniform(-240, W * 0.08)
        length = random.uniform(700, 1500)
        segs = random.randint(5, 8)
        x, y = x0, y0
        d = [f"M{x:.1f},{y:.1f}"]
        for s in range(segs):
            dx = max(70, length / segs + random.uniform(-20, 60))
            dy = -abs(dx) * random.uniform(0.55, 1.1) + random.uniform(-22, 14)
            wobble = random.uniform(30, 85) * (1 if s % 2 == 0 else -1)
            c1x = x + dx * 0.32
            c1y = y + dy * 0.28 + wobble
            c2x = x + dx * 0.68
            c2y = y + dy * 0.7 - wobble * 0.65
            x2, y2 = x + dx, y + dy
            d.append(f"C{c1x:.1f},{c1y:.1f} {c2x:.1f},{c2y:.1f} {x2:.1f},{y2:.1f}")
            x, y = x2, y2
        sw = random.choice([1.6, 2.0, 2.4, 2.9, 3.4, 4.0])
        op = random.choice([0.34, 0.42, 0.5, 0.58, 0.66, 0.74])
        out.append((sw, op, " ".join(d)))
    out.sort(key=lambda t: t[0])
    return [
        f'<path d="{d}" fill="none" stroke="{stroke_color}" stroke-width="{sw}" '
        f'stroke-linecap="round" opacity="{op}"/>'
        for sw, op, d in out
    ]


base = "C:/Users/mdulche/code/Gaia-Life/src/App.Core/wwwroot"
for name, parts in [
    ("gaia-motif.svg", make_paths(48, 11, "#66BB6A")),
    ("gaia-motif-b.svg", make_paths(30, 33, "#66BB6A")),
    ("gaia-motif-dark.svg", make_paths(48, 11, "#81C784")),
    ("gaia-motif-b-dark.svg", make_paths(30, 33, "#81C784")),
]:
    svg = (
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" '
        f'width="{W}" height="{H}" preserveAspectRatio="xMinYMid slice">'
        + "".join(parts)
        + "</svg>"
    )
    with open(f"{base}/{name}", "w", encoding="utf-8") as f:
        f.write(svg)
    print(name, len(parts))
