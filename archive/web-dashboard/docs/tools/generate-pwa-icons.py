"""Génère les icônes PWA (lettre G sur fond #1b6ec2, aligné sur le thème Bootstrap)."""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

BG = (27, 110, 194)
FG = (255, 255, 255)
ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "src" / "App.Core" / "wwwroot" / "icons"


def font_path() -> Path:
    fonts = Path(r"C:\Windows\Fonts")
    for name in ("segoeuib.ttf", "arialbd.ttf", "arial.ttf", "calibrib.ttf"):
        candidate = fonts / name
        if candidate.is_file():
            return candidate
    raise FileNotFoundError("Aucune police TrueType système trouvée.")


def draw_g(size: int, margin_ratio: float) -> Image.Image:
    img = Image.new("RGB", (size, size), BG)
    draw = ImageDraw.Draw(img)
    inner = size * (1 - 2 * margin_ratio)
    font = ImageFont.truetype(str(font_path()), int(inner * 0.92))
    bbox = draw.textbbox((0, 0), "G", font=font)
    tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
    x = (size - tw) / 2 - bbox[0]
    y = (size - th) / 2 - bbox[1] - size * 0.02
    draw.text((x, y), "G", font=font, fill=FG)
    return img


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    any_512 = draw_g(512, 0.14)
    maskable_512 = draw_g(512, 0.22)
    any_512.save(OUT / "icon-512.png", "PNG")
    maskable_512.save(OUT / "icon-512-maskable.png", "PNG")
    any_512.resize((192, 192), Image.Resampling.LANCZOS).save(OUT / "icon-192.png", "PNG")
    any_512.resize((180, 180), Image.Resampling.LANCZOS).save(OUT / "icon-180.png", "PNG")
    any_512.resize((32, 32), Image.Resampling.LANCZOS).save(
        ROOT / "src" / "App.Core" / "wwwroot" / "favicon.png", "PNG"
    )
    print(f"Icônes écrites dans {OUT}")


if __name__ == "__main__":
    main()
