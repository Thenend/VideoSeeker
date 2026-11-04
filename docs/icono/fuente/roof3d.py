import sys, os
src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'porro3d.py')).read().split('if __name__')[0]
exec(src)

def roof(sep=39, tube_r=29, length=100):
    prims = []
    for s in (-1, 1):
        cx = s*sep
        zf = 52
        # objective bell with lens
        prims.append(Prism(circle_pts(v(cx, 0, zf), (1,0,0), (0,1,0), tube_r+4, 48), (0,0,-24), BODY, cap_pal=METAL,
                           front_extra=lens_extra(v(cx, 0, zf), tube_r+4)))
        # main tube
        prims.append(Prism(circle_pts(v(cx, 0, zf-24), (1,0,0), (0,1,0), tube_r, 48), (0,0,-length), BODY))
        # eyepiece + cup
        ze = zf-24-length
        prims.append(Prism(circle_pts(v(cx, 0, ze), (1,0,0), (0,1,0), tube_r*0.72, 40), (0,0,-26), BODY, cap_pal=BODY))
    # bridge between tubes
    bw = min(sep, sep - tube_r + 14)
    prims.append(Prism(rrect(-bw, -10, bw, 14, 8, -2), (0,0,-80), BODY))
    # focus wheel on top of the bridge, near the eyepieces
    prims.append(Prism(circle_pts(v(-16, 28, -58), (0,1,0), (0,0,1), 16, 24), (32,0,0), METAL, outline=7, stripes=True))
    return prims

if __name__ == "__main__":
    out = sys.argv[1]; os.makedirs(out, exist_ok=True)
    views = dict(a[0] for a in [])
    for spec in sys.argv[2:]:
        parts = spec.split(':'); name = parts[0]; yaw, pitch, roll = map(float, parts[1:4])
        dist = float(parts[4]) if len(parts) > 4 else None
        open(os.path.join(out, name + '.svg'), 'w').write(svg(render(roof(), View(yaw, pitch, roll, dist))))
