"""PlanRobotu.dcl dosyasini PlanRobotu.lsp icindeki *pr-dcl* ve *pr-form-yazilar*
listelerinden uretir.

AutoCAD .dcl dosyalarini Windows ANSI kod sayfasiyla okur. Turkce Windows'ta bu
cp1254'tur; bu yuzden dosya cp1254 olarak yazilir ve Turkce etiketler dogrudan
DCL icine gomulur (LISP ayrica set_tile ile de yazar).

Kullanim:  python3 AutoCAD/PlanRobotu/araclar/dcl_uret.py
"""
import os
import re
import sys

KOK = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(KOK, "test"))
from lisp_sim import Interp, Sym  # noqa: E402


def uret():
    it = Interp()
    it.load(os.path.join(KOK, "PlanRobotu.lsp"))
    satirlar = list(it.g[Sym("*pr-dcl*")])
    yazilar = {k: v for k, v in it.g[Sym("*pr-form-yazilar*")]}
    out = []
    for sat in satirlar:
        def ekle(m):
            k = m.group(1)
            if k in yazilar:
                etiket = yazilar[k].replace('"', '\\"')
                # varsa ASCII label'i degistir, yoksa ekle
                return 'key = "%s"; label = "%s";' % (k, etiket)
            return m.group(0)
        sat = re.sub(r'key = "([^"]+)";(?: label = "[^"]*";)?', ekle, sat)
        out.append(sat)
    metin = ("// Plan Robotu formu - araclar/dcl_uret.py ile uretilir, elle duzenlemeyin.\n"
             "// Kodlama: Windows-1254 (AutoCAD .dcl dosyalarini ANSI okur).\n"
             + "\n".join(out) + "\n")
    return metin


if __name__ == "__main__":
    yol = os.path.join(KOK, "PlanRobotu.dcl")
    with open(yol, "w", encoding="cp1254", newline="\r\n") as f:
        f.write(uret())
    print("yazildi:", yol)
