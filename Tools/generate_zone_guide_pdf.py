#!/usr/bin/env python3
"""Generate the Ruhsat Hesap Archicad zone code guide as a styled PDF."""

from __future__ import annotations

import html
import math
import re
from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    BaseDocTemplate,
    Flowable,
    Frame,
    KeepTogether,
    ListFlowable,
    ListItem,
    PageBreak,
    PageTemplate,
    Paragraph,
    Spacer,
    Table,
    TableStyle,
)


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "ZON-ADLANDIRMA-STANDARDI.md"
OUTPUT = ROOT / "output" / "pdf" / "Ruhsat-Hesap-Zon-Kodlari-Kullanim-Kilavuzu.pdf"

PAGE_WIDTH, PAGE_HEIGHT = A4
LEFT = 19 * mm
RIGHT = 17 * mm
TOP = 19 * mm
BOTTOM = 17 * mm
CONTENT_WIDTH = PAGE_WIDTH - LEFT - RIGHT

INK = colors.HexColor("#17201F")
MUTED = colors.HexColor("#60706C")
GREEN = colors.HexColor("#149B5F")
GREEN_DARK = colors.HexColor("#0C7045")
GREEN_PALE = colors.HexColor("#EAF6F0")
GRID = colors.HexColor("#CBD7D2")
LIGHT = colors.HexColor("#F5F8F6")
WHITE = colors.white
WARNING = colors.HexColor("#FFF5D8")


def register_fonts() -> None:
    font_dir = Path("/usr/share/fonts/truetype/dejavu")
    pdfmetrics.registerFont(TTFont("RH-Sans", str(font_dir / "DejaVuSans.ttf")))
    pdfmetrics.registerFont(TTFont("RH-Sans-Bold", str(font_dir / "DejaVuSans-Bold.ttf")))
    pdfmetrics.registerFont(TTFont("RH-Mono", str(font_dir / "DejaVuSansMono.ttf")))
    pdfmetrics.registerFontFamily(
        "RH-Sans",
        normal="RH-Sans",
        bold="RH-Sans-Bold",
        italic="RH-Sans",
        boldItalic="RH-Sans-Bold",
    )


def make_styles():
    base = getSampleStyleSheet()
    return {
        "cover_kicker": ParagraphStyle(
            "CoverKicker",
            parent=base["Normal"],
            fontName="RH-Sans-Bold",
            fontSize=10,
            leading=14,
            textColor=GREEN,
            spaceAfter=10,
            tracking=1.2,
        ),
        "cover_title": ParagraphStyle(
            "CoverTitle",
            parent=base["Title"],
            fontName="RH-Sans-Bold",
            fontSize=29,
            leading=35,
            textColor=INK,
            alignment=TA_LEFT,
            spaceAfter=13,
        ),
        "cover_subtitle": ParagraphStyle(
            "CoverSubtitle",
            parent=base["Normal"],
            fontName="RH-Sans",
            fontSize=12,
            leading=18,
            textColor=MUTED,
            spaceAfter=22,
        ),
        "h1": ParagraphStyle(
            "H1",
            parent=base["Heading1"],
            fontName="RH-Sans-Bold",
            fontSize=18,
            leading=23,
            textColor=INK,
            spaceBefore=10,
            spaceAfter=8,
            keepWithNext=True,
        ),
        "h2": ParagraphStyle(
            "H2",
            parent=base["Heading2"],
            fontName="RH-Sans-Bold",
            fontSize=13,
            leading=17,
            textColor=GREEN_DARK,
            spaceBefore=9,
            spaceAfter=5,
            keepWithNext=True,
        ),
        "h3": ParagraphStyle(
            "H3",
            parent=base["Heading3"],
            fontName="RH-Sans-Bold",
            fontSize=10.5,
            leading=14,
            textColor=INK,
            spaceBefore=7,
            spaceAfter=4,
            keepWithNext=True,
        ),
        "body": ParagraphStyle(
            "Body",
            parent=base["BodyText"],
            fontName="RH-Sans",
            fontSize=8.7,
            leading=13.2,
            textColor=INK,
            spaceAfter=5.5,
        ),
        "small": ParagraphStyle(
            "Small",
            parent=base["BodyText"],
            fontName="RH-Sans",
            fontSize=7.3,
            leading=10.2,
            textColor=INK,
        ),
        "small_bold": ParagraphStyle(
            "SmallBold",
            parent=base["BodyText"],
            fontName="RH-Sans-Bold",
            fontSize=7.3,
            leading=10.2,
            textColor=WHITE,
        ),
        "code": ParagraphStyle(
            "Code",
            parent=base["Code"],
            fontName="RH-Mono",
            fontSize=6.8,
            leading=10.2,
            textColor=colors.HexColor("#133B2C"),
            leftIndent=0,
            rightIndent=0,
        ),
        "quote": ParagraphStyle(
            "Quote",
            parent=base["BodyText"],
            fontName="RH-Sans",
            fontSize=8.5,
            leading=13,
            textColor=GREEN_DARK,
        ),
        "toc": ParagraphStyle(
            "Toc",
            parent=base["BodyText"],
            fontName="RH-Sans",
            fontSize=9,
            leading=13.5,
            textColor=INK,
        ),
    }


