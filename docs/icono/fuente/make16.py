import os, sys
exec(open('roof3d.py').read().split('\nif __name__ == "__main__"')[0])
OUT = 'out16'; os.makedirs(OUT, exist_ok=True)
P0 = dict(BODY=["#2A2F37", "#3A414C", "#4D5562"], METAL=["#5F656E", "#8A9099", "#B7BDC6"], GLASS_DK="#14243A", GLASS="#2F5E9E")
P1 = dict(BODY=["#343A44", "#47505D", "#5D6676"], METAL=["#6A717B", "#979EA8", "#C2C8D0"], GLASS_DK="#15284A", GLASS="#3A73C2")
P2 = dict(BODY=["#3D4450", "#535C6B", "#6B7586"], METAL=["#757C86", "#A4ABB5", "#CCD2D9"], GLASS_DK="#162C52", GLASS="#4383D6")
def draw(pal, sep, length, pitch, margin=-2):
    g = globals(); g.update(pal)
    return render(roof(sep=sep, length=length), View(0, pitch, 0, 320), margin=margin)
geo = dict(sep=35, length=122, pitch=38)
for name, pal in (("16a-grande-actual", P0), ("16b-grande-poco-mas-claro", P1), ("16c-grande-algo-mas-claro", P2)):
    open(f'{OUT}/{name}.svg', 'w').write(svg(draw(pal, **geo)))
print("ok")
