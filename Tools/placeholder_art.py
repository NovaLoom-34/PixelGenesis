"""PixelGenesis geçici (placeholder) sanat üreticisi.
Data/*.json dosyalarından her tür, bina, feature, güç, trait ve statü için
EK C'deki boyut ve indeks kurallarına uygun geçici sprite'lar üretir.
Kullanım: python Tools/placeholder_art.py  (depo kökünden)
Çıktı:   Art/Placeholder/{units,buildings,features,icons}/*.png + *.json, preview.png, asset_list.csv
"""
import json, os, csv, hashlib, math
from PIL import Image, ImageDraw

def _norm(fn):
    def w(self, xy, *a, **k):
        if len(xy) == 4 and not isinstance(xy[0], tuple):
            x0, y0, x1, y1 = xy; xy = [min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1)]
        return fn(self, xy, *a, **k)
    return w
for _n in ("rectangle", "ellipse", "rounded_rectangle"):  # küçük sprite'larda ters koordinatlara karşı
    setattr(ImageDraw.ImageDraw, _n, _norm(getattr(ImageDraw.ImageDraw, _n)))

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "Data")
OUT = os.path.join(ROOT, "Art", "Placeholder")
def load(n): return json.load(open(os.path.join(DATA, n + ".json"), encoding="utf-8"))["items"]
def h(s, n): return int(hashlib.md5(s.encode()).hexdigest(), 16) % n

# ---- İndeks kodlaması (EK C 4): gri = indeks * 28, alfa 0 = boş
OUTLINE, SHADOW, BASE, LIGHT, CLOTH_D, CLOTH, ACCENT, SHINE = range(1, 9)
def g(i): return (i * 28, i * 28, i * 28, 255)

# ---- Animasyon tanımı (EK C 5)
ANIMS = [("idle", 2, 400), ("walk", 4, 120), ("attack", 3, 100), ("swim", 2, 250), ("sleep", 1, 1000), ("death", 3, 150)]

BUG = {"bee", "ant", "beetle", "scorpion", "crystal_beetle", "void_moth", "butterfly", "caterpillar", "snail", "mosquito_swarm", "crab", "clock_crab", "bone_crawler"}
SERPENT = {"snake", "sandworm"}
BLOB = {"slime", "flesh_mound", "devourer", "candy_golem", "gummy_bear", "shroomling"}
BIPED_CATS = {"civ", "undead", "magic", "special", "elemental"}

def body_plan(sp):
    k = sp["id"][3:]
    if k in BLOB: return "blob"
    if k in SERPENT: return "serpent"
    if k in BUG: return "bug"
    if sp["habitat"] == "water" or k in ("fish", "piranha", "kraken"): return "fish"
    if sp["habitat"] == "air" and sp["category"] not in BIPED_CATS: return "bird"
    if k in ("dragon", "zombie_dragon"): return "dragon"
    if sp["category"] in BIPED_CATS or k in ("monkey", "penguin", "walking_tree", "zombie_beast") and k != "zombie_beast": return "biped"
    return "quad"

SIZES = {  # plan -> boy(1..5) -> (w,h)
 "biped": {1: (3, 5), 2: (4, 7), 3: (5, 8), 4: (7, 10), 5: (12, 14)},
 "quad":  {1: (4, 3), 2: (6, 4), 3: (8, 5), 4: (10, 7), 5: (16, 10)},
 "bird":  {1: (4, 3), 2: (6, 4), 3: (8, 5), 4: (10, 6), 5: (18, 10)},
 "bug":   {1: (3, 2), 2: (4, 3), 3: (5, 3), 4: (7, 4), 5: (9, 5)},
 "fish":  {1: (3, 2), 2: (5, 3), 3: (7, 4), 4: (10, 5), 5: (16, 9)},
 "serpent": {1: (5, 3), 2: (6, 3), 3: (8, 4), 4: (12, 5), 5: (20, 6)},
 "blob":  {1: (3, 3), 2: (4, 4), 3: (6, 5), 4: (8, 7), 5: (12, 10)},
 "dragon": {5: (20, 14), 4: (16, 12), 3: (12, 9), 2: (8, 6), 1: (6, 4)},
}