class BrandMark(Flowable):
    def __init__(self, width=42 * mm, height=18 * mm):
        super().__init__()
        self.width = width
        self.height = height

    def draw(self):
        canvas = self.canv
        canvas.setFillColor(INK)
        canvas.roundRect(0, 0, self.width, self.height, 2.5 * mm, stroke=0, fill=1)
        canvas.setFillColor(WHITE)
        canvas.setFont("RH-Sans-Bold", 9.5)
        canvas.drawString(8 * mm, 10.7 * mm, "RUHSAT")
        canvas.setFillColor(GREEN)
        canvas.drawString(8 * mm, 5.3 * mm, "HESAP")
        canvas.setStrokeColor(WHITE)
        canvas.setLineWidth(1.2)
        for idx, y in enumerate((5.0, 8.2, 11.4)):
            canvas.line(3.0 * mm, y * mm, 6.0 * mm, (y + (1 if idx == 1 else 0)) * mm)


def inline_markup(text: str) -> str:
    placeholders: list[str] = []

    def stash_code(match):
        placeholders.append(
            f'<font name="RH-Mono" color="#0C7045">{html.escape(match.group(1))}</font>'
        )
        return f"@@CODE{len(placeholders) - 1}@@"

    text = re.sub(r"`([^`]+)`", stash_code, text)
    text = html.escape(text)
    text = re.sub(r"\*\*([^*]+)\*\*", r"<b>\1</b>", text)
    for index, value in enumerate(placeholders):
        text = text.replace(f"@@CODE{index}@@", value)
    return text


def table_widths(rows: list[list[str]]) -> list[float]:
    columns = max(len(row) for row in rows)
    maxima = []
    for column in range(columns):
        values = [row[column] if column < len(row) else "" for row in rows]
        maxima.append(max(5, min(48, max(len(re.sub(r"[`*]", "", value)) for value in values))))
    weights = [math.sqrt(value) for value in maxima]
    minimum = 25 * mm if columns <= 3 else 18 * mm
    widths = [max(minimum, CONTENT_WIDTH * weight / sum(weights)) for weight in weights]
    scale = CONTENT_WIDTH / sum(widths)
    return [value * scale for value in widths]


def make_table(raw_rows: list[list[str]], styles) -> Table:
    columns = max(len(row) for row in raw_rows)
    normalized = [row + [""] * (columns - len(row)) for row in raw_rows]
    data = []
    for row_index, row in enumerate(normalized):
        cell_style = styles["small_bold"] if row_index == 0 else styles["small"]
        data.append([Paragraph(inline_markup(cell), cell_style) for cell in row])
    table = Table(data, colWidths=table_widths(normalized), repeatRows=1, hAlign="LEFT")
    table.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, 0), GREEN_DARK),
                ("TEXTCOLOR", (0, 0), (-1, 0), WHITE),
                ("BACKGROUND", (0, 1), (-1, -1), WHITE),
                ("ROWBACKGROUNDS", (0, 1), (-1, -1), [WHITE, LIGHT]),
                ("GRID", (0, 0), (-1, -1), 0.35, GRID),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 4),
                ("RIGHTPADDING", (0, 0), (-1, -1), 4),
                ("TOPPADDING", (0, 0), (-1, -1), 4),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
            ]
        )
    )
    return table


