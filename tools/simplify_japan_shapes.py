import json, math

with open('/tmp/japan.geojson', encoding='utf-8') as f:
    data = json.load(f)

# Our game's 46 prefecture names (matches Assets/Resources/Data/prefectures.json)
our_prefs = ["北海道","青森県","岩手県","宮城県","秋田県","山形県","福島県","茨城県","栃木県","群馬県",
"埼玉県","千葉県","東京都","神奈川県","新潟県","長野県","山梨県","静岡県","愛知県","岐阜県",
"石川県","富山県","福井県","三重県","滋賀県","京都府","大阪府","兵庫県","奈良県","和歌山県",
"鳥取県","島根県","岡山県","広島県","山口県","徳島県","香川県","愛媛県","高知県","福岡県",
"佐賀県","長崎県","熊本県","大分県","宮崎県","鹿児島県"]
our_set = set(our_prefs)

def ring_area(ring):
    a = 0.0
    n = len(ring)
    for i in range(n):
        x1,y1 = ring[i]
        x2,y2 = ring[(i+1)%n]
        a += x1*y2 - x2*y1
    return abs(a)/2.0

def perpendicular_distance(pt, a, b):
    (x,y),(x1,y1),(x2,y2) = pt,a,b
    dx, dy = x2-x1, y2-y1
    if dx==0 and dy==0:
        return math.hypot(x-x1, y-y1)
    t = ((x-x1)*dx + (y-y1)*dy) / (dx*dx+dy*dy)
    t = max(0,min(1,t))
    px, py = x1+t*dx, y1+t*dy
    return math.hypot(x-px, y-py)

def rdp(points, epsilon):
    if len(points) < 3:
        return points
    dmax = 0.0
    index = 0
    for i in range(1, len(points)-1):
        d = perpendicular_distance(points[i], points[0], points[-1])
        if d > dmax:
            index = i
            dmax = d
    if dmax > epsilon:
        left = rdp(points[:index+1], epsilon)
        right = rdp(points[index:], epsilon)
        return left[:-1] + right
    else:
        return [points[0], points[-1]]

result = {}
matched = 0
for feature in data['features']:
    name = feature['properties'].get('nam_ja')
    if name not in our_set:
        continue
    geom = feature['geometry']
    polys = geom['coordinates'] if geom['type']=='MultiPolygon' else [geom['coordinates']]
    # polys: list of polygons, each polygon = list of rings, first ring = outer boundary
    best_ring = None
    best_area = -1
    for poly in polys:
        outer = poly[0]
        area = ring_area(outer)
        if area > best_area:
            best_area = area
            best_ring = outer
    simplified = rdp(best_ring, 0.01)
    # dedupe consecutive identical points
    pts = []
    for p in simplified:
        if not pts or (abs(p[0]-pts[-1][0])>1e-9 or abs(p[1]-pts[-1][1])>1e-9):
            pts.append([round(p[0],3), round(p[1],3)])
    result[name] = pts
    matched += 1

print(f"matched {matched}/{len(our_prefs)} prefectures")
missing = our_set - set(result.keys())
print("missing:", missing)

total_points = sum(len(v) for v in result.values())
print("total points:", total_points, "avg:", total_points/len(result))

with open('/tmp/prefecture_shapes.json', 'w', encoding='utf-8') as f:
    json.dump(result, f, ensure_ascii=False)
