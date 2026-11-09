"""Genera los PNG del paquete MSIX (scripts/msix/Assets) y los de la ficha de la Store (scripts/msix/tienda).

Los tamaños que ya tiene el icono de la app (src/VideoSeeker.App/Assets/VideoSeeker.ico) se copian de él, para que
sean idénticos; el resto se dibuja desde los SVG de docs/icono con la misma regla que el .ico: el de 16 px dibujado
píxel a píxel, el de la barra de tareas de 24 a 44 px y el principal en 20 px y desde 48 px.

Uso: python generar-assets.py   (necesita pillow y cairosvg: pip install pillow cairosvg)
"""
import io
import os

import cairosvg
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
SVG = {name: open(os.path.join(ROOT, 'docs', 'icono', f), 'rb').read() for name, f in
       [('16', 'VideoSeeker-16.svg'), ('barra', 'VideoSeeker-barra.svg'), ('main', 'VideoSeeker.svg')]}
ICO = Image.open(os.path.join(ROOT, 'src', 'VideoSeeker.App', 'Assets', 'VideoSeeker.ico'))
FROM_ICO = {}
for size in ICO.info.get('sizes', []):
    ICO.size = size
    FROM_ICO[size[0]] = ICO.convert('RGBA').copy()

BACKGROUND = (0x16, 0x18, 0x1C, 255)  # el fondo de la app


def icon(px):
    """El icono a px × px, transparente."""
    if px in FROM_ICO:
        return FROM_ICO[px].copy()
    if px <= 16:
        name = '16'
    elif 24 <= px <= 44:
        name = 'barra'
    else:
        name = 'main'
    png = cairosvg.svg2png(bytestring=SVG[name], output_width=px, output_height=px)
    return Image.open(io.BytesIO(png)).convert('RGBA')


def canvas(w, h, icon_px, background=(0, 0, 0, 0)):
    """El icono centrado en un lienzo w × h."""
    image = Image.new('RGBA', (w, h), background)
    mark = icon(icon_px)
    image.alpha_composite(mark, ((w - icon_px) // 2, (h - icon_px) // 2))
    return image


def save(image, folder, name):
    os.makedirs(folder, exist_ok=True)
    image.save(os.path.join(folder, name), optimize=True)


assets = os.path.join(HERE, 'Assets')
for f in os.listdir(assets) if os.path.isdir(assets) else []:
    os.remove(os.path.join(assets, f))

SCALES = [100, 125, 150, 200, 400]

# Icono pequeño: lista de Inicio, barra de tareas, Explorador, Aplicaciones predeterminadas. Los targetsize son a
# tamaño exacto y sin placa (unplated), como los del .ico.
for target in [16, 20, 24, 30, 32, 36, 40, 44, 48, 60, 64, 72, 80, 96, 256]:
    for suffix in ['', '_altform-unplated', '_altform-lightunplated']:
        save(icon(target), assets, f'Square44x44Logo.targetsize-{target}{suffix}.png')
for scale in SCALES:
    px = round(44 * scale / 100)
    save(icon(px), assets, f'Square44x44Logo.scale-{scale}.png')

# Logo del paquete (instalador, Store en el PC).
for scale in SCALES:
    px = round(50 * scale / 100)
    save(icon(px), assets, f'StoreLogo.scale-{scale}.png')

# Mosaicos (Inicio de Windows 10): el icono a algo más de la mitad del alto, sobre transparente.
for scale in SCALES:
    side = round(150 * scale / 100)
    save(canvas(side, side, round(side * 0.6)), assets, f'Square150x150Logo.scale-{scale}.png')
    w, h = round(310 * scale / 100), round(150 * scale / 100)
    save(canvas(w, h, round(h * 0.6)), assets, f'Wide310x150Logo.scale-{scale}.png')

# Ficha de la Store: logo 1:1 (300 px, transparente) e imagen cuadrada grande con el fondo de la app.
tienda = os.path.join(HERE, 'tienda')
save(icon(300), tienda, 'logo-300.png')
save(canvas(1080, 1080, 720, BACKGROUND), tienda, 'logo-1080-fondo.png')
print('Assets:', len(os.listdir(assets)), 'archivos')
