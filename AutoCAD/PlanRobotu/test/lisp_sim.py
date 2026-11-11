"""Minimal AutoLISP subset interpreter used to test PlanRobotu.lsp without AutoCAD.

Only the language features and built-ins used by the geometry/report core are
implemented. AutoCAD/DCL specific functions are not available; the tests call
`pr:plan-uret` and `pr:son-islem` directly and render the produced primitives
to SVG.
"""
import math
import re

NIL = None


class Sym(str):
    pass


T = Sym("t")


class Dotted:
    def __init__(self, a, b):
        self.a, self.b = a, b


class LispError(Exception):
    pass


# --------------------------------------------------------------------- reader
TOKEN = re.compile(r'''\s*(?:(;[^\n]*)|("(?:\\.|[^"\\])*")|([()'])|([^\s()'";]+))''', re.S)


def tokenize(src):
    pos, out = 0, []
    while pos < len(src):
        m = TOKEN.match(src, pos)
        if not m or m.end() == pos:
            if src[pos:].strip() == "":
                break
            raise LispError("tokenize error at %d: %r" % (pos, src[pos:pos + 30]))
        pos = m.end()
        if m.group(1):
            continue
        if m.group(2):
            s = m.group(2)[1:-1]
            s = re.sub(r'\\(.)', lambda mm: {'n': '\n', 't': '\t', 'r': '\r'}.get(mm.group(1), mm.group(1)), s)
            out.append(("str", s))
        elif m.group(3):
            out.append(("p", m.group(3)))
        elif m.group(4):
            out.append(("atom", m.group(4)))
    return out


def atom(tok):
    try:
        return int(tok)
    except ValueError:
        pass
    try:
        return float(tok)
    except ValueError:
        pass
    low = tok.lower()
    if low == "nil":
        return NIL
    return Sym(low)


def read_all(src):
    toks = tokenize(src)
    i = 0

    def rd():
        nonlocal i
        if i >= len(toks):
            raise LispError("unexpected EOF (unbalanced parentheses)")
        kind, v = toks[i]
        i += 1
        if kind == "str":
            return v
        if kind == "p" and v == "'":
            return [Sym("quote"), rd()]
        if kind == "p" and v == "(":
            items = []
            while True:
                if i >= len(toks):
                    raise LispError("unexpected EOF in list")
                if toks[i] == ("p", ")"):
                    i += 1
                    break
                if toks[i] == ("atom", "."):
                    i += 1
                    b = rd()
                    if toks[i] != ("p", ")"):
                        raise LispError("bad dotted pair")
                    i += 1
                    return Dotted(items[0], b)
                items.append(rd())
            return items if items else NIL
        if kind == "p" and v == ")":
            raise LispError("unexpected ')' at token %d" % i)
        return atom(v)

    forms = []
    while i < len(toks):
        forms.append(rd())
    return forms


# ------------------------------------------------------------------ evaluator
class Func:
    def __init__(self, name, params, locs, body):
        self.name, self.params, self.locs, self.body = name, params, locs, body


def parse_params(lst):
    params, locs, cur = [], [], None
    cur = params
    for x in (lst or []):
        if x == "/":
            cur = locs
        else:
            cur.append(x)
    return params, locs


def truthy(v):
    return v is not None


def L(lst):
    return lst if lst else NIL


