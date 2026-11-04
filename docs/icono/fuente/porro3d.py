"""Tiny flat-shaded 3D renderer for a Porro binocular, output as SVG.
Usage: python3 porro3d.py OUTDIR
"""
import math, os, sys

INK = "#16181C"
BODY = ["#2A2F37", "#3A414C", "#4D5562"]          # dark, mid, lit
METAL = ["#5F656E", "#8A9099", "#B7BDC6"]
GLASS_DK = "#14243A"
GLASS = "#2F5E9E"
ACCENT = "#4C9BFF"
WHITE = "#E6E8EB"

def v(x, y, z): return (x, y, z)
def add(a, b): return (a[0]+b[0], a[1]+b[1], a[2]+b[2])
def sub(a, b): return (a[0]-b[0], a[1]-b[1], a[2]-b[2])
def mul(a, k): return (a[0]*k, a[1]*k, a[2]*k)
def dot(a, b): return a[0]*b[0]+a[1]*b[1]+a[2]*b[2]
def cross(a, b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def norm(a):
    l = math.sqrt(dot(a, a)); return (a[0]/l, a[1]/l, a[2]/l)

class View:
    def __init__(self, yaw, pitch, roll=0, dist=None):
        self.yaw, self.pitch, self.roll = map(math.radians, (yaw, pitch, roll))
        self.dist = dist
    def rot(self, p):
        x, y, z = p
        c, s = math.cos(self.yaw), math.sin(self.yaw)
        x, z = c*x + s*z, -s*x + c*z
        c, s = math.cos(self.pitch), math.sin(self.pitch)
        y, z = c*y - s*z, s*y + c*z
        c, s = math.cos(self.roll), math.sin(self.roll)
        x, y = c*x - s*y, s*x + c*y
        return (x, y, z)

LIGHT = norm((-0.45, 0.75, 0.55))   # in view space

def tone(n_view, pal, ramp=(0.30, 0.72)):
    l = dot(n_view, LIGHT)
    return pal[0] if l < ramp[0] else pal[1] if l < ramp[1] else pal[2]

def circle_pts(c, u, w, r, n=48, a0=0):
    return [add(c, add(mul(u, r*math.cos(a0 + 2*math.pi*i/n)), mul(w, r*math.sin(a0 + 2*math.pi*i/n)))) for i in range(n)]

def clip(poly, d, k, keep_ge=True):
    """Sutherland-Hodgman on 2D poly against half-plane dot(p,d) >= k (or <=)."""
    out = []
    f = (lambda p: dot2(p, d) - k) if keep_ge else (lambda p: k - dot2(p, d))
    for i in range(len(poly)):
        a, b = poly[i-1], poly[i]
        fa, fb = f(a), f(b)
        if fb >= 0:
            if fa < 0: out.append(lerp2(a, b, fa/(fa-fb)))
            out.append(b)
        elif fa >= 0:
            out.append(lerp2(a, b, fa/(fa-fb)))
    return out
def dot2(a, b): return a[0]*b[0] + a[1]*b[1]
def lerp2(a, b, t): return (a[0]+(b[0]-a[0])*t, a[1]+(b[1]-a[1])*t)

class Prism:
    """Extrusion of a convex 3D planar profile along vector d."""
    def __init__(self, front, d, pal, outline=8, cap_pal=None, front_extra=None, stripes=False):
        self.front, self.d, self.pal = front, d, pal
        self.outline, self.cap_pal = outline, cap_pal or pal
        self.front_extra = front_extra or []   # list of (pts3d, color)
        self.stripes = stripes
    def center(self):
        n = len(self.front)
        c = mul(tuple(map(sum, zip(*self.front))), 1/n)
        return add(c, mul(self.d, 0.5))

def hull(pts):
    pts = sorted(set((round(x, 2), round(y, 2)) for x, y in pts))
    if len(pts) < 3: return pts
    def half(ps):
        h = []
        for p in ps:
            while len(h) >= 2 and ((h[-1][0]-h[-2][0])*(p[1]-h[-2][1]) - (h[-1][1]-h[-2][1])*(p[0]-h[-2][0])) <= 0:
                h.pop()
            h.append(p)
        return h
    lo, up = half(pts), half(pts[::-1])
    return lo[:-1] + up[:-1]

def render(prims, view, size=256, margin=12, extras_after=None):
    # project everything to find bbox
    zs = [view.rot(q)[2] for pr in prims for p in pr.front for q in (p, add(p, pr.d))]
    zc = (max(zs) + min(zs)) / 2
    cam = (0, 0, zc + view.dist) if view.dist else None
    def P(p):
        r = view.rot(p)
        if cam:
            f = view.dist / (cam[2] - r[2]); return (r[0]*f, -r[1]*f)
        return (r[0], -r[1])
    def vis(nv, pt):
        if not cam: return nv[2] > 1e-3
        pv = view.rot(pt); return dot(nv, sub(cam, pv)) > 1e-3
    allp = []
    for pr in prims:
        for p in pr.front:
            allp += [P(p), P(add(p, pr.d))]
    xs, ys = [p[0] for p in allp], [p[1] for p in allp]
    w, h = max(xs)-min(xs), max(ys)-min(ys)
    k = (size - 2*margin - 8) / max(w, h)
    ox = size/2 - (min(xs)+max(xs))/2*k
    oy = size/2 - (min(ys)+max(ys))/2*k
    S = lambda p: (P(p)[0]*k + ox, P(p)[1]*k + oy)
    def path(pts):
        return "M" + " L".join(f"{x:.1f} {y:.1f}" for x, y in pts) + " Z"
    # painter: sort by view depth (z toward viewer is +)
    order = sorted(prims, key=lambda pr: (-math.dist(view.rot(pr.center()), cam)) if cam else view.rot(pr.center())[2])
    out = []
    for pr in order:
        F = pr.front; B = [add(p, pr.d) for p in F]
        sil = hull([S(p) for p in F + B])
        out.append(f'<path d="{path(sil)}" fill="{INK}" stroke="{INK}" stroke-width="{pr.outline}" stroke-linejoin="round"/>')
        n = len(F)
        # profile normal (front cap)
        cn = norm(cross(sub(F[1], F[0]), sub(F[2], F[0])))
        if dot(cn, pr.d) > 0: cn = mul(cn, -1)   # outward at front
        cap_n = view.rot(cn)
        cen = mul(tuple(map(sum, zip(*F))), 1/n)
        faces = []
        for i in range(n):
            a, b = F[i], F[(i+1) % n]
            mid = mul(add(a, b), 0.5)
            en = norm(sub(mid, cen))
            en = norm(sub(en, mul(cn, dot(en, cn))))
            nv = view.rot(en)
            if vis(nv, add(mid, mul(pr.d, 0.5))):
                col = tone(nv, pr.pal)
                if pr.stripes: col = pr.pal[0] if i % 2 else col
                faces.append((nv[2], [S(a), S(b), S(add(b, pr.d)), S(add(a, pr.d))], col))
        for _, q, col in faces:
            out.append(f'<path d="{path(q)}" fill="{col}" stroke="{col}" stroke-width="1.2" stroke-linejoin="round"/>')
        if vis(cap_n, cen):
            out.append(f'<path d="{path([S(p) for p in F])}" fill="{tone(cap_n, pr.cap_pal)}"/>')
            for pts, col in pr.front_extra:
                out.append(f'<path d="{path([S(p) for p in pts])}" fill="{col}"/>')
        elif vis(view.rot(mul(cn, -1)), add(cen, pr.d)):
            out.append(f'<path d="{path([S(p) for p in B])}" fill="{tone(view.rot(mul(cn, -1)), pr.cap_pal)}"/>')
    return "\n".join(out)

def capsule(c1, r1, c2, r2, z, n=40):
    pts = circle_pts(v(c1[0], c1[1], z), (1, 0, 0), (0, 1, 0), r1, n) + circle_pts(v(c2[0], c2[1], z), (1, 0, 0), (0, 1, 0), r2, n)
    h = hull([(p[0], p[1]) for p in pts])
    return [v(x, y, z) for x, y in h]

def rrect(x0, y0, x1, y1, r, z, n=6):
    pts = []
    for cx, cy, a0 in ((x1-r, y1-r, 0), (x0+r, y1-r, 90), (x0+r, y0+r, 180), (x1-r, y0+r, 270)):
        for i in range(n+1):
            a = math.radians(a0 + 90*i/n)
            pts.append(v(cx + r*math.cos(a), cy + r*math.sin(a), z))
    return pts

def lens_extra(c, R, refl_dir=(-0.6, 0.8)):
    """Front decoration for an objective facing +z: metal rim is the cap; glass + straight reflection band."""
    n = 56
    glass2 = [(R*0.80*math.cos(2*math.pi*i/n), R*0.80*math.sin(2*math.pi*i/n)) for i in range(n)]
    d = refl_dir; l = math.hypot(*d); d = (d[0]/l, d[1]/l)
    band = clip(clip(glass2, d, R*0.18), d, R*0.46, keep_ge=False)
    thin = clip(clip(glass2, d, R*0.56), d, R*0.66, keep_ge=False)
    z = c[2] + 0.01
    to3 = lambda pts: [v(c[0]+x, c[1]+y, z) for x, y in pts]
    ex = [(to3(glass2), GLASS_DK), (to3(band), GLASS)]
    if thin: ex.append((to3(thin), WHITE))
    return ex

def model(bridge_low=-8, lens_r=38):
    prims = []
    for s in (-1, 1):
        oc, orr = (s*64, -30), 44      # objective lobe
        ec, er = (s*36, 36), 32        # eyepiece lobe
        # prism housing
        prims.append(Prism(capsule(oc, orr, ec, er, 0), (0, 0, -54), BODY))
        # objective barrel with lens
        front = circle_pts(v(oc[0], oc[1], 22), (1, 0, 0), (0, 1, 0), lens_r, 48)
        prims.append(Prism(front, (0, 0, -22), BODY, cap_pal=METAL, front_extra=lens_extra(v(oc[0], oc[1], 22), lens_r)))
        # eyepiece
        prims.append(Prism(circle_pts(v(ec[0], ec[1], -54), (1, 0, 0), (0, 1, 0), 21, 32), (0, 0, -30), BODY, cap_pal=BODY))
    # bridge plate between the housings (joins both halves)
    prims.append(Prism(rrect(-44, bridge_low, 44, 56, 14, -6), (0, 0, -42), BODY))
    # hinge axle
    # focus wheel (axis x), knurled
    prims.append(Prism(circle_pts(v(-15, 58, -26), (0, 1, 0), (0, 0, 1), 14, 24), (30, 0, 0), METAL, outline=7, stripes=True))
    return prims

def svg(body, size=256):
    return f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {size} {size}">\n{body}\n</svg>\n'

if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "out3d"
    os.makedirs(out, exist_ok=True)
    views = {
        "12a-porro-frente": (View(0, 10), dict(bridge_low=-20)),
        "12b-porro-3-4": (View(-28, 24), {}),
        "12c-porro-ladeado": (View(-28, 24, -14), {}),
    }
    for name, (vw, kw) in views.items():
        open(os.path.join(out, name + ".svg"), "w").write(svg(render(model(**kw), vw)))
    print("ok")
