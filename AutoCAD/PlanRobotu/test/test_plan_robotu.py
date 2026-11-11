"""PlanRobotu.lsp geometri ve rapor cekirdegi testleri (AutoCAD gerektirmez).

Calistirma:  python3 AutoCAD/PlanRobotu/test/test_plan_robotu.py [--svg CIKTI_KLASORU]
Her senaryo icin plan uretilir; mahallerin cakismadigi, ic konturun disina
tasmadigi ve yeterli alanda tum mahallerin UYGUN oldugu dogrulanir.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from lisp_sim import Interp, Sym, to_lisp, NIL  # noqa: E402

LSP = os.path.join(HERE, "..", "PlanRobotu.lsp")

S21 = [1, 2, 1, 1, 1, 0, 1]
S31 = [1, 3, 1, 2, 1, 0, 1]
S11 = [1, 1, 1, 1, 0, 0, 1]
S41 = [1, 4, 1, 2, 1, 1, 2]

# (ad, Lx, Ly, specs, td, ti, tp, asansor, beklenenUygun)
SENARYOLAR = [
    ("2_daire_2+1", 24.0, 12.0, [S21, S21], 0.25, 0.10, 0.20, True, True),
    ("1_daire_3+1", 18.0, 12.0, [S31], 0.25, 0.10, 0.20, True, True),
    ("4_daire_karisik", 30.0, 22.0, [S21, S31, S11, S21], 0.25, 0.10, 0.20, True, True),
    ("6_daire_2+1", 40.0, 22.0, [S21] * 6, 0.30, 0.12, 0.20, True, True),
    ("3_daire_asansorsuz", 28.0, 20.0, [S41, S11, S21], 0.25, 0.10, 0.20, False, True),
    ("sinirda_uygunsuz", 21.0, 10.0, [S31, S31], 0.25, 0.10, 0.20, True, False),
]

# Hic sigmayan alan: plan uretilmemeli, rapor aciklama vermeli
SIGMAYANLAR = [
    ("kucuk_alan", 14.0, 8.0, [S31, S31], 0.25, 0.20, 0.20, True),
    # derinlik > 10 m ama daireler enine sigmiyor (eski surumde 32 bit tasmasi)
    ("dar_derin_alan", 12.0, 10.7, [S31, S31], 0.25, 0.10, 0.20, True),
]


def kur(interp, Lx, Ly, specs, td, ti, tp, asansor):
    par = [["N", len(specs)], ["SPECS", specs], ["TD", td], ["TI", ti], ["TP", tp],
           ["H", 3.0], ["ASANSOR", asansor], ["RAPOR", True], ["LX", Lx], ["LY", Ly]]
    interp.g[Sym("*pr-par*")] = to_lisp(par)


def bbox(pts):
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    return min(xs), min(ys), max(xs), max(ys)


def rect_overlap(a, b, eps=1e-6):
    return (min(a[2], b[2]) - max(a[0], b[0]) > eps) and (min(a[3], b[3]) - max(a[1], b[1]) > eps)


def svg(interp, Lx, Ly, td, path, baslik):
    s = 40.0  # px / m
    pad = 30
    rap = interp.g.get(Sym("*pr-rapor*")) or []
    W = Lx * s + 2 * pad + 620
    H = max(Ly * s + 2 * pad + 30, len(rap) * 14 + 60)

    def X(p):
        return pad + (p[0] + td) * s

    def Y(p):
        return pad + 30 + (Ly - (p[1] + td)) * s

    renk = {"PR-DUVAR": "#111", "PR-KAPI": "#c76b00", "PR-PENCERE": "#0aa", "PR-MERDIVEN": "#a80",
            "PR-YAZI": "#222", "PR-DAIRE": "#c00", "PR-RAPOR": "#666"}
    o = ['<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d" font-family="Arial">' % (W, H),
         '<rect width="100%" height="100%" fill="white"/>',
         '<text x="%d" y="20" font-size="14" font-weight="bold">%s</text>' % (pad, baslik),
         '<rect x="%f" y="%f" width="%f" height="%f" fill="none" stroke="#111" stroke-width="1.5"/>'
         % (pad, pad + 30, Lx * s, Ly * s)]
    for e in reversed(interp.g[Sym("*pr-prims*")] or []):
        k, lay = e[0], e[1]
        c = renk.get(lay, "#000")
        if k == "LN":
            o.append('<line x1="%.2f" y1="%.2f" x2="%.2f" y2="%.2f" stroke="%s" stroke-width="1"/>'
                     % (X(e[2]), Y(e[2]), X(e[3]), Y(e[3]), c))
        elif k == "PL":
            pts = " ".join("%.2f,%.2f" % (X(p), Y(p)) for p in e[2])
            tag = "polygon" if e[3] else "polyline"
            o.append('<%s points="%s" fill="none" stroke="%s" stroke-width="1.2"/>' % (tag, pts, c))
        elif k == "ARC":
            cc, p1, p2 = e[2], e[3], e[4]
            r = ((p1[0] - cc[0]) ** 2 + (p1[1] - cc[1]) ** 2) ** 0.5 * s
            cr = (p1[0] - cc[0]) * (p2[1] - cc[1]) - (p1[1] - cc[1]) * (p2[0] - cc[0])
            sweep = 0 if cr > 0 else 1  # SVG y ekseni ters
            o.append('<path d="M %.2f %.2f A %.2f %.2f 0 0 %d %.2f %.2f" fill="none" stroke="%s" stroke-width="0.8"/>'
                     % (X(p1), Y(p1), r, r, sweep, X(p2), Y(p2), c))
        elif k == "TX":
            h = e[3] * s
            o.append('<text x="%.2f" y="%.2f" font-size="%.1f" fill="%s" text-anchor="middle" '
                     'dominant-baseline="middle">%s</text>' % (X(e[2]), Y(e[2]) - e[5] * s, h, c, e[4]))
    x0 = pad + Lx * s + 20
    for i, line in enumerate(rap):
        o.append('<text x="%d" y="%d" font-size="11" fill="#333">%s</text>' % (x0, pad + 40 + i * 14, line))
    o.append("</svg>")
    with open(path, "w", encoding="utf-8") as fh:
        fh.write("\n".join(o))


def calistir(svg_dir=None):
    from lisp_lint import kontrol
    hatalar = 0
    for x in kontrol(LSP):
        print("[HATA] lint: " + x)
        hatalar += 1
    for ad, Lx, Ly, specs, td, ti, tp, asansor, bekle in SENARYOLAR:
        it = Interp()
        it.load(LSP)
        kur(it, Lx, Ly, specs, td, ti, tp, asansor)
        ok = it.fn("pr:plan-uret", Lx, Ly)
        if ok is NIL:
            print("[%s] plan uretilemedi" % ad)
            hatalar += 1
            continue
        uygunsuz = it.fn("pr:son-islem")
        mahaller = it.g[Sym("*pr-mahaller*")] or []
        Lin, Din = Lx - 2 * td, Ly - 2 * td
        sorun = []
        kutular = []
        for m in mahaller:
            bb = bbox(m[2])
            if bb[0] < -1e-6 or bb[1] < -1e-6 or bb[2] > Lin + 1e-6 or bb[3] > Din + 1e-6:
                sorun.append("%s ic konturun disinda %s" % (m[1], bb))
            if m[0] != "ANTRE":  # L bicimli antre/koridor kutusu ile cakisma testi yapilmaz
                kutular.append((m[1], m[5], bb))
        for i in range(len(kutular)):
            for j in range(i + 1, len(kutular)):
                if rect_overlap(kutular[i][2], kutular[j][2]):
                    sorun.append("cakisma: %s/%s - %s/%s" % (kutular[i][1], kutular[i][0], kutular[j][1], kutular[j][0]))
        daire_sayisi = len(it.g[Sym("*pr-daireler*")] or [])
        if bekle and daire_sayisi != len(specs):
            sorun.append("cizilen daire %d != %d" % (daire_sayisi, len(specs)))
        if bekle and uygunsuz != 0:
            sorun.append("uygunsuzluk beklenmiyordu (%d)" % uygunsuz)
        if not bekle and uygunsuz == 0:
            sorun.append("uygunsuzluk bekleniyordu")
        durum = "OK " if not sorun else "HATA"
        print("[%s] %s  duzen=%s mahal=%d uygunsuz=%d" % (durum, ad, it.g[Sym("*pr-duzen*")][0],
                                                          len(mahaller), uygunsuz))
        for s_ in sorun:
            print("      - " + s_)
        hatalar += bool(sorun)
        if svg_dir:
            os.makedirs(svg_dir, exist_ok=True)
            svg(it, Lx, Ly, td, os.path.join(svg_dir, ad + ".svg"),
                "%s  (%.1f x %.1f m)" % (ad, Lx, Ly))
    for sg in SIGMAYANLAR:
        it = Interp()
        it.load(LSP)
        kur(it, *sg[1:])
        if it.fn("pr:plan-uret", sg[1], sg[2]) is not NIL:
            print("[HATA] %s: plan uretilmemeliydi" % sg[0])
            hatalar += 1
        else:
            print("[OK ] %s: plan uretilmedi -> %s" % (sg[0], (it.g[Sym("*pr-rapor*")] or ["?"])[-1]))
    # Bant genislikleri her zaman daire genisligini tam doldurmali
    it = Interp()
    it.load(LSP)
    kur(it, 28.0, 20.0, [S41], 0.25, 0.10, 0.20, False)
    g = it.fn("pr:bant-dagit", to_lisp([[c for c in o] for o in it.fn("pr:adaylar", to_lisp(S41))[0][0]]),
              to_lisp([4.0, 4.0, 4.0, 4.0]), 27.0, NIL)
    if g is NIL or abs(sum(g) - 27.0) > 1e-6:
        print("[HATA] bant-dagit genisligi doldurmuyor: %r" % (g,))
        hatalar += 1
    else:
        print("[OK ] bant-dagit genisligi tam dolduruyor (%s)" % ", ".join("%.2f" % x for x in g))
    print("\nToplam hatali senaryo: %d" % hatalar)
    return hatalar


if __name__ == "__main__":
    d = None
    if "--svg" in sys.argv:
        d = sys.argv[sys.argv.index("--svg") + 1]
    sys.exit(1 if calistir(d) else 0)
