"""Write the LoD0 footprints of the 3D BAG buildings near a point as one WKT MULTIPOLYGON in
RD New (EPSG:28992 - the horizontal part of the tile's EPSG:7415), 2D, millimetres."""
import json, sys, math

path, out = sys.argv[1], sys.argv[2]
cx, cy, radius = float(sys.argv[3]), float(sys.argv[4]), float(sys.argv[5])
with open(path, encoding="utf-8") as f:
    cj = json.load(f)
sx, sy, _ = cj["transform"]["scale"]
tx, ty, _ = cj["transform"]["translate"]
V = cj["vertices"]

def xy(i):
    v = V[i]
    return (round(v[0] * sx + tx, 3), round(v[1] * sy + ty, 3))

def ring_wkt(ring):
    pts = [xy(i) for i in ring]
    if pts[0] != pts[-1]:
        pts.append(pts[0])  # CityJSON rings are implicitly closed; WKT's are explicit
    return "(" + ", ".join(f"{x:.3f} {y:.3f}" for x, y in pts) + ")"

polys, ids = [], []
for oid, o in cj["CityObjects"].items():
    if o["type"] != "Building":
        continue
    g = next((g for g in o.get("geometry", []) if str(g.get("lod")) == "0"), None)
    if g is None:
        continue
    for surface in g["boundaries"]:          # MultiSurface: one surface per footprint part
        outer = [xy(i) for i in surface[0]]
        mx = sum(p[0] for p in outer) / len(outer)
        my = sum(p[1] for p in outer) / len(outer)
        if math.hypot(mx - cx, my - cy) > radius:
            continue
        polys.append("(" + ", ".join(ring_wkt(r) for r in surface) + ")")
        ids.append(oid)

wkt = "MULTIPOLYGON (" + ", ".join(polys) + ")"
with open(out, "w", encoding="utf-8") as f:
    f.write(wkt)
print(len(polys), "footprints from", len(set(ids)), "buildings,", len(wkt), "characters")
print("first:", ids[:3])
