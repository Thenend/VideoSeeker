"""Monta la carpeta para «Store listings > Import listings» de Partner Center, con los seis idiomas.

Uso: python rellenar-ficha.py <listingData exportado.csv> <carpeta de capturas> <carpeta de salida>
  - El .csv tiene que ser una exportación («Export listings») del MISMO envío, con los seis idiomas ya añadidos:
    Partner Center rechaza entero un archivo hecho a mano o de otro envío.
  - Capturas: out\\capturas-tienda, con una subcarpeta por idioma (es, en, fr, de, it, pt-BR) y los PNG en orden.
  - Salida: la carpeta se llama como se quiera, pero en el CSV las rutas de imagen empiezan por su nombre
    (ficha-store/es/1.png); con solo es/1.png Partner Center dice «The value you provided is not valid».
Se importa eligiendo la carpeta entera, no solo el CSV. Textos en textos.json (mismo directorio).
"""
import csv
import json
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
LANGS = {'es': 'es-es', 'en': 'en-us', 'fr': 'fr-fr', 'de': 'de-de', 'it': 'it-it', 'pt-BR': 'pt-br'}


def main(src, shots, out):
    texts = json.load(open(os.path.join(HERE, 'textos.json'), encoding='utf-8'))
    name = os.path.basename(os.path.normpath(out))
    rows = list(csv.reader(open(src, encoding='utf-8-sig', newline='')))
    header = [h.strip().lower() for h in rows[0]]
    missing = [col for col in LANGS.values() if col not in header]
    if missing:
        sys.exit(f'Al CSV le faltan idiomas: {", ".join(missing)}. Añádelos en Partner Center y vuelve a exportar.')

    if os.path.exists(out):
        shutil.rmtree(out)
    os.makedirs(os.path.join(out, 'logotipos'))
    for logo in ['caja-2160x2160.png', 'poster-1440x2160.png', 'logo-300.png']:
        shutil.copy(os.path.join(HERE, logo), os.path.join(out, 'logotipos', logo))
    count = {}
    for folder in LANGS:
        pngs = sorted(f for f in os.listdir(os.path.join(shots, folder)) if f.lower().endswith('.png'))
        os.makedirs(os.path.join(out, folder))
        for i, png in enumerate(pngs, 1):
            shutil.copy(os.path.join(shots, folder, png), os.path.join(out, folder, f'{i}.png'))
        count[folder] = len(pngs)

    def value(folder, field):
        t = texts[folder]
        if field in ('ShortDescription', 'Description'):
            return t[field]
        if field == 'ReleaseNotes':
            return t.get('ReleaseNotes', '')
        for prefix, key in (('DesktopScreenshotCaption', 'Captions'), ('Feature', 'Features'), ('SearchTerm', 'SearchTerms')):
            if field.startswith(prefix) and field[len(prefix):].isdigit():
                n = int(field[len(prefix):])
                return t[key][n - 1] if n <= len(t[key]) else ''
        if field.startswith('DesktopScreenshot') and field[17:].isdigit():
            n = int(field[17:])
            return f'{name}/{folder}/{n}.png' if n <= count[folder] else ''
        if field == 'StoreLogo1080x1080':
            return f'{name}/logotipos/caja-2160x2160.png'
        if field == 'StoreLogo300x300':
            return f'{name}/logotipos/logo-300.png'
        if field == 'StoreLogo720x1080':
            return f'{name}/logotipos/poster-1440x2160.png'
        return None  # el resto (título, notas, otras imágenes) se queda como venía

    filled = set()
    for row in rows[1:]:
        if not row or not row[0]:
            continue
        for folder, col in LANGS.items():
            v = value(folder, row[0])
            if v is not None:
                row[header.index(col)] = v
                filled.add(row[0])
    with open(os.path.join(out, 'listingData.csv'), 'w', encoding='utf-8-sig', newline='') as f:
        csv.writer(f, lineterminator='\r\n').writerows(rows)
    print(f'Carpeta lista: {out}. Capturas por idioma: {count}. Campos rellenados: {len(filled)}')
    for field in ('ShortDescription', 'Description', 'Feature1', 'SearchTerm1', 'DesktopScreenshot1', 'StoreLogo1080x1080'):
        if field not in filled:
            print(f'Ojo: el CSV no tiene el campo {field}')


if __name__ == '__main__':
    if len(sys.argv) != 4:
        sys.exit(__doc__)
    main(*sys.argv[1:])