class Interp:
    def __init__(self):
        self.g = {}
        self.f = {}
        self.out = []
        self._builtins()

    # dynamic scoping as in AutoLISP
    def call(self, fn, args):
        if callable(fn):
            return fn(*args)
        names = fn.params + fn.locs
        saved = {n: self.g.get(n, NIL) for n in names}
        if len(args) != len(fn.params):
            raise LispError("%s: expected %d args, got %d" % (fn.name, len(fn.params), len(args)))
        for n in fn.locs:
            self.g[n] = NIL
        for n, a in zip(fn.params, args):
            self.g[n] = a
        try:
            r = NIL
            for b in fn.body:
                r = self.ev(b)
            return r
        finally:
            self.g.update(saved)

    def to_fn(self, v):
        if isinstance(v, Func) or callable(v):
            return v
        if isinstance(v, Sym):
            if v in self.f:
                return self.f[v]
            raise LispError("undefined function " + v)
        if isinstance(v, list) and v and v[0] == "lambda":
            p, l = parse_params(v[1])
            return Func("lambda", p, l, v[2:])
        raise LispError("not a function: %r" % (v,))

    def ev(self, x):
        if isinstance(x, Sym):
            if x == "t":
                return T
            return self.g.get(x, NIL)
        if not isinstance(x, list):
            return x
        op = x[0]
        if isinstance(op, Sym):
            sf = getattr(self, "sf_" + op.replace("-", "_").replace(":", "_"), None) if op in SPECIAL else None
            if sf:
                return sf(x)
            fn = self.f.get(op)
            if fn is None:
                raise LispError("undefined function: " + op)
        else:
            fn = self.to_fn(self.ev(op))
        args = [self.ev(a) for a in x[1:]]
        return self.call(fn, args)

    # ---- special forms
    def sf_quote(self, x):
        return x[1]

    def sf_function(self, x):
        return x[1]

    def sf_setq(self, x):
        r = NIL
        for i in range(1, len(x), 2):
            r = self.ev(x[i + 1])
            self.g[x[i]] = r
        return r

    def sf_defun(self, x):
        p, l = parse_params(x[2])
        self.f[x[1]] = Func(x[1], p, l, x[3:])
        return x[1]

    def sf_lambda(self, x):
        p, l = parse_params(x[1])
        return Func("lambda", p, l, x[2:])

    def sf_if(self, x):
        if truthy(self.ev(x[1])):
            return self.ev(x[2])
        return self.ev(x[3]) if len(x) > 3 else NIL

    def sf_cond(self, x):
        for cl in x[1:]:
            v = self.ev(cl[0])
            if truthy(v):
                r = v
                for b in cl[1:]:
                    r = self.ev(b)
                return r
        return NIL

    def sf_progn(self, x):
        r = NIL
        for b in x[1:]:
            r = self.ev(b)
        return r

    def sf_while(self, x):
        r = NIL
        n = 0
        while truthy(self.ev(x[1])):
            for b in x[2:]:
                r = self.ev(b)
            n += 1
            if n > 10_000_000:
                raise LispError("infinite loop?")
        return r

    def sf_repeat(self, x):
        r = NIL
        for _ in range(int(self.ev(x[1]))):
            for b in x[2:]:
                r = self.ev(b)
        return r

    def sf_foreach(self, x):
        var = x[1]
        saved = self.g.get(var, NIL)
        r = NIL
        try:
            for item in (self.ev(x[2]) or []):
                self.g[var] = item
                for b in x[3:]:
                    r = self.ev(b)
        finally:
            self.g[var] = saved
        return r

    def sf_and(self, x):
        for b in x[1:]:
            if not truthy(self.ev(b)):
                return NIL
        return T

    def sf_or(self, x):
        for b in x[1:]:
            if truthy(self.ev(b)):
                return T
        return NIL

    # ---- builtins
    def _builtins(self):
        f = self.f

        def cxr(path):
            def g(v):
                for c in reversed(path):
                    if v is None:
                        return NIL
                    if isinstance(v, Dotted):
                        v = v.a if c == "a" else v.b
                    elif c == "a":
                        v = v[0]
                    else:
                        v = L(v[1:])
                return v
            return g

        for p in ["a", "d", "aa", "ad", "da", "dd", "add", "ddd", "addd"]:
            f[Sym("c%sr" % p)] = cxr(p)

        def num_eq(a, b):
            return a == b

        def lt(*a):
            return T if all(a[i] < a[i + 1] for i in range(len(a) - 1)) else NIL

        f["list"] = lambda *a: L(list(a))
        f["cons"] = lambda a, b: Dotted(a, b) if (b is not None and not isinstance(b, list)) else L([a] + (b or []))
        f["append"] = lambda *a: L([y for x in a for y in (x or [])])
        f["reverse"] = lambda a: L(list(reversed(a or [])))
        f["length"] = lambda a: len(a or [])
        f["nth"] = lambda i, a: (a or [])[i] if i < len(a or []) else NIL
        f["last"] = lambda a: a[-1] if a else NIL
        f["assoc"] = lambda k, a: next((e for e in (a or []) if (e.a if isinstance(e, Dotted) else e[0]) == k), NIL)
        f["member"] = lambda k, a: next((L(a[i:]) for i in range(len(a or [])) if equal(a[i], k)), NIL)
        f["subst"] = lambda new, old, a: L([new if equal(e, old) else e for e in (a or [])])
        f["vl-position"] = lambda k, a: next((i for i, e in enumerate(a or []) if equal(e, k)), NIL)
        f["vl-sort"] = self._vl_sort
        f["mapcar"] = self._mapcar
        f["apply"] = lambda fn, a: self.call(self.to_fn(fn), list(a or []))
        f["+"] = lambda *a: sum(a) if a else 0
        f["*"] = lambda *a: math.prod(a)
        f["-"] = lambda *a: -a[0] if len(a) == 1 else a[0] - sum(a[1:])
        f["/"] = self._div
        f["1+"] = lambda a: a + 1
        f["1-"] = lambda a: a - 1
        f["fix"] = lambda a: int(a)
        f["float"] = lambda a: float(a)
        f["abs"] = abs
        f["min"] = lambda *a: min(a)
        f["max"] = lambda *a: max(a)
        f["sqrt"] = lambda a: math.sqrt(a)
        f["rem"] = lambda a, b: math.fmod(a, b) if isinstance(a, float) or isinstance(b, float) else int(math.fmod(a, b))
        f["="] = lambda *a: T if all(num_eq(a[0], b) for b in a[1:]) else NIL
        f["/="] = lambda a, b: NIL if a == b else T
        f["<"] = lt
        f[">"] = lambda *a: lt(*reversed(a))
        f["<="] = lambda *a: T if all(a[i] <= a[i + 1] for i in range(len(a) - 1)) else NIL
        f[">="] = lambda *a: T if all(a[i] >= a[i + 1] for i in range(len(a) - 1)) else NIL
        f["not"] = lambda a: T if a is None else NIL
        f["null"] = f["not"]
        f["equal"] = lambda a, b, fz=0: T if equal(a, b, fz) else NIL
        f["strcat"] = lambda *a: "".join(a)
        f["itoa"] = lambda a: str(int(a))
        f["atoi"] = lambda s: int(re.match(r'\s*-?\d*', s).group(0) or 0) if re.match(r'\s*-?\d', s) else 0
        f["princ"] = lambda *a: (self.out.append(a[0] if a else ""), a[0] if a else NIL)[1]
        f["vl-load-com"] = lambda: NIL
        f["angle"] = lambda a, b: math.atan2(b[1] - a[1], b[0] - a[0]) % (2 * math.pi)

    def _div(self, *a):
        r = a[0]
        for b in a[1:]:
            if isinstance(r, int) and isinstance(b, int):
                q = abs(r) // abs(b)
                r = q if (r >= 0) == (b >= 0) else -q
            else:
                r = r / b
        return r

    def _vl_sort(self, lst, fn):
        import functools
        fn = self.to_fn(fn)
        return L(sorted(lst or [], key=functools.cmp_to_key(
            lambda x, y: -1 if truthy(self.call(fn, [x, y])) else (1 if truthy(self.call(fn, [y, x])) else 0))))

    def _mapcar(self, fn, *lists):
        fn = self.to_fn(fn)
        n = min(len(l or []) for l in lists)
        return L([self.call(fn, [l[i] for l in lists]) for i in range(n)])

    def load(self, path, skip=()):
        src = open(path, encoding="utf-8").read()
        for form in read_all(src):
            if isinstance(form, list) and form and form[0] in ("defun", "setq", "if"):
                if form[0] == "defun" and form[1] in skip:
                    continue
                self.ev(form)

    def fn(self, name, *args):
        return self.call(self.f[Sym(name)], list(args))


SPECIAL = {"quote", "function", "setq", "defun", "lambda", "if", "cond", "progn", "while",
           "repeat", "foreach", "and", "or"}


def equal(a, b, fz=0):
    if isinstance(a, (int, float)) and isinstance(b, (int, float)):
        return abs(a - b) <= fz
    if isinstance(a, list) and isinstance(b, list):
        return len(a) == len(b) and all(equal(x, y, fz) for x, y in zip(a, b))
    return a == b


def to_lisp(v):
    """Python value -> lisp data (lists, None)."""
    if isinstance(v, (list, tuple)):
        return L([to_lisp(x) for x in v])
    if v is True:
        return T
    if v is False:
        return NIL
    return v