def code_block(lines: list[str], styles) -> Table:
    paragraphs = [Paragraph(html.escape(line) if line else "&#160;", styles["code"]) for line in lines]
    table = Table([[paragraph] for paragraph in paragraphs], colWidths=[CONTENT_WIDTH])
    table.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, -1), GREEN_PALE),
                ("BOX", (0, 0), (-1, -1), 0.55, GRID),
                ("LINEBEFORE", (0, 0), (0, -1), 3, GREEN),
                ("LEFTPADDING", (0, 0), (-1, -1), 8),
                ("RIGHTPADDING", (0, 0), (-1, -1), 7),
                ("TOPPADDING", (0, 0), (-1, 0), 6),
                ("BOTTOMPADDING", (0, -1), (-1, -1), 6),
            ]
        )
    )
    return table


def quote_block(text: str, styles) -> Table:
    table = Table([[Paragraph(inline_markup(text), styles["quote"])]], colWidths=[CONTENT_WIDTH])
    table.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, -1), GREEN_PALE),
                ("LINEBEFORE", (0, 0), (0, -1), 4, GREEN),
                ("LEFTPADDING", (0, 0), (-1, -1), 10),
                ("RIGHTPADDING", (0, 0), (-1, -1), 10),
                ("TOPPADDING", (0, 0), (-1, -1), 8),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 8),
            ]
        )
    )
    return table


def parse_markdown(markdown: str, styles) -> list:
    lines = markdown.splitlines()
    story: list = []
    index = 0
    skipped_title = False

    while index < len(lines):
        stripped = lines[index].strip()

        if not stripped:
            index += 1
            continue
        if stripped.startswith("# ") and not skipped_title:
            skipped_title = True
            index += 1
            continue
        if stripped.startswith("**Uygulama:") or stripped.startswith("**Uyumluluk:") or stripped.startswith("**Kapsam:"):
            index += 1
            continue
        if stripped == "---":
            story.append(Spacer(1, 3 * mm))
            index += 1
            continue
        if stripped.startswith("```"):
            index += 1
            code_lines = []
            while index < len(lines) and not lines[index].strip().startswith("```"):
                code_lines.append(lines[index].rstrip())
                index += 1
            index += 1
            story.extend([code_block(code_lines, styles), Spacer(1, 3.2 * mm)])
            continue
        if stripped.startswith("|"):
            rows = []
            while index < len(lines) and lines[index].strip().startswith("|"):
                cells = [cell.strip() for cell in lines[index].strip().strip("|").split("|")]
                if not all(re.fullmatch(r":?-{3,}:?", cell or "") for cell in cells):
                    rows.append(cells)
                index += 1
            if rows:
                story.extend([make_table(rows, styles), Spacer(1, 3.5 * mm)])
            continue
        if stripped.startswith(">"):
            quote_parts = []
            while index < len(lines) and lines[index].strip().startswith(">"):
                quote_parts.append(lines[index].strip()[1:].strip())
                index += 1
            story.extend([quote_block(" ".join(quote_parts), styles), Spacer(1, 3 * mm)])
            continue
        if stripped.startswith("### "):
            story.append(Paragraph(inline_markup(stripped[4:]), styles["h3"]))
            index += 1
            continue
        if stripped.startswith("## "):
            story.append(Paragraph(inline_markup(stripped[3:]), styles["h1"]))
            index += 1
            continue
        if stripped.startswith("# "):
            story.append(Paragraph(inline_markup(stripped[2:]), styles["h1"]))
            index += 1
            continue
        if re.match(r"^[-*] ", stripped) or re.match(r"^\d+\. ", stripped):
            ordered = bool(re.match(r"^\d+\. ", stripped))
            items = []
            while index < len(lines):
                current = lines[index].strip()
                pattern = r"^\d+\. (.+)$" if ordered else r"^[-*] (.+)$"
                match = re.match(pattern, current)
                if not match:
                    break
                item_text = match.group(1)
                if item_text.startswith("[ ] "):
                    item_text = "□ " + item_text[4:]
                elif item_text.startswith("[x] ") or item_text.startswith("[X] "):
                    item_text = "■ " + item_text[4:]
                items.append(ListItem(Paragraph(inline_markup(item_text), styles["body"]), leftIndent=5))
                index += 1
            list_options = {
                "bulletType": "1" if ordered else "bullet",
                "bulletFontName": "RH-Sans",
                "bulletFontSize": 8,
                "leftIndent": 13,
                "bulletColor": GREEN_DARK,
                "spaceAfter": 3,
            }
            if ordered:
                list_options["start"] = "1"
            story.append(ListFlowable(items, **list_options))
            continue

        paragraph_parts = [stripped.rstrip("\\").strip()]
        index += 1
        while index < len(lines):
            current = lines[index].strip()
            if not current or current.startswith(("#", "|", ">", "```", "- ", "* ")) or re.match(r"^\d+\. ", current):
                break
            paragraph_parts.append(current.rstrip("\\").strip())
            index += 1
        story.append(Paragraph(inline_markup(" ".join(paragraph_parts)), styles["body"]))

    return story


