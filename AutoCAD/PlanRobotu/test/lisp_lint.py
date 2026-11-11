"""PlanRobotu.lsp icin statik kontroller:
- parantez dengesi (hatali satir numarasiyla)
- ayni fonksiyonda yalniz harf buyuklugu farkli semboller
  (AutoLISP sembolleri buyuk/kucuk harf ayirmaz: W ile w ayni degiskendir)
"""
import re
import sys


def parantez(src):
    derinlik, satir, basla, instr, i = 0, 1, None, False, 0
    hatalar = []
    while i < len(src):
        c = src[i]
        if c == "\n":
            satir += 1
        if instr:
            if c == "\\":
                i += 2
                continue
            if c == '"':
                instr = False
        elif c == ";":
            while i < len(src) and src[i] != "\n":
                i += 1
            continue
        elif c == '"':
            instr = True
        elif c == "(":
            if derinlik == 0:
                basla = satir
            derinlik += 1
        elif c == ")":
            derinlik -= 1
            if derinlik < 0:
                hatalar.append("fazla ')' satir %d" % satir)
                derinlik = 0
        i += 1
    if derinlik:
        hatalar.append("kapanmamis '(' (form satir %s'de basliyor)" % basla)
    return hatalar


def harf(src):
    s = re.sub(r'"(?:\\.|[^"\\])*"', '""', src)
    s = re.sub(r";[^\n]*", "", s)
    hatalar = []
    for parca in re.split(r"\n\(defun ", s)[1:]:
        g = {}
        for t in set(re.findall(r"[A-Za-z][\w:*+-]*", parca)):
            g.setdefault(t.lower(), set()).add(t)
        for k, v in g.items():
            if len(v) > 1:
                hatalar.append("%s: %s" % (parca.split()[0], sorted(v)))
    return hatalar


def kontrol(yol):
    src = open(yol, encoding="utf-8").read()
    return parantez(src) + harf(src)


if __name__ == "__main__":
    h = kontrol(sys.argv[1])
    for x in h:
        print("LINT:", x)
    sys.exit(1 if h else 0)