def draw_frame(plan, w, h, anim, f, seed):
    """EK C 5: her tür siluette tam bir ayırt edici çıkıntı taşır (tür id'sinden deterministik),
    idle nefes alır, yürüyüş 4 kare bacak dönüşümü + 1 px basınç kullanır, ışık sol üstten gelir."""
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    fid = int(hashlib.md5(seed.encode()).hexdigest(), 16)   # tür özellikleri: bit başına deterministik
    breathe = 1 if (anim == "idle" and f == 1) else 0       # nefes: kafa/gövde 1 px iner
    hop = 1 if (anim == "walk" and f % 2 == 1) else 0       # adım basınç: gövde 1 px iner
    if anim == "death":
        rot = f  # yere yatma: gövdeyi basıklaştır
        hh = max(2, h - rot * (h // 3)); d.rectangle([0, h - hh, w - 1, h - 1], fill=g(SHADOW))
        d.rectangle([1, h - hh + 1, w - 2, h - 2], fill=g(BASE))
        d.line([1, h - hh + 1, 1, h - 2], fill=g(LIGHT)); return im
    if anim == "sleep":
        top = h - max(2, h // 2)
        d.rectangle([0, top, w - 1, h - 1], fill=g(BASE)); d.line([0, top, w - 1, top], fill=g(LIGHT))
        d.point((w - 2, top + 1), fill=g(SHADOW))            # kapalı göz çizgisi
        if plan in ("biped", "quad", "dragon") and w >= 5: d.point((0, h - 2), fill=g(SHADOW))  # kıvrık kuyruk
        return im

    if plan == "biped":
        head = max(2, (w + 1) // 2); hx = (w - head) // 2
        gear = fid % 4 if h >= 6 else 0                      # 0 saç, 1 kulak, 2 boynuz, 3 kukuleta
        if gear in (2, 3): hb = 1                            # başı 1 px aşağı kaydır, tepede çıkıntı
        else: hb = 0
        hb += breathe
        body_top, body_bot = head + hb, h - 3                # gövde alt satırı (bacaklar h-2..h-1)
        d.rectangle([0, body_top, w - 1, body_bot], fill=g(CLOTH))                     # gövde (krallık kumaşı)
        d.line([w - 1, body_top, w - 1, body_bot], fill=g(CLOTH_D))                    # gölge yan
        d.line([0, body_top, w - 1, body_top], fill=g(CLOTH_D) if breathe else g(SHADOW))  # omuz/kemer
        if body_bot - body_top >= 2: d.line([0, body_bot, w - 1, body_bot], fill=g(SHADOW))
        if w >= 5: d.point((0, body_top + 1), fill=g(CLOTH_D))                         # kol
        legs = [(1, w - 2), (0, w - 1), (1, w - 2), (2, w - 3)][f % 4] if anim == "walk" else (1, w - 2)
        d.line([legs[0], h - 2, legs[0], h - 1], fill=g(SHADOW)); d.line([legs[1], h - 2, legs[1], h - 1], fill=g(SHADOW))
        hy = hb
        d.rectangle([hx, hy, hx + head - 1, head - 1 + hy], fill=g(LIGHT if gear != 3 else CLOTH_D))  # baş/kukuleta
        d.line([hx + head - 1, hy, hx + head - 1, head - 1 + hy], fill=g(BASE))        # yüz gölgesi
        ex = hx + head - 1 if head <= 2 else hx + head - 2
        d.point((ex, min(head - 1, 1) + hy), fill=g(ACCENT))                           # göz
        if gear == 1 and w > head: d.point((hx - 1, hy), fill=g(LIGHT)); d.point((hx + head, hy), fill=g(LIGHT))  # kulaklar
        if gear == 2: d.point((hx, 0), fill=g(LIGHT)); d.point((hx + head - 1, 0), fill=g(LIGHT))                 # boynuz
        if gear == 3: d.point((hx + head // 2, 0), fill=g(CLOTH))                                                 # tepelik
        if w >= 5 and (fid >> 7) & 1: d.point((0, body_top + 2), fill=g(SHADOW))       # kuyruk
        if anim == "attack":
            if f == 0: d.line([w - 1, hy, w - 1, max(0, hy - 1)], fill=g(SHINE))       # hazırlık: yukarı
            elif f == 1: d.line([hx + head, body_top, w - 1, body_top], fill=g(SHINE)) # vuruş: ileri
            else: d.line([w - 1, body_top, w - 1, body_top + 2], fill=g(SHINE))        # toparlanma: aşağı
        if anim == "swim": ImageDraw.Draw(im).rectangle([0, h - 3, w - 1, h - 1], fill=(0, 0, 0, 0))

    elif plan in ("quad", "dragon"):
        bh = max(2, h - 2)
        htop = 1 if h >= 5 else 0                     # çıkıntıya (kulak/boynuz) yer açmak için baş 1 px aşağıda
        d.rectangle([0, 1 + hop, w - 3, bh], fill=g(BASE)); d.line([0, 1 + hop, w - 3, 1 + hop], fill=g(LIGHT))
        d.line([0, bh, w - 3, bh], fill=g(SHADOW))                                     # karın gölgesi
        d.rectangle([w - 3, htop + hop, w - 1, htop + 2 + hop], fill=g(LIGHT))
        d.point((w - 1, htop + 1 + hop), fill=g(ACCENT))
        d.point((w - 1, htop + hop), fill=g(BASE))                                     # burun
        if (fid >> 3) & 1 and htop: d.point((w - 3, 0), fill=g(LIGHT))                 # kulak
        if (fid >> 5) & 1 and htop: d.point((w - 2, 0), fill=g(LIGHT))                 # boynuz
        if (fid >> 9) & 1 and w >= 6:                                                  # yele
            for x in range(1, w - 3): d.point((x, htop + hop), fill=g(CLOTH))
        d.line([0, bh - 1, 0, bh - 1], fill=g(SHADOW))                                 # kuyruk kökü
        step = f % 4 if anim == "walk" else 0
        leg_x = [1, w // 3, w - 5, w - 4] if w >= 9 else [1, w - 4]
        for i, x in enumerate(leg_x):
            off = (1 if (step + i) % 2 else 0)
            d.line([min(w - 1, x + off), bh + 1, min(w - 1, x + off), h - 1], fill=g(SHADOW))
        if plan == "dragon":
            wing = 1 if f % 2 else 0
            d.polygon([(w // 3, 1), (w // 2, 1 + wing), (2 * w // 3, 1)], fill=g(CLOTH))
            d.point((w - 2, htop - 1 if htop else 0), fill=g(CLOTH))                   # boynuz

    elif plan == "bird":
        up = f % 2 if anim in ("walk", "idle", "swim") else 0
        d.rectangle([w // 3, h // 3 + breathe, 2 * w // 3, h - 1], fill=g(BASE))
        d.line([w // 3, h // 3 + breathe, 2 * w // 3, h // 3 + breathe], fill=g(LIGHT))
        d.rectangle([2 * w // 3, 0 + breathe, w - 2, h // 3 + breathe], fill=g(LIGHT)) # baş
        d.point((w - 1, 1 + breathe), fill=g(ACCENT))                                  # gaga
        d.point((w - 2, 1 + breathe), fill=g(ACCENT))
        if (fid >> 4) & 1: d.point((2 * w // 3, 0 + breathe), fill=g(CLOTH))           # ibik
        wy = 0 if up else h // 3
        d.polygon([(w // 3, h // 3 + breathe), (0, wy), (2 * w // 3, h // 3 + breathe)], fill=g(LIGHT) if not up else g(SHADOW))
        if anim != "swim": d.line([w // 2, h - 1, w // 2, h - 1], fill=g(SHADOW))      # ayak

    elif plan == "fish":
        d.ellipse([1, 0, w - 1, h - 1], fill=g(BASE)); d.line([2, 0, w - 2, 0], fill=g(LIGHT))
        d.line([2, h - 1, w - 2, h - 1], fill=g(SHADOW))
        t = h // 2 + (1 if f % 2 else 0) - 1                                           # kuyruk yüzgeci (solda)
        d.polygon([(1, t), (0, max(0, t - 1)), (0, min(h - 1, t + 1))], fill=g(SHADOW))
        if h >= 4: d.line([w // 2, 0, w // 2, 0], fill=g(CLOTH))                       # sırt yüzgeci
        d.point((w - 2, h // 2 - 1 if h > 2 else 0), fill=g(ACCENT))

    elif plan == "bug":
        ty = 1 if h >= 3 else 0                                        # antenler için gövdeyi 1 px aşağı çek
        seg = max(1, w // 3)
        d.rectangle([0, ty, seg - 1, h - 1], fill=g(SHADOW))                           # abdomen
        d.rectangle([seg, ty, 2 * seg - 1, h - 1], fill=g(BASE))                       # thorax
        d.rectangle([2 * seg, ty, w - 1, h - 1], fill=g(LIGHT))                        # baş
        d.point((w - 1, ty), fill=g(ACCENT))                                           # göz
        if ty: d.point((w - 2, 0), fill=g(LIGHT)); d.point((w - 1, 0), fill=g(LIGHT))  # antenler
        legx = [1, seg + 1, 2 * seg + 1]
        for i, x in enumerate(legx):
            off = (1 if (f + i) % 2 else 0) if anim == "walk" else 0
            if h >= 3 and x + off < w: d.point((x + off, h - 1), fill=g(SHADOW))
        if (fid >> 6) & 1 and seg >= 2: d.line([0, h // 2, seg - 1, h // 2], fill=g(CLOTH))  # çizgili abdomen

    elif plan == "serpent":
        amp = max(0, (h - 2) / 2)
        for x in range(w - 2):
            y = int((h - 2) / 2 + math.sin((x + f * 2) / 2.0) * amp)
            d.point((x, y), fill=g(BASE)); d.point((x, min(h - 1, y + 1)), fill=g(SHADOW))
            if x < w - 3 and (x + f) % 3 == 0: d.point((x, y), fill=g(LIGHT))          # parlak omurga
        hx_, hy_ = w - 2, int((h - 2) / 2 + math.sin((w - 2 + f * 2) / 2.0) * amp)
        d.rectangle([hx_, hy_, w - 1, hy_ + 1], fill=g(LIGHT))                         # kafa
        d.point((w - 1, hy_), fill=g(ACCENT))                                          # göz
        if f % 2 == 0: d.point((w - 1, hy_ + 1), fill=g(ACCENT))                       # dil

    elif plan == "blob":
        squish = 1 if f % 2 else 0
        d.ellipse([0, squish, w - 1, h - 1], fill=g(BASE))
        d.line([1, 1 + squish, w // 2, 1 + squish], fill=g(SHINE))
        d.line([0, h // 2, 0, h - 1], fill=g(SHADOW))                                  # sol gölge kenarı
        d.point((w // 2, h // 3 + squish), fill=g(ACCENT))
        if w >= 5: d.point((w // 2 + 2, h // 3 + squish), fill=g(ACCENT))              # ikinci göz
        d.point((w - 2, h - 2), fill=g(SHADOW))                                        # damla
    return im

def unit_sheet(sp):
    plan = body_plan(sp); size = max(1, min(5, sp["stats"]["size"] or 2))
    w, h = SIZES[plan][size]
    cols = max(n for _, n, _ in ANIMS)
    sheet = Image.new("RGBA", (w * cols, h * len(ANIMS)), (0, 0, 0, 0))
    frames, tags, idx = [], [], 0
    for r, (name, n, dur) in enumerate(ANIMS):
        tags.append({"name": name, "from": idx, "to": idx + n - 1, "direction": "forward"})
        for f in range(n):
            fr = draw_frame(plan, w, h, name, f, sp["id"])
            sheet.paste(fr, (f * w, r * h), fr)
            frames.append({"filename": f"{name}_{f}", "frame": {"x": f * w, "y": r * h, "w": w, "h": h}, "duration": dur}); idx += 1
    meta = {"frames": frames, "meta": {"frameTags": tags, "size": {"w": sheet.width, "h": sheet.height}, "plan": plan, "placeholder": True}}
    return sheet, meta, (w, h), plan

# ---- Binalar (EK C 6): 1 hücre = 3x3 tile. Tile başına 5 px — birim ve ağaçlarla aynı piksel yoğunluğu (DECISIONS #88);
# BuildingRenderer sprite'ı kaplama genişliğine ölçeklediği için yalnız en-boy oranı önemli.
TILE_PX = 5
CELL = 3 * TILE_PX
STYLES = ("human", "elf", "dwarf", "orc", "beast", "insect")
# bina indeksleri (EK C 4.1): kontur, gölge/kiriş, duvar, açık duvar, çatı gölgesi, çatı (krallık), çerçeve, ışık
OL, SH, WA, LI, RS, RF, TR, LT = range(1, 9)
TALL = {"watchtower": 2.3, "windmill": 1.7}  # hücre yüksekliği katı; çatı payı bir kuleye yetmez

class Pix:
    def __init__(s, W, H, fh, seed):
        s.w, s.h, s.fh = W, H, fh
        s.im = Image.new("RGBA", (W, H), (0, 0, 0, 0)); s.px = s.im.load(); s.d = ImageDraw.Draw(s.im)
        s.seed = h(seed, 1 << 20)
    def ok(s, x, y): return 0 <= x < s.w and 0 <= y < s.h
    def get(s, x, y):
        x, y = int(x), int(y)
        if not s.ok(x, y): return 0
        p = s.px[x, y]; return round(p[0] / 28) if p[3] else 0
    def p(s, x, y, i):
        x, y = int(x), int(y)
        if s.ok(x, y): s.px[x, y] = g(i) if i else (0, 0, 0, 0)
    def r(s, x0, y0, x1, y1, i):
        x0, y0, x1, y1 = int(x0), int(y0), int(x1), int(y1)
        for y in range(max(0, min(y0, y1)), min(s.h, max(y0, y1) + 1)):
            for x in range(max(0, min(x0, x1)), min(s.w, max(x0, x1) + 1)): s.px[x, y] = g(i) if i else (0, 0, 0, 0)
    def e(s, x0, y0, x1, y1, i): s.d.ellipse([x0, y0, x1, y1], fill=g(i))
    def poly(s, pts, i): s.d.polygon(pts, fill=g(i))
    def line(s, x0, y0, x1, y1, i): s.d.line([x0, y0, x1, y1], fill=g(i))
    def rnd(s, x, y, n): return (((x * 73856093) ^ (y * 19349663) ^ s.seed) & 0x7FFFFFFF) % n
    def swap(s, x0, y0, x1, y1, pairs):
        for y in range(max(0, int(y0)), min(s.h, int(y1) + 1)):
            for x in range(max(0, int(x0)), min(s.w, int(x1) + 1)):
                v = s.get(x, y)
                if v in pairs: s.p(x, y, pairs[v])
    def shade_rows(s, y0, y1, lit=2, dark=3):
        # light from the top-left (EK C 3): first columns of each wall row lit, last ones in shadow
        for y in range(max(0, int(y0)), min(s.h, int(y1) + 1)):
            xs = [x for x in range(s.w) if s.get(x, y) == WA]
            if not xs: continue
            for x in xs[:lit]: s.p(x, y, LI)
            for x in xs[-dark:]: s.p(x, y, SH)

def outline(c):
    # 1 px dark contour on building silhouettes (EK C 3), skipped on 1 px lines so poles and rails stay readable
    src = [[c.get(x, y) for x in range(c.w)] for y in range(c.h)]
    at = lambda x, y: src[y][x] if 0 <= x < c.w and 0 <= y < c.h else 0
    for y in range(c.h):
        for x in range(c.w):
            if src[y][x] in (0, OL, TR, LT): continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                if not at(x + dx, y + dy) and at(x - dx, y - dy): c.p(x, y, OL); break

# ---- ortak parçalar
def door(c, cx, bottom, dw, dh, arched=False, barn=False):
    x0 = cx - dw // 2; x1 = x0 + dw - 1; top = bottom - dh
    bg = c.get(x0 - 2, top - 1)
    c.r(x0 - 1, top - 1, x1 + 1, bottom, TR); c.r(x0, top, x1, bottom, SH)
    if barn: c.line(x0, top, x1, bottom, TR); c.line(x1, top, x0, bottom, TR)
    elif dw >= 4: c.r(cx, top, cx, bottom, OL)
    if arched and bg:
        c.p(x0 - 1, top - 1, bg); c.p(x1 + 1, top - 1, bg); c.p(x0, top, TR); c.p(x1, top, TR)

def window(c, x, y, ws, round_=False):
    bg = c.get(x - 2, y)
    c.r(x - 1, y - 1, x + ws, y + ws, TR); c.r(x, y, x + ws - 1, y + ws - 1, LT)
    if ws >= 5: c.r(x + ws // 2, y, x + ws // 2, y + ws - 1, TR); c.r(x, y + ws // 2, x + ws - 1, y + ws // 2, TR)
    else: c.p(x + ws - 1, y + ws - 1, TR)
    if round_ and bg:
        for px_, py_ in ((x - 1, y - 1), (x + ws, y - 1), (x - 1, y + ws), (x + ws, y + ws)): c.p(px_, py_, bg)

def windows(c, x0, x1, y, ws, gap, clear, round_=False):
    # symmetric pairs around the centre; clear = half-width kept free for the door (0 = centre window instead)
    cx = (x0 + x1) // 2
    if clear == 0: window(c, cx - ws // 2, y, ws, round_); clear = ws // 2 + gap - 1
    off = clear + 2
    while cx + off + ws + 1 <= x1 - 2:
        window(c, cx + off, y, ws, round_); window(c, cx - off - ws + 1, y, ws, round_)
        off += ws + gap

def bricks(c, x0, y0, x1, y1):
    c.r(x0, y0, x1, y1, WA)
    for row, y in enumerate(range(int(y0), int(y1) + 1, 3)):
        c.r(x0, y, x1, y, SH)
        for x in range(int(x0) + (2 if row % 2 else 0), int(x1), 5):
            c.r(x, y + 1, x, min(y1, y + 2), SH); c.p(x + 1, y + 1, LI)
    c.r(x0, y0, x0, y1, LI); c.r(x1 - 1, y0, x1, y1, SH)

def planks(c, x0, y0, x1, y1):
    for row, y in enumerate(range(int(y0), int(y1) + 1, 3)):
        c.r(x0, y, x1, min(y1, y + 2), WA); c.r(x0, y, x1, y, LI)
        if y + 2 <= y1: c.r(x0, y + 2, x1, y + 2, SH)
        for x in range(int(x0) + (4 if row % 2 else 0), int(x1), 9): c.r(x, y, x, min(y1, y + 2), SH)

def pointed_roof(c, x0, x1, top, eave):
    cx = (x0 + x1) / 2
    c.poly([(x0, eave), (cx, top), (x1, eave)], RF); c.poly([(cx, top), (x1, eave), (cx, eave)], RS)
    c.r(x0, eave, x1, eave, OL)

def gable_roof(c, x0, x1, top, eave):
    if x1 - x0 < 16: pointed_roof(c, x0, x1, top, eave); return
    ridge = top + max(1, (eave - top) // 3)
    c.r(x0 + 1, top, x1 - 1, eave, RF)
    c.r(x0 + 1, ridge, x1 - 1, ridge, RS)                                     # ridge line
    for y in range(ridge + 3, eave, 3):                                        # tile courses, staggered seams
        c.r(x0, y, x1, y, RS)
        for x in range(x0 + 1 + (y // 3 % 2) * 2, x1, 4): c.p(x, y - 1, RS)
    c.r(x1 - 3, ridge, x1, eave, RS)                                           # shaded end
    c.r(x0, eave, x1, eave, OL)

def dome_shade(c, x0, x1, top, bottom, frm=WA):
    cx, rx = (x0 + x1) / 2, max(1, (x1 - x0) / 2)
    for y in range(max(0, int(top)), min(c.h, int(bottom) + 1)):
        for x in range(int(x0), int(x1) + 1):
            if c.get(x, y) != frm: continue
            u, v = (x - cx) / rx, (y - top) / max(1, bottom - top)
            n = (c.rnd(x, y, 5) - 2) * 0.06
            if u + n > 0.4: c.p(x, y, SH)
            elif u + v * 0.8 + n < -0.25: c.p(x, y, LI)

def fence(c, x0, y0, x1, y1):
    for y in (y0, y1):
        c.r(x0, y - 1, x1, y - 1, TR); c.r(x0, y - 3, x1, y - 3, TR)
        for x in range(x0, x1 + 1, 5): c.r(x, y - 4, x, y, SH)
    for x in (x0, x1):
        c.r(x, y0 - 1, x, y1, TR)
        for y in range(y0, y1, 4): c.r(x, y - 3, x, y, SH)

def skep(c, x0, x1, top, bottom):
    cap = max(3, (x1 - x0) // 2)
    c.e(x0, top, x1, top + 2 * cap, WA); c.r(x0, top + cap, x1, bottom, WA)
    for y in range(int(top) + 3, int(bottom), 3):
        for x in range(int(x0), int(x1) + 1):
            if c.get(x, y) == WA: c.p(x, y, SH)
    c.shade_rows(top, bottom, 2, 2)
    for x in range(int(x0), int(x1) + 1):                                   # comb rim in the owner's colour
        ys = [y for y in range(int(top), int(top) + cap + 2) if c.get(x, y)]
        for y in ys[:2]: c.p(x, y, RF)

# ---- stil gövdeleri: (x0, x1, top, bottom) içine çizer, duvar üst satırını döndürür
WALL_FRAC = {"human": 0.40, "elf": 0.45, "dwarf": 0.50}

def wall_top(style, top, bottom, storeys):
    f = 0.7 if storeys >= 3 else WALL_FRAC[style] + (0.15 if storeys == 2 else 0)
    return bottom - max(8, int((bottom - top + 1) * f)) + 1

def body_human(c, x0, x1, top, bottom, storeys):
    wt = wall_top("human", top, bottom, storeys)
    a, b = x0 + 2, x1 - 2                                     # eaves overhang the wall
    c.r(a, wt, b, bottom, LI); c.r(b - 2, wt, b, bottom, WA)  # plaster, shaded end
    c.r(a, bottom - 2, b, bottom, WA)                         # stone footing
    for x in range(a + 1, b, 3): c.p(x + (x // 3) % 2, bottom - 1, SH)
    for x in list(range(a, b - 2, 8)) + [b]: c.r(x, wt, x, bottom - 3, SH)   # timber posts
    if storeys == 2: c.r(a, (wt + bottom) // 2, b, (wt + bottom) // 2, SH)
    gable_roof(c, x0, x1, top, wt)
    c.r(a, wt + 1, b, wt + 1, SH)                             # shadow under the eaves
    return wt

def body_elf(c, x0, x1, top, bottom, storeys):
    wt = wall_top("elf", top, bottom, storeys)
    inset = max(2, (x1 - x0) // 8)
    a, b = x0 + inset, x1 - inset
    c.r(a, wt, b, bottom, WA); c.r(a - 2, bottom - 1, b + 2, bottom, WA)      # trunk, root flare
    for x in range(a + 3, b - 2, 3):
        for y in range(wt, bottom + 1):
            if c.rnd(x, y // 3, 5): c.p(x, y, SH)                              # broken bark streaks
    c.r(a, wt, a + 1, bottom, LI); c.r(b - 1, wt, b, bottom, SH)
    cy1 = wt + 3
    c.e(x0, top, x1, cy1, RF)                                                  # leaf canopy
    cx, cy, rx, ry = (x0 + x1) / 2, (top + cy1) / 2, max(1, (x1 - x0) / 2), max(1, (cy1 - top) / 2)
    for y in range(max(0, top), cy1 + 1):
        for x in range(x0, x1 + 1):
            if c.get(x, y) != RF: continue
            u, v = (x - cx) / rx, (y - cy) / ry
            if u * 0.7 + v + (c.rnd(x, y, 3) - 1) * 0.15 > 0.35 or c.rnd(x, y, 9) == 0: c.p(x, y, RS)
    return cy1

def body_dwarf(c, x0, x1, top, bottom, storeys):
    wt = wall_top("dwarf", top, bottom, storeys)
    slab = max(3, (bottom - top) // 7)
    c.r(x0 + 1, wt - slab, x1 - 1, wt - 1, LI); c.r(x0 + 1, wt - slab, x1 - 1, wt - slab, WA)   # flat stone roof
    bricks(c, x0 + 1, wt, x1 - 1, bottom)
    for x in range(x0 + 1, x1, 4): c.r(x, wt - 2, min(x1 - 1, x + 1), wt, WA)                   # merlons
    return wt

def body_orc(c, x0, x1, top, bottom, storeys):
    t = top + max(3, (bottom - top) // 6)
    cx = (x0 + x1) / 2
    half = max(0, (x1 - x0) / 2 - (bottom - t) * 0.55)       # wide plots get a long hide hall
    c.poly([(x0, bottom), (cx - half, t), (cx + half, t), (x1, bottom)], WA)
    for k in range(1, 6):
        c.line(cx - half + k / 6 * 2 * half, t, x0 + k / 6 * (x1 - x0), bottom, SH)         # hide seams
    c.shade_rows(t, bottom, 3, 4)
    by = t + int((bottom - t) * 0.42)
    c.swap(x0, by, x1, by + 1, {WA: RF, LI: RF, SH: RS}); c.swap(x0, by + 2, x1, by + 2, {WA: RS, LI: RS, SH: RS})
    c.line(cx - half, t + 2, cx - half - 4, top, TR); c.line(cx + half, t + 2, cx + half + 4, top, TR)   # bone poles
    if half > 6: c.line(cx, t + 1, cx, top, TR)
    dw, dh = max(3, (x1 - x0) // 8), max(5, (bottom - t) // 3)
    c.poly([(cx - dw, bottom), (cx, bottom - dh), (cx + dw, bottom)], OL); c.line(cx, bottom - dh, cx + dw, bottom, LI)
    return by + 3

def body_beast(c, x0, x1, top, bottom, storeys):
    dt = top + max(5, int((bottom - top) * 0.3))
    c.e(x0, dt, x1, bottom + (bottom - dt), WA)               # earth mound: keep only the half above the ground line
    c.swap(x0, bottom + 1, x1, c.h - 1, {WA: 0})
    dome_shade(c, x0, x1, dt, bottom)
    for i in range((x1 - x0) * (bottom - dt) // 30):          # twigs
        x, y = x0 + c.rnd(i, 1, max(1, x1 - x0)), dt + c.rnd(i, 2, max(1, bottom - dt))
        if c.get(x, y) in (WA, LI, SH): c.line(x, y, x + 2, y + c.rnd(i, 3, 3) - 1, TR)
    cx, dw = (x0 + x1) // 2, max(3, (x1 - x0) // 7)
    c.e(cx - dw - 1, bottom - dw * 2 - 1, cx + dw + 1, bottom + dw * 2, SH); c.e(cx - dw, bottom - dw * 2, cx + dw, bottom + dw * 2, OL)
    c.r(cx, top, cx, dt + 2, TR); c.poly([(cx + 1, top), (cx + 6, top + 2), (cx + 1, top + 4)], RF)   # flag
    return bottom - (bottom - dt) // 2

def body_insect(c, x0, x1, top, bottom, storeys):
    dt = top + max(2, int((bottom - top) * 0.15))
    cap = max(4, (x1 - x0) // 3)
    c.e(x0, dt, x1, dt + 2 * cap, WA); c.r(x0, dt + cap, x1, bottom, WA)
    for y in range(dt, bottom + 1):                           # wax comb cells
        row = (y - dt) // 4
        for x in range(x0, x1 + 1):
            if c.get(x, y) != WA: continue
            xo, yo = (x - x0 + (3 if row % 2 else 0)) % 6, (y - dt) % 4
            if yo == 0 or xo == 0: c.p(x, y, SH)
            elif yo == 1 and xo == 1: c.p(x, y, LI)
    c.swap(x1 - (x1 - x0) // 5, dt, x1, bottom, {WA: SH, SH: OL, LI: WA})
    for x in range(x0, x1 + 1):                               # comb rim in the owner's colour
        ys = [y for y in range(dt, dt + cap + 2) if c.get(x, y)]
        for y in ys[:2]: c.p(x, y, RF)
    cx, dw = (x0 + x1) // 2, max(2, (x1 - x0) // 8)
    c.r(cx - dw - 1, bottom - dw * 3 - 1, cx + dw + 1, bottom, TR); c.r(cx - dw, bottom - dw * 3, cx + dw, bottom, OL)
    return dt + cap

BODY = {"human": body_human, "elf": body_elf, "dwarf": body_dwarf, "orc": body_orc, "beast": body_beast, "insect": body_insect}
TWO_STOREY = {"manor", "hall_2", "hall_3", "inn", "library", "barracks", "grand_temple"}
CHIMNEY = {"house", "manor", "bakery", "smithy", "inn", "healers_house", "hall_2", "hall_3"}
TOWERS = {"hall_2": 1, "hall_3": 3, "temple": 1, "grand_temple": 3}
WIDE_DOOR = {"storage", "granary", "barracks", "stable", "farm_shed", "smithy", "temple", "grand_temple", "hall_1", "hall_2", "hall_3"}
EMBLEM = {"barracks", "healers_house", "inn"}

def generic(c, style, key, cat, x0, x1, top, bottom):
    walled = style in WALL_FRAC
    ntow = TOWERS.get(key, 0) if walled else 0
    if (cat == "center" or ntow) and top == 0: top = max(6, c.h // 4)
    if ntow:
        tw = max(7, (x1 - x0) // 5) | 1
        mid = (x0 + x1) // 2
        spots = [x0 + tw // 2 + 1, x1 - tw // 2 - 1] if ntow == 3 else []
        for tx in spots + [mid]:
            ttop = (7 if cat == "center" else 0) if tx == mid else c.h // 8
            tb = top + (bottom - top) // 2
            twt = BODY[style](c, tx - tw // 2, tx + tw // 2, ttop, tb, 3)
            window(c, tx - 1, twt + 3, 3, style == "elf")
            if cat == "center" and tx == mid:
                c.r(tx, 0, tx, ttop + 1, TR); c.poly([(tx + 1, 0), (tx + 6, 2), (tx + 1, 4)], RF)
    storeys = 2 if key in TWO_STOREY and c.h >= 40 else 1
    wt = BODY[style](c, x0, x1, top, bottom, storeys)
    if walled: openings(c, style, key, x0, x1, wt, bottom, storeys)
    decorate(c, style, key, cat, x0, x1, top, wt, bottom, ntow)
    return wt

def openings(c, style, key, x0, x1, wt, bottom, storeys):
    cx = (x0 + x1) // 2
    wide = key in WIDE_DOOR
    dw = max(3, min(9 if wide else 5, (x1 - x0) // (4 if wide else 7)))
    lower = bottom - wt if storeys == 1 else (bottom - wt) // 2
    dh = max(4, min(lower - 3, int(lower * (0.55 if style == "dwarf" else 0.7))))
    door(c, cx, bottom, dw, dh, arched=style in ("elf", "dwarf") or "temple" in key, barn=key in ("storage", "granary", "stable", "farm_shed"))
    ws = 5 if lower >= 14 else 3
    clear = dw // 2 + (10 if key in EMBLEM else 2)
    windows(c, x0 + 2, x1 - 2, bottom - lower + 1 + max(2, int(lower * 0.28)), ws, 4 if key == "library" else 6, clear, style == "elf")
    if storeys == 2:
        up = bottom - wt - lower
        windows(c, x0 + 2, x1 - 2, wt + 2 + max(1, (up - ws) // 2), ws, 4 if key == "library" else 6, 0, style == "elf")

def decorate(c, style, key, cat, x0, x1, top, wt, bottom, ntow):
    cx = (x0 + x1) // 2
    if key in CHIMNEY and style in ("human", "dwarf"):
        x = x0 + int((x1 - x0) * 0.72); ch = max(5, (wt - top) // 3)
        y0 = top if style == "human" else wt - max(3, (bottom - top) // 7) - ch + 2
        c.r(x, y0, x + 3, y0 + ch, WA); c.r(x, y0, x, y0 + ch, LI); c.r(x + 3, y0, x + 3, y0 + ch, SH); c.r(x - 1, y0, x + 4, y0, SH)
        if key == "smithy": c.p(x + 1, y0, LT); c.p(x + 2, y0, LT)
    if cat == "center" and not ntow and style in WALL_FRAC:
        c.r(cx, 0, cx, top + 2, TR); c.poly([(cx + 1, 0), (cx + 7, 2), (cx + 1, 5)], RF)
    if style == "dwarf" and cat in ("center", "house", "military", "culture"):     # banner over the door
        c.r(cx - 3, wt + 1, cx + 3, wt + 1, TR); c.r(cx - 2, wt + 2, cx + 2, wt + 6, RF); c.r(cx + 2, wt + 2, cx + 2, wt + 6, RS)
        c.p(cx, wt + 6, SH)
    if key not in EMBLEM or style not in WALL_FRAC: return
    lower = bottom - wt if not (key in TWO_STOREY and c.h >= 40) else (bottom - wt) // 2
    ey = bottom - lower + 1 + max(2, int(lower * 0.28))   # level with the ground-floor windows
    for sx in (cx - 10, cx + 6):
        if key == "barracks":                                                    # shields
            c.r(sx, ey, sx + 4, ey + 3, RF); c.r(sx + 1, ey + 4, sx + 3, ey + 4, RF); c.p(sx + 2, ey + 5, RF); c.r(sx + 4, ey, sx + 4, ey + 3, RS); c.p(sx + 2, ey + 1, LT)
        elif key == "healers_house":                                             # cross
            c.r(sx, ey, sx + 4, ey + 4, RS); c.r(sx + 2, ey, sx + 2, ey + 4, LT); c.r(sx, ey + 2, sx + 4, ey + 2, LT)
    if key == "inn":                                                             # hanging sign
        c.r(cx + 6, ey, cx + 11, ey, TR); c.r(cx + 7, ey + 1, cx + 7, ey + 1, TR); c.r(cx + 7, ey + 2, cx + 11, ey + 5, LT); c.r(cx + 7, ey + 2, cx + 11, ey + 2, TR)

# ---- özel yapılar (malzeme rengi yine stil paletinden gelir)
def sp_bonfire(c, style, key):
    W, H = c.w, c.h; cx, b = W // 2, H - 3
    stones = [(cx + math.cos(i / 10 * 2 * math.pi) * W * 0.32, b - 4 + math.sin(i / 10 * 2 * math.pi) * 3.5) for i in range(10)]
    def st(x, y): c.e(x - 1.5, y - 1, x + 1.5, y + 1.5, WA); c.p(x - 1, y - 1, LI)
    for x, y in stones:
        if y < b - 4: st(x, y)
    c.line(cx - 5, b - 2, cx + 5, b - 6, SH); c.line(cx - 5, b - 6, cx + 5, b - 2, SH)
    c.poly([(cx - 4, b - 4), (cx, b - 16), (cx + 4, b - 4)], LT); c.poly([(cx - 2, b - 4), (cx + 1, b - 10), (cx + 2, b - 4)], LI)
    for x, y in stones:
        if y >= b - 4: st(x, y)

def sp_tent(c, style, key):
    W, H = c.w, c.h; cx, b = W // 2, H - 1; t = H - int(W * 0.8)
    c.poly([(1, b), (cx, t), (W - 2, b)], RF); c.poly([(cx, t), (W - 2, b), (cx, b)], RS)
    for k in (-1, 1): c.line(cx, t + 2, cx + k * (W // 2 - 4), b, RS if k < 0 else SH)   # canvas folds
    c.poly([(cx - 4, b), (cx, b - 10), (cx + 4, b)], OL); c.line(cx, b - 10, cx + 4, b, LI)
    c.r(cx, t - 3, cx, t, TR)

def sp_well(c, style, key):
    W, H = c.w, c.h; cx = W // 2
    c.r(1, H - 6, W - 2, H - 1, WA); c.e(1, H - 9, W - 2, H - 4, LI); c.e(3, H - 8, W - 4, H - 5, OL)
    c.r(W - 3, H - 6, W - 2, H - 1, SH); c.r(1, H - 3, W - 2, H - 3, SH)
    c.r(2, H - 16, 2, H - 7, TR); c.r(W - 3, H - 16, W - 3, H - 7, TR); c.r(2, H - 15, W - 3, H - 15, TR)
    c.r(cx, H - 15, cx, H - 9, SH); c.r(cx - 1, H - 10, cx + 1, H - 8, WA)
    pointed_roof(c, 0, W - 1, 0, H - 16)

def sp_watchtower(c, style, key):
    W, H = c.w, c.h
    c.r(3, 11, W - 4, H - 1, WA); c.r(3, 11, 4, H - 1, LI); c.r(W - 5, 11, W - 4, H - 1, SH)
    for y in range(14, H - 1, 4): c.r(3, y, W - 4, y, SH)
    door(c, W // 2, H - 1, 3, 5); c.r(W // 2 - 1, 16, W // 2 + 1, 17, LT)
    c.r(1, 9, W - 2, 11, TR); c.r(1, 11, W - 2, 11, OL)
    for x in range(1, W - 1, 3): c.r(x, 6, x, 8, TR)
    pointed_roof(c, 0, W - 1, 0, 7)

def sp_wall(c, style, key):
    W, H = c.w, c.h
    bricks(c, 0, 8, W - 1, H - 1); c.r(0, 5, W - 1, 7, LI)
    for x in range(0, W, 5): c.r(x, 2, min(W - 1, x + 2), 5, WA); c.r(x, 2, min(W - 1, x + 2), 2, LI)

def sp_gate(c, style, key):
    W, H = c.w, c.h
    bricks(c, 0, 8, W - 1, H - 1); c.r(0, 6, W - 1, 7, LI)
    for x0 in (0, W - 9):
        bricks(c, x0, 3, x0 + 8, H - 1); c.r(x0, 1, x0 + 8, 2, LI)
        for x in (x0, x0 + 3, x0 + 6): c.r(x, 0, x + 1, 1, WA)
    a0, a1 = 10, W - 11
    c.r(a0 - 1, 10, a1 + 1, H - 1, TR); c.r(a0, 11, a1, H - 1, OL)
    for x in range(a0 + 1, a1, 2): c.r(x, 11, x, H - 3, SH)
    c.r(a0, 14, a1, 14, SH); c.p(a0 - 1, 10, WA); c.p(a1 + 1, 10, WA)

def sp_windmill(c, style, key):
    W, H = c.w, c.h; cx, t = W // 2, H // 3
    c.poly([(6, H - 1), (9, t), (W - 10, t), (W - 7, H - 1)], WA)
    for y in range(t + 4, H - 1, 5): c.swap(0, y, W - 1, y, {WA: SH})
    c.shade_rows(t, H - 1, 2, 3)
    door(c, cx, H - 1, 4, 7); window(c, cx - 1, t + 7, 3)
    pointed_roof(c, 7, W - 8, t - 7, t + 1)
    hx, hy = cx, t - 1
    for dx, dy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        ex, ey, px_, py_ = hx + dx * 13, hy + dy * 13, -dy, dx
        c.poly([(hx + dx * 4, hy + dy * 4), (ex, ey), (ex + px_ * 3, ey + py_ * 3), (hx + dx * 4 + px_ * 3, hy + dy * 4 + py_ * 3)], LI)
        c.line(hx, hy, ex, ey, TR)
    c.r(hx - 1, hy - 1, hx, hy, OL)

def sp_pasture(c, style, key):
    W, H = c.w, c.h; top = H - c.fh + 6
    c.r(1, top - 3, W - 2, top - 3, TR); c.r(1, top - 1, W - 2, top - 1, TR)
    generic(c, style, "farm_shed", "economy", 3, 3 + W // 3, top - 14, top + 12)
    fence(c, 1, top, W - 2, H - 1)

def sp_market(c, style, key):
    W, H = c.w, c.h; top = H - c.fh + 2
    sw = (W - 8) // 3
    for y0 in (top, top + (c.fh - 2) // 2):
        for i in range(3):
            x0 = 2 + i * (sw + 2)
            c.r(x0 + 1, y0 + 5, x0 + 1, y0 + 12, TR); c.r(x0 + sw - 2, y0 + 5, x0 + sw - 2, y0 + 12, TR)
            c.r(x0, y0 + 9, x0 + sw - 1, y0 + 12, WA); c.r(x0, y0 + 9, x0 + sw - 1, y0 + 9, LI); c.r(x0 + sw - 2, y0 + 10, x0 + sw - 1, y0 + 12, SH)
            for x in range(x0 + 2, x0 + sw - 2, 2): c.p(x, y0 + 8, LT if (x // 2) % 2 else RS)   # goods
            for x in range(x0, x0 + sw):
                stripe = RF if (x - x0) // 2 % 2 == 0 else LT
                c.r(x, y0, x, y0 + 3, stripe)
                if (x - x0) % 2 == 0: c.p(x, y0 + 4, stripe)                                       # scalloped edge

def sp_docks(c, style, key):
    W, H = c.w, c.h; top = H - c.fh + 2
    planks(c, 0, top, W - 1, H - 4)
    for x in range(1, W - 1, 7): c.r(x, H - 4, x + 1, H - 1, SH)
    for x in (3, W - 5): c.r(x, top - 2, x + 1, top + 1, TR)
    c.r(W - 12, top - 3, W - 5, top + 4, WA); c.r(W - 12, top - 3, W - 5, top - 3, LI); c.r(W - 6, top - 2, W - 5, top + 4, SH)
    c.line(W - 12, top - 3, W - 5, top + 4, SH)

def sp_shipyard(c, style, key):
    W, H = c.w, c.h; top = H - c.fh + 2
    planks(c, 0, top + 8, W - 1, H - 4)
    for x in range(1, W - 1, 7): c.r(x, H - 4, x + 1, H - 1, SH)
    generic(c, style, "storage", "economy", W - 22, W - 2, top - 14, top + 12)
    y1 = H - 8
    c.poly([(4, y1 - 9), (int(W * 0.62), y1 - 9), (int(W * 0.56), y1), (9, y1)], WA)          # hull on the slipway
    c.r(4, y1 - 9, int(W * 0.62), y1 - 9, LI); c.r(9, y1, int(W * 0.56), y1, SH)
    for x in range(7, int(W * 0.6), 4): c.r(x, y1 - 14, x, y1 - 1, TR)                          # unfinished ribs

def sp_mine(c, style, key):
    W, H = c.w, c.h; mt = H - c.fh - 2
    c.e(0, mt, W - 1, H - 1 + (H - mt), WA)
    dome_shade(c, 0, W - 1, mt, H - 1)
    for i in range(W // 3):                                                                       # rock cracks
        x, y = c.rnd(i, 1, W), mt + c.rnd(i, 2, H - mt)
        if c.get(x, y) in (WA, LI, SH): c.line(x, y, x + c.rnd(i, 3, 3) - 1, y + 2, OL)
    cx = W // 2
    c.r(cx - 7, H - 14, cx + 6, H - 1, TR); c.r(cx - 5, H - 12, cx + 4, H - 1, OL)
    c.r(cx - 3, H - 3, cx + 2, H - 3, SH)
    c.r(cx + 9, H - 6, cx + 15, H - 2, SH); c.p(cx + 10, H - 7, LT); c.p(cx + 12, H - 7, LI); c.p(cx + 14, H - 7, LT)
    c.p(cx + 10, H - 1, OL); c.p(cx + 14, H - 1, OL)

def sp_graveyard(c, style, key):
    W, H = c.w, c.h; top = H - c.fh + 4
    fence(c, 1, top, W - 2, H - 1)
    for row in range(3):
        for col in range(3):
            x, y = 6 + col * 13 + (row % 2) * 3, top + 6 + row * 12
            if (row + col) % 3 == 1: c.r(x + 1, y, x + 2, y + 7, WA); c.r(x, y + 2, x + 3, y + 3, WA); c.r(x + 1, y, x + 1, y + 7, LI)
            else: c.r(x, y + 1, x + 4, y + 7, WA); c.r(x + 1, y, x + 3, y, WA); c.r(x, y + 1, x, y + 7, LI); c.r(x + 4, y + 1, x + 4, y + 7, SH)
            c.r(x - 1, y + 8, x + 5, y + 8, SH)

def sp_statue(c, style, key):
    W, H = c.w, c.h; cx = W // 2
    for (x0, y0, x1, y1) in ((3, H - 6, W - 4, H - 1), (7, H - 11, W - 8, H - 7)):
        c.r(x0, y0, x1, y1, WA); c.r(x0, y0, x1, y0, LI); c.r(x1 - 2, y0 + 1, x1, y1, SH)
    if key == "statue_god":
        fb = H - 12
        c.poly([(cx - 5, fb), (cx - 2, fb - 18), (cx + 2, fb - 18), (cx + 5, fb)], LI)
        c.poly([(cx + 1, fb - 18), (cx + 2, fb - 18), (cx + 5, fb), (cx + 1, fb)], WA)
        c.e(cx - 2, fb - 23, cx + 2, fb - 19, LI)
        c.line(cx - 2, fb - 16, cx - 7, fb - 25, LI); c.line(cx + 2, fb - 16, cx + 7, fb - 25, WA)
        c.p(cx - 7, fb - 26, LT); c.p(cx + 7, fb - 26, LT)
    else:
        c.poly([(cx - 4, H - 12), (cx - 2, 6), (cx + 2, 6), (cx + 4, H - 12)], LI)
        c.poly([(cx, 6), (cx + 2, 6), (cx + 4, H - 12), (cx, H - 12)], WA)
        c.poly([(cx - 2, 6), (cx, 1), (cx + 2, 6)], LT)
        c.r(cx - 3, H - 5, cx + 2, H - 3, TR)

def sp_pyre(c, style, key):
    W, H = c.w, c.h; cx = W // 2
    c.e(2, H - 8, W - 3, H - 1, WA); c.e(2, H - 8, W - 3, H - 3, LI)
    for i, y in enumerate(range(H - 6, H - 20, -3)):
        inset = i * 2
        if i % 2 == 0: c.r(5 + inset, y - 2, W - 6 - inset, y, SH); c.r(5 + inset, y - 2, W - 6 - inset, y - 2, WA)
        else:
            for x in range(5 + inset, W - 6 - inset, 3): c.e(x, y - 2, x + 2, y, WA); c.p(x + 1, y - 1, SH)
    c.r(cx - 5, H - 21, cx + 4, H - 20, OL); c.p(cx - 2, H - 21, LT); c.p(cx + 2, H - 21, LT)

def sp_nest(c, style, key):
    W, H = c.w, c.h; b, t, cx = H - 1, H - 16, W // 2
    c.e(1, t, W - 2, b, SH); c.e(5, t + 3, W - 6, b - 4, OL)
    for i in range(40):
        x, y = 2 + c.rnd(i, 1, W - 4), t + c.rnd(i, 2, b - t)
        if c.get(x, y) == SH: c.line(x, y, x + 2, y + c.rnd(i, 4, 3) - 1, TR if i % 2 else WA)
    for x in (cx - 4, cx, cx + 3): c.e(x - 1, t + 5, x + 2, t + 9, LI); c.p(x, t + 6, LT)

def sp_hive(c, style, key):
    W, H = c.w, c.h
    skep(c, 2, W - 3, H // 4, H - 1)
    cx = W // 2
    for ex, ey in ((cx, H - 1), (cx - W // 4, H * 2 // 3), (cx + W // 4, H // 2)):
        c.e(ex - 2, ey - 4, ex + 2, ey, OL)

def sp_beehive(c, style, key):
    W, H = c.w, c.h
    c.r(3, H - 4, 3, H - 1, TR); c.r(W - 4, H - 4, W - 4, H - 1, TR); c.r(1, H - 5, W - 2, H - 4, TR)
    skep(c, 2, W - 3, 3, H - 6); c.e(W // 2 - 1, H - 9, W // 2 + 1, H - 7, OL)

def cp_granary(c, style, key):
    W, H = c.w, c.h
    generic(c, style, key, "economy", 0, W - 10, 0, H - 1)
    x0, x1, y0 = W - 12, W - 2, H // 3
    c.r(x0, y0 + 5, x1, H - 1, WA)
    for y in range(y0 + 9, H - 1, 5): c.r(x0, y, x1, y, SH)
    c.shade_rows(y0 + 5, H - 1, 2, 3); pointed_roof(c, x0 - 1, x1 + 1, y0, y0 + 5)

def cp_lumber(c, style, key):
    W, H = c.w, c.h
    generic(c, style, key, "economy", 0, W - 12, 0, H - 1)
    for n, y in ((3, H - 4), (2, H - 7), (1, H - 10)):
        for i in range(n):
            x = W - 13 + (3 - n) * 2 + i * 4
            c.e(x, y - 3, x + 3, y, LI); c.p(x + 1, y - 2, SH); c.p(x + 2, y - 1, SH)

def cp_fishing(c, style, key):
    W, H = c.w, c.h
    generic(c, style, key, "naval", 8, W - 1, 0, H - 1)
    c.r(1, H - 14, 1, H - 1, TR); c.r(7, H - 14, 7, H - 1, TR)
    for y in range(H - 13, H - 3, 2):
        for x in range(2, 7): c.p(x, y + (x % 2), SH)

SPECIAL = {"bonfire": sp_bonfire, "tent": sp_tent, "well": sp_well, "watchtower": sp_watchtower, "wall": sp_wall, "gate": sp_gate,
           "windmill": sp_windmill, "pasture": sp_pasture, "market": sp_market, "docks": sp_docks, "shipyard": sp_shipyard,
           "mine": sp_mine, "graveyard": sp_graveyard, "statue_god": sp_statue, "monument": sp_statue, "pyre": sp_pyre,
           "nest": sp_nest, "hive": sp_hive, "beehive": sp_beehive, "granary": cp_granary, "lumber_camp": cp_lumber,
           "fishing_hut": cp_fishing}

def construction(c):
    # EK C 6.3: scaffold over the lower half of the finished building
    W, H = c.w, c.h; cut = H - int(c.fh * 0.55)
    for x in range(W):
        for y in range(0, min(H, cut + c.rnd(x // 3, 5, 3))): c.p(x, y, 0)
    for x in (1, W // 3, 2 * W // 3, W - 2): c.r(x, cut - 6, x, H - 1, TR)
    for y in range(cut - 5, H - 1, 5): c.r(1, y, W - 2, y, TR)
    c.line(1, H - 1, W // 3, cut - 5, TR)

def ruin(c):
    # EK C 6.3: broken wall stumps, burnt roof, dark empty windows, rubble
    W, H = c.w, c.h
    for x in range(W):
        keep = int(c.fh * (0.18 + 0.3 * c.rnd(x // 4, 7, 100) / 100))
        for y in range(0, H - keep): c.p(x, y, 0)
    c.swap(0, 0, W - 1, H - 1, {RF: SH, RS: SH, LT: OL})
    for i in range(W // 2): c.p(c.rnd(i, 11, W), H - 1 - c.rnd(i, 13, 4), (WA, SH, LI)[i % 3])
    outline(c)

def building_sprite(b, style, state):
    cw, ch = [int(x) for x in b["size"].split("x")] if "x" in b["size"] else (2, 2)
    key = b["id"][4:]
    W, fh = cw * CELL, ch * CELL
    H = int(fh * TALL[key]) if key in TALL else fh + max(2, fh // 2)
    c = Pix(W, H, fh, style + key)
    if key in SPECIAL: SPECIAL[key](c, style, key)
    else: generic(c, style, key, b["category"], 0, W - 1, 0, H - 1)
    outline(c)
    if state == "construction": construction(c)
    elif state == "ruin": ruin(c)
    return c.im

def building_preview_colors(style_def, kingdom=(58, 96, 178)):
    # mirrors BuildingRenderer.WritePalette so previews match the game
    hx = lambda s: tuple(int(s[i:i + 2], 16) for i in (1, 3, 5))
    wall, trim, acc = hx(style_def["wall"]), hx(style_def["trim"]), hx(style_def["accent"])
    roof = tuple(k + (a - k) * style_def["roofMix"] for k, a in zip(kingdom, acc))
    mul = lambda col, f: tuple(min(255, int(v * f)) for v in col)
    return [None, mul(wall, .3), mul(wall, .62), mul(wall, .85), tuple(int(v + (255 - v) * .18) for v in wall),
            mul(roof, .62), mul(roof, 1), trim, (255, 222, 140)]

def bcolorize(im, pal):
    out = Image.new("RGBA", im.size, (0, 0, 0, 0)); px, po = im.load(), out.load()
    for y in range(im.height):
        for x in range(im.width):
            r_, _, _, a = px[x, y]
            if a: po[x, y] = pal[min(8, max(1, round(r_ / 28)))] + (255,)
    return out

# ---- Feature'lar (RGB, biyom rengiyle çizilir)
# Readable placeholder hues (degrees, saturation) by id keyword; everything else: green foliage with a small hash shift.
FEATURE_HUES = [("candy", 330, 0.6), ("ember", 20, 0.8), ("volcanic", 15, 0.25), ("snowpine", 150, 0.25), ("dead", 30, 0.35),
                ("ash", 30, 0.05), ("crystal", 185, 0.6), ("rot", 280, 0.35), ("bone", 45, 0.25), ("honey", 42, 0.75),
                ("cloud", 200, 0.15), ("void", 265, 0.5), ("fairy", 300, 0.45), ("hourglass", 40, 0.45), ("mushroom", 12, 0.55),
                ("coral", 350, 0.6), ("flower", 320, 0.6), ("berry", 350, 0.55), ("garlic", 60, 0.2), ("cactus", 110, 0.45),
                ("salt", 200, 0.05), ("wheat", 48, 0.7), ("reed", 85, 0.45), ("gold", 48, 0.8), ("silver", 210, 0.1),
                ("copper", 25, 0.65), ("iron", 15, 0.3), ("obsidian", 270, 0.3), ("skyiron", 200, 0.55), ("starore", 55, 0.9)]

def feature_hue(fid):
    for key, hue, sat in FEATURE_HUES:
        if key in fid: return hue, sat
    return 100 + h(fid, 40), 0.45

def feature_sprite(f):
    k = f["kind"]; c, sat = feature_hue(f["id"])
    col = tuple(int(v) for v in hsl(c / 360, sat, 0.35)) + (255,)
    lite = tuple(int(v) for v in hsl(c / 360, sat, 0.55)) + (255,)
    if k == "tree":
        im = Image.new("RGBA", (5, 7), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
        d.line([2, 4, 2, 6], fill=(92, 64, 40, 255)); d.ellipse([0, 0, 4, 4], fill=col); d.point((1, 1), fill=lite)
    elif k == "ore":
        im = Image.new("RGBA", (3, 3), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
        d.rectangle([0, 1, 2, 2], fill=(110, 110, 110, 255)); d.point((1, 1), fill=lite); d.point((2, 0), fill=lite)
    else:
        im = Image.new("RGBA", (3, 3), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
        d.point((1, 2), fill=col); d.point((0, 1), fill=col); d.point((2, 1), fill=col); d.point((1, 0), fill=lite)
    return im

def hsl(hh, s, l):
    import colorsys; r, g_, b = colorsys.hls_to_rgb(hh, l, s); return (r * 255, g_ * 255, b * 255)

# ---- 3x5 piksel font (ikon harfleri)
FONT = {c: v for c, v in zip("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", [
 "111101111101101","110101110101110","111100100100111","110101101101110","111100110100111","111100110100100","111100101101111","101101111101101",
 "111010010010111","001001001101111","101110100110101","100100100100111","101111111101101","111101101101101","111101101101111","111101111100100",
 "111101101111001","111101110101101","111100111001111","111010010010010","101101101101111","101101101101010","101101111111101","101101010101101",
 "101101111010010","111001010100111","111101101101111","010110010010111","111001111100111","111001111001111","101101111001001","111100111001111",
 "111100111101111","111001001001001","111101111101111","111101111001111"])}
def text(d, s, x, y, col):
    for i, ch in enumerate(s.upper()[:3]):
        bits = FONT.get(ch)
        if not bits: continue
        for j, b in enumerate(bits):
            if b == "1": d.point((x + i * 4 + j % 3, y + j // 3), fill=col)

TAB_COL = {"world": (86, 142, 60), "civ": (186, 120, 60), "creatures": (180, 90, 120), "nature": (60, 120, 180), "destruction": (190, 60, 50), "other": (110, 110, 130)}
RAR_COL = {"normal": (140, 140, 140), "rare": (70, 130, 210), "epic": (150, 80, 200), "legendary": (230, 170, 40)}

# ---- ikon piktogramları (EK C 8.1: tek nesne, yazı yok; aile başına ortak şekil dili) ----
def _scale(s):
    def t(*xy): return [max(0, min(s - 1, int(round(v * s / 16)))) for v in xy]
    return t

def _g_meteor(d, s, fg, bg, t):
    d.line(t(3, 2, 9, 8), fill=fg, width=max(1, s // 8)); d.point(t(5, 3), fill=fg); d.point(t(2, 5), fill=fg)
    d.ellipse(t(8, 8, 14, 14), fill=fg); d.point(t(10, 10), fill=bg)

def _g_flame(d, s, fg, bg, t):
    d.polygon(t(8, 1, 12, 7, 11, 13, 5, 13, 4, 7), fill=fg)
    d.polygon(t(8, 6, 10, 10, 8, 13, 7, 10), fill=bg)

def _g_bolt(d, s, fg, bg, t):
    a = t(10, 1, 5, 8); d.line(a, fill=fg, width=max(1, s // 8))
    d.line(t(5, 8, 9, 8), fill=fg, width=max(1, s // 8)); d.line(t(9, 8, 6, 15), fill=fg, width=max(1, s // 8))

def _g_drop(d, s, fg, bg, t):
    d.polygon(t(8, 1, 12, 7, 4, 7), fill=fg); d.ellipse(t(4, 6, 12, 14), fill=fg); d.point(t(6, 9), fill=bg)

def _g_snow(d, s, fg, bg, t):
    for x0, y0, x1, y1 in ((8, 1, 8, 15), (2, 4, 14, 12), (2, 12, 14, 4)):
        d.line(t(x0, y0, x1, y1), fill=fg, width=max(1, s // 8))

def _g_tree(d, s, fg, bg, t):
    d.rectangle(t(7, 9, 9, 15), fill=fg); d.ellipse(t(3, 1, 13, 10), fill=fg); d.point(t(5, 4), fill=bg)

def _g_flower(d, s, fg, bg, t):
    d.line(t(8, 8, 8, 15), fill=fg); d.line(t(8, 11, 11, 13), fill=fg)
    for x, y in ((5, 3), (11, 3), (5, 8), (11, 8)): d.ellipse(t(x, y, x + 4, y + 4), fill=fg)
    d.ellipse(t(6, 5, 10, 9), fill=bg); d.point(t(8, 7), fill=fg)

def _g_cross(d, s, fg, bg, t):
    d.rectangle(t(6, 2, 10, 14), fill=fg); d.rectangle(t(2, 6, 14, 10), fill=fg)

def _g_skull(d, s, fg, bg, t):
    d.ellipse(t(3, 1, 13, 11), fill=fg); d.rectangle(t(5, 10, 11, 14), fill=fg)
    d.ellipse(t(5, 5, 7, 8), fill=bg); d.ellipse(t(9, 5, 11, 8), fill=bg)
    d.line(t(7, 11, 7, 13), fill=bg); d.line(t(9, 11, 9, 13), fill=bg)

def _g_sword(d, s, fg, bg, t):
    d.line(t(4, 12, 12, 4), fill=fg, width=max(1, s // 7)); d.line(t(3, 9, 7, 13), fill=fg, width=max(1, s // 8))
    d.point(t(13, 3), fill=fg)

def _g_shield(d, s, fg, bg, t):
    d.polygon(t(8, 1, 14, 4, 12, 11, 8, 15, 4, 11, 2, 4), fill=fg)
    d.polygon(t(8, 4, 11, 6, 10, 10, 8, 12, 6, 10, 5, 6), fill=bg)

def _g_coin(d, s, fg, bg, t):
    d.ellipse(t(2, 2, 14, 14), fill=fg); d.ellipse(t(5, 5, 11, 11), fill=bg); d.point(t(8, 8), fill=fg)

def _g_wheat(d, s, fg, bg, t):
    d.line(t(8, 4, 8, 15), fill=fg)
    for y in (5, 8, 11): d.line(t(5, y, 11, y - 1), fill=fg, width=max(1, s // 9))

def _g_mountain(d, s, fg, bg, t):
    d.polygon(t(1, 14, 6, 4, 9, 9, 11, 6, 15, 14), fill=fg); d.point(t(6, 6), fill=bg)

def _g_sun(d, s, fg, bg, t):
    d.ellipse(t(5, 5, 11, 11), fill=fg)
    for x0, y0, x1, y1 in ((8, 0, 8, 3), (8, 13, 8, 15), (0, 8, 3, 8), (13, 8, 15, 8), (2, 2, 4, 4), (12, 2, 14, 4), (2, 14, 4, 12), (12, 14, 14, 12)):
        d.line(t(x0, y0, x1, y1), fill=fg, width=max(1, s // 9))

def _g_moon(d, s, fg, bg, t):
    d.ellipse(t(2, 2, 12, 14), fill=fg); d.ellipse(t(6, 1, 15, 11), fill=bg)

def _g_cloud(d, s, fg, bg, t):
    d.ellipse(t(2, 7, 8, 13), fill=fg); d.ellipse(t(5, 3, 12, 10), fill=fg); d.ellipse(t(9, 7, 15, 13), fill=fg)
    d.rectangle(t(3, 9, 14, 13), fill=fg)

def _g_star(d, s, fg, bg, t):
    d.polygon(t(8, 0, 10, 6, 16, 6, 11, 10, 13, 16, 8, 12, 3, 16, 5, 10, 0, 6, 6, 6), fill=fg)

def _g_crown(d, s, fg, bg, t):
    d.polygon(t(2, 12, 2, 5, 6, 8, 8, 2, 10, 8, 14, 5, 14, 12), fill=fg); d.rectangle(t(2, 12, 14, 14), fill=fg)

def _g_house(d, s, fg, bg, t):
    d.polygon(t(8, 1, 15, 8, 1, 8), fill=fg); d.rectangle(t(4, 8, 12, 15), fill=fg); d.rectangle(t(7, 11, 9, 15), fill=bg)

def _g_up(d, s, fg, bg, t):
    d.polygon(t(8, 1, 14, 8, 10, 8, 10, 15, 6, 15, 6, 8, 2, 8), fill=fg)

def _g_down(d, s, fg, bg, t):
    d.polygon(t(8, 15, 14, 8, 10, 8, 10, 1, 6, 1, 6, 8, 2, 8), fill=fg)

def _g_eye(d, s, fg, bg, t):
    d.polygon(t(1, 8, 8, 2, 15, 8, 8, 14), fill=fg); d.ellipse(t(5, 5, 11, 11), fill=bg); d.ellipse(t(7, 7, 9, 9), fill=fg)

def _g_bomb(d, s, fg, bg, t):
    d.ellipse(t(3, 6, 13, 15), fill=fg); d.line(t(10, 5, 13, 2), fill=fg); d.point(t(14, 1), fill=fg)

def _g_wind(d, s, fg, bg, t):
    d.line(t(1, 4, 11, 4), fill=fg, width=max(1, s // 8)); d.line(t(4, 8, 14, 8), fill=fg, width=max(1, s // 8))
    d.line(t(1, 12, 10, 12), fill=fg, width=max(1, s // 8)); d.point(t(13, 4), fill=fg)

def _g_portal(d, s, fg, bg, t):
    d.ellipse(t(2, 2, 14, 14), fill=fg); d.ellipse(t(5, 5, 11, 11), fill=bg); d.ellipse(t(7, 7, 9, 9), fill=fg)

def _g_beast(d, s, fg, bg, t):
    d.ellipse(t(2, 8, 12, 13), fill=fg); d.ellipse(t(9, 2, 15, 8), fill=fg)
    d.line(t(4, 13, 4, 15), fill=fg); d.line(t(9, 13, 9, 15), fill=fg); d.point(t(1, 7), fill=fg)
    d.point(t(12, 4), fill=bg)

def _g_heart(d, s, fg, bg, t):
    d.ellipse(t(2, 2, 8, 8), fill=fg); d.ellipse(t(8, 2, 14, 8), fill=fg)
    d.polygon(t(2, 6, 14, 6, 8, 15), fill=fg); d.point(t(8, 10), fill=bg)

def _g_rune(d, s, fg, bg, t):
    d.polygon(t(8, 1, 15, 8, 8, 15, 1, 8), fill=fg); d.polygon(t(8, 5, 11, 8, 8, 11, 5, 8), fill=bg); d.point(t(8, 8), fill=fg)

def _g_orb(d, s, fg, bg, t):
    d.ellipse(t(3, 3, 13, 13), fill=fg); d.point(t(6, 6), fill=bg); d.point(t(5, 5), fill=fg)

GLYPHS = {"meteor": _g_meteor, "flame": _g_flame, "bolt": _g_bolt, "drop": _g_drop, "snow": _g_snow, "tree": _g_tree,
          "flower": _g_flower, "cross": _g_cross, "skull": _g_skull, "sword": _g_sword, "shield": _g_shield, "coin": _g_coin,
          "wheat": _g_wheat, "mountain": _g_mountain, "sun": _g_sun, "moon": _g_moon, "cloud": _g_cloud, "star": _g_star,
          "crown": _g_crown, "house": _g_house, "up": _g_up, "down": _g_down, "eye": _g_eye, "bomb": _g_bomb,
          "wind": _g_wind, "portal": _g_portal, "beast": _g_beast, "heart": _g_heart, "rune": _g_rune, "orb": _g_orb}

# sıra önemli: özel anahtar kelimeler genel olanlardan önce taranır
GLYPH_KEYS = [
 (("plague", "poison", "disease", "curse", "rot", "undead", "zombie", "skeleton", "death", "necro"), "skull"),
 (("meteor", "comet", "starfall"), "meteor"),
 (("lava", "volcan", "fire", "flame", "burn", "ember", "inferno", "pyre", "bonfire", "smith"), "flame"),
 (("lightning", "shock", "storm", "thunder"), "bolt"),
 (("rain", "flood", "water", "drip", "geyser", "fountain", "tide"), "drop"),
 (("ice", "snow", "frost", "freeze", "blizzard"), "snow"),
 (("seed", "grow", "forest", "tree", "wood", "garden", "grass", "vine", "spring"), "tree"),
 (("flower", "bloom", "leaf", "bee", "honey", "fairy"), "flower"),
 (("heal", "cure", "medicine", "purify", "bless", "holy"), "cross"),
 (("sword", "war", "battle", "attack", "duel", "assassin", "raid"), "sword"),
 (("shield", "protect", "wall", "barrier", "fortify", "defense"), "shield"),
 (("coin", "gold", "trade", "market", "tax", "rich", "treasure", "festival", "gift"), "coin"),
 (("wheat", "food", "bread", "farm", "harvest", "grain", "feast"), "wheat"),
 (("mountain", "rock", "stone", "ore", "mine", "quake", "earth", "soil", "sand"), "mountain"),
 (("sun", "light", "divine", "radiant", "angel"), "sun"),
 (("moon", "night", "shadow", "lunar"), "moon"),
 (("cloud", "weather", "fog", "mist"), "cloud"),
 (("star", "wish", "celestial", "cosmic"), "star"),
 (("crown", "king", "ruler", "command", "throne"), "crown"),
 (("house", "build", "found", "city", "settle", "village"), "house"),
 (("boost", "buff", "strengthen", "haste", "speed", "fertility"), "up"),
 (("slow", "weak", "weaken", "aging", "decay", "drain"), "down"),
 (("eye", "reveal", "scout", "vision", "inspect", "oracle"), "eye"),
 (("bomb", "explode", "blast", "nuke"), "bomb"),
 (("tornado", "wind", "hurricane", "gale"), "wind"),
 (("portal", "rift", "teleport", "warp", "gate"), "portal"),
 (("spawn", "creature", "animal", "beast", "summon", "herd", "wild"), "beast"),
 (("love", "peace", "friend", "alliance", "charm", "happiness"), "heart"),
 (("mana", "magic", "arcane", "spell", "enchant", "essence", "crystal", "timewarp"), "rune"),
]
DEFAULT_GLYPHS = ("orb", "star", "rune", "cloud")

def glyph_for(pid):
    for keys, g in GLYPH_KEYS:
        if any(k in pid for k in keys): return g
    return DEFAULT_GLYPHS[h(pid, 1 << 30) % len(DEFAULT_GLYPHS)]

FG_ICON = (246, 242, 232, 255)

def icon(size, label, bg, frame=None, glyph=None):
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.rounded_rectangle([0, 0, size - 1, size - 1], radius=2, fill=bg + (255,), outline=(frame or (28, 26, 24)) + (255,))
    if glyph:
        GLYPHS[glyph](d, size, FG_ICON, bg + (255,), _scale(size)); return im
    words = [w for w in label.replace("_", " ").split() if w]
    lab = "".join(w[0] for w in words[:3]) if len(words) > 1 else label[:3]
    n = min(3, len(lab)); tw = n * 4 - 1
    text(d, lab, (size - tw) // 2, (size - 5) // 2, (250, 250, 250, 255)); return im

PAL = [(0, 0, 0, 0), (30, 24, 28), (70, 52, 44), (120, 90, 70), (170, 132, 100), (40, 60, 120), (70, 100, 180), (230, 60, 40), (250, 240, 200)]
def colorize(im):
    out = Image.new("RGBA", im.size, (0, 0, 0, 0)); px, po = im.load(), out.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g_, b, a = px[x, y]
            if a: po[x, y] = PAL[min(8, max(1, round(r / 28)))] + (255,)
    return out

def main():
    for sub in ("units", "buildings", "features", "icons/powers", "icons/traits", "icons/status"):
        os.makedirs(os.path.join(OUT, sub), exist_ok=True)
    rows, previews = [], []
    for sp in load("species"):
        sheet, meta, (w, h_), plan = unit_sheet(sp)
        base = os.path.join(OUT, "units", sp["id"][3:])
        sheet.save(base + ".png"); json.dump(meta, open(base + ".json", "w"), indent=1)
        rows.append(["unit", sp["id"], sp["name"], f"{w}x{h_}", plan, "idle2 walk4 attack3 swim2 sleep1 death3", 3])
        previews.append(colorize(sheet.crop((0, 0, w * 4, h_ * 2))))  # idle + walk kareleri
    blds = load("buildings")
    for style in STYLES:
        for b in blds:
            if b["id"] == "bld.ruins": continue
            for state in ("construction", "complete", "ruin"):
                im = building_sprite(b, style, state)
                im.save(os.path.join(OUT, "buildings", f"{style}_{b['id'][4:]}_{state}.png"))
            rows.append(["building", b["id"], b["name"], f"{im.width}x{im.height}", style, "construction complete ruin", 4])
    for f in load("features"):
        im = feature_sprite(f); im.save(os.path.join(OUT, "features", f["id"][5:] + ".png"))
        rows.append(["feature", f["id"], f["name"], f"{im.width}x{im.height}", f["kind"], "evre 0-3 (ölçek)", 2])
    icon_previews = []
    for p in load("powers"):
        icon(16, p["id"][3:], TAB_COL[p["tab"]], glyph=glyph_for(p["id"][3:])).save(os.path.join(OUT, "icons/powers", p["id"][3:] + ".png"))
        rows.append(["icon_power", p["id"], p["name"], "16x16", p["tab"], "-", 1])
        if len(icon_previews) < 72: icon_previews.append((p["id"][3:], TAB_COL[p["tab"]]))
    for fn, frame_key in [("unit_traits", "rarity"), ("subspecies_traits", None), ("culture_traits", None), ("religion_traits", None),
                          ("language_traits", None), ("clan_traits", None), ("kingdom_traits", None), ("genes", None)]:
        for t in load(fn):
            fr = RAR_COL.get(t.get("rarity", "normal"))
            icon(12, t["id"].split(".", 1)[1], tuple(int(v) for v in hsl(h(fn, 360) / 360, 0.35, 0.3)), fr,
                 glyph=glyph_for(t["id"].split(".", 1)[1])).save(
                os.path.join(OUT, "icons/traits", t["id"].replace(".", "_") + ".png"))
            rows.append(["icon_trait", t["id"], t["name"], "12x12", fn, "-", 9])
    for s in load("status_effects"):
        icon(7, s["id"][3:4], (60, 60, 60)).save(os.path.join(OUT, "icons/status", s["id"][3:] + ".png"))
        rows.append(["icon_status", s["id"], s["name"], "7x7", "-", "-", 3])
    with open(os.path.join(OUT, "asset_list.csv"), "w", newline="", encoding="utf-8") as fh:
        w = csv.writer(fh); w.writerow(["kategori", "id", "ad", "boyut_px", "tip/stil", "animasyon/durum", "ilk_gereken_faz"]); w.writerows(rows)
    # önizleme: ilk 40 birimin yürüme karesi, 6x büyütülmüş
    sc, pad = 8, 6; sel = previews[:48]; cols = 12
    cw = max(p.width for p in sel) * sc + pad; ch = max(p.height for p in sel) * sc + pad
    pv = Image.new("RGBA", (cw * cols, ch * math.ceil(len(sel) / cols)), (46, 52, 40, 255))
    for i, p in enumerate(sel):
        big = p.resize((p.width * sc, p.height * sc), Image.NEAREST); pv.paste(big, ((i % cols) * cw + 2, (i // cols) * ch + 2), big)
    pv.save(os.path.join(OUT, "preview_units.png"))
    # bina önizlemesi: oyundaki paletle (BuildingRenderer.WritePalette), stil başına bir satır, 3x
    sdefs = {x["id"][6:]: x for x in load("building_styles")}
    show = ("hut", "house", "manor", "hall_1", "hall_3", "temple", "barracks", "well", "watchtower", "windmill", "market", "tent")
    bl = {x["id"][4:]: x for x in blds}
    sc = 3; rows_ = []
    for s_ in STYLES:
        rows_.append([bcolorize(building_sprite(bl[k], s_, "complete"), building_preview_colors(sdefs[s_])) for k in show])
    rows_.append([bcolorize(building_sprite(bl[k], "human", st), building_preview_colors(sdefs["human"])) for k in ("house", "hall_2") for st in ("construction", "ruin")])
    rh = [max(i.height for i in r) * sc + 6 for r in rows_]
    tw = max(sum(i.width * sc + 6 for i in r) for r in rows_)
    pv2 = Image.new("RGBA", (tw, sum(rh)), (74, 112, 58, 255)); y = 0
    for r, hh in zip(rows_, rh):
        x = 3
        for im in r:
            big = im.resize((im.width * sc, im.height * sc), Image.NEAREST); pv2.paste(big, (x, y + hh - 3 - big.height), big); x += big.width + 6
        y += hh
    pv2.save(os.path.join(OUT, "preview_buildings.png"))
    # ikon önizlemesi: ilk 72 güç piktogramı, 5x
    sc = 5; cols = 12
    pv3 = Image.new("RGBA", (cols * (16 * sc + 4) + 4, math.ceil(len(icon_previews) / cols) * (16 * sc + 4) + 4), (46, 52, 40, 255))
    for i, (pid, bg) in enumerate(icon_previews):
        im = icon(16, pid, bg, glyph=glyph_for(pid))
        big = im.resize((16 * sc, 16 * sc), Image.NEAREST)
        pv3.paste(big, (4 + (i % cols) * (16 * sc + 4), 4 + (i // cols) * (16 * sc + 4)), big)
    pv3.save(os.path.join(OUT, "preview_icons.png"))
    print(len(rows), "varlık üretildi")

if __name__ == "__main__":
    main()