def cover_and_contents(styles) -> list:
    sections = [
        "1. Hizli baslangic",
        "2. Kodun yapisi",
        "3. Tanimlanan anahtarlar",
        "4. Alan tipi - TIP degerleri",
        "5. Hangi bilgi nereden gelir?",
        "6. Bagimsiz bolumlerin birlestirilme kurali",
        "7. Kat bilgisinin belirlenmesi",
        "8. Oda sayisi ve nitelik",
        "9. Uygulama ornegi - bir dairenin tum zonlari",
        "10. Zon Numarasini BB olarak kullanma",
        "11. Cok bloklu proje ornekleri",
        "12. Archicad'de adim adim uygulama",
        "13. Yeniden aktarim ve veri guvenligi",
        "14. Gecersiz veya desteklenmeyen kodlar",
        "15. Ortak, yapi insaat, emsal ve siginak zonlari",
        "16. Ofis standardi onerisi",
        "17. Son kontrol listesi",
    ]

    toc_rows = []
    split = math.ceil(len(sections) / 2)
    for row in range(split):
        left = sections[row]
        right = sections[row + split] if row + split < len(sections) else ""
        toc_rows.append([Paragraph(left, styles["toc"]), Paragraph(right, styles["toc"])])
    toc = Table(toc_rows, colWidths=[CONTENT_WIDTH / 2 - 3 * mm, CONTENT_WIDTH / 2 - 3 * mm], hAlign="LEFT")
    toc.setStyle(
        TableStyle(
            [
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 0),
                ("RIGHTPADDING", (0, 0), (-1, -1), 6),
                ("TOPPADDING", (0, 0), (-1, -1), 3),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 3),
            ]
        )
    )

    return [
        Spacer(1, 12 * mm),
        BrandMark(),
        Spacer(1, 22 * mm),
        Paragraph("ARCHICAD 29 UYGULAMA KILAVUZU", styles["cover_kicker"]),
        Paragraph("Zon Kodları<br/>Kullanım Kılavuzu", styles["cover_title"]),
        Paragraph(
            "Blok, bağımsız bölüm, kat, net/brüt alan, eklenti, balkon, oda sayısı ve nitelik verilerinin Archicad zonlarından güvenli biçimde aktarılması.",
            styles["cover_subtitle"],
        ),
        code_block(["RH|BLOK=A|BB=01|TIP=NET|ODA=3|MAHAL=SALON|NITELIK=MESKEN"], styles),
        Spacer(1, 11 * mm),
        quote_block(
            "Sürüm notu: Bu kılavuz Ruhsat Hesap 0.5.9 içinde çalışan zon okuyucu kurallarına göre hazırlanmıştır.",
            styles,
        ),
        Spacer(1, 15 * mm),
        Paragraph("3 Ağustos 2026  |  Windows  |  Archicad 29", styles["body"]),
        PageBreak(),
        Paragraph("İçindekiler", styles["h1"]),
        Paragraph(
            "Kılavuz, hızlı uygulamadan veri birleştirme kurallarına ve sorun gidermeye kadar günlük proje akışına göre düzenlenmiştir.",
            styles["body"],
        ),
        Spacer(1, 3 * mm),
        toc,
        Spacer(1, 8 * mm),
        quote_block(
            "En güvenli ofis standardı: uzun anahtarları, iki haneli BB numaralarını ve yalnızca tanımlı TIP değerlerini kullanın.",
            styles,
        ),
        PageBreak(),
    ]


def draw_page(canvas, doc):
    page = canvas.getPageNumber()
    canvas.saveState()
    canvas.setTitle("Ruhsat Hesap - Zon Kodlari Kullanim Kilavuzu")
    canvas.setAuthor("Ruhsat Hesap")
    if page > 1:
        canvas.setStrokeColor(GRID)
        canvas.setLineWidth(0.45)
        canvas.line(LEFT, PAGE_HEIGHT - 11 * mm, PAGE_WIDTH - RIGHT, PAGE_HEIGHT - 11 * mm)
        canvas.setFont("RH-Sans-Bold", 6.8)
        canvas.setFillColor(INK)
        canvas.drawString(LEFT, PAGE_HEIGHT - 8.5 * mm, "RUHSAT HESAP")
        canvas.setFont("RH-Sans", 6.8)
        canvas.setFillColor(MUTED)
        canvas.drawRightString(PAGE_WIDTH - RIGHT, PAGE_HEIGHT - 8.5 * mm, "ARCHICAD ZON KODLARI")

        canvas.setStrokeColor(GRID)
        canvas.line(LEFT, 10 * mm, PAGE_WIDTH - RIGHT, 10 * mm)
        canvas.setFont("RH-Sans", 6.5)
        canvas.setFillColor(MUTED)
        canvas.drawString(LEFT, 6.7 * mm, "Ruhsat Hesap 0.5.9")
        canvas.drawRightString(PAGE_WIDTH - RIGHT, 6.7 * mm, f"Sayfa {page}")
    else:
        canvas.setFillColor(GREEN)
        canvas.rect(0, 0, 6 * mm, PAGE_HEIGHT, stroke=0, fill=1)
        canvas.setFillColor(GREEN_PALE)
        canvas.circle(PAGE_WIDTH - 12 * mm, PAGE_HEIGHT - 12 * mm, 22 * mm, stroke=0, fill=1)
    canvas.restoreState()


def main() -> None:
    register_fonts()
    styles = make_styles()
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)

    doc = BaseDocTemplate(
        str(OUTPUT),
        pagesize=A4,
        leftMargin=LEFT,
        rightMargin=RIGHT,
        topMargin=TOP,
        bottomMargin=BOTTOM,
        title="Ruhsat Hesap Archicad Zon Kodlari Kullanim Kilavuzu",
        author="Ruhsat Hesap",
    )
    frame = Frame(LEFT, BOTTOM, CONTENT_WIDTH, PAGE_HEIGHT - TOP - BOTTOM, id="normal")
    doc.addPageTemplates(PageTemplate(id="guide", frames=[frame], onPage=draw_page))

    markdown = SOURCE.read_text(encoding="utf-8")
    story = cover_and_contents(styles)
    story.extend(parse_markdown(markdown, styles))
    doc.build(story)
    print(OUTPUT)


if __name__ == "__main__":
    main()
