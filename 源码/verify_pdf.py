"""Optional independent interoperability checks (developer tooling only).

Usage: python verify_pdf.py fixtures|verify <test-directory>
Dependencies: pypdf, reportlab, pymupdf. No dependency is shipped with the app.
"""
import io
import json
from pathlib import Path
import sys

sys.dont_write_bytecode = True


def fixtures(root):
    from reportlab.pdfgen import canvas
    from reportlab.pdfbase import pdfmetrics
    from reportlab.pdfbase.cidfonts import UnicodeCIDFont
    from pypdf import PdfReader, PdfWriter
    from pypdf.constants import UserAccessPermissions

    root.mkdir(parents=True, exist_ok=True)
    stream = io.BytesIO()
    pdfmetrics.registerFont(UnicodeCIDFont("STSong-Light"))
    c = canvas.Canvas(stream, pagesize=(595.276, 841.89))
    c.setTitle("Independent PDF interoperability fixture")
    for i in range(3):
        c.bookmarkPage(f"page-{i}")
        c.addOutlineEntry(f"Chapter {i + 1}", f"page-{i}", level=0)
        c.setFont("Helvetica", 22)
        c.drawString(55, 765, f"EXTERNAL PAGE {i + 1}")
        c.setFont("STSong-Light", 18)
        c.drawString(55, 725, "外部生成的中文 PDF 测试")
        c.setFont("Helvetica", 12)
        c.drawString(55, 685, "This fixture was generated with ReportLab.")
        c.linkURL("https://example.org/", (55, 630, 240, 650), relative=0)
        c.linkRect("Next chapter", f"page-{(i + 1) % 3}", (300, 630, 450, 650))
        if i == 0:
            c.acroForm.textfield(name="example-field", value="Original form value", x=55, y=570, width=280, height=28)
        c.showPage()
    c.save()
    data = stream.getvalue()
    (root / "external.pdf").write_bytes(data)
    for filename, password, owner, algorithm in [
        ("restricted.pdf", "read123", "owner123", "RC4-128"),
        ("unicode-password.pdf", "阅读密码中文", "所有者密码中文", "AES-256"),
    ]:
        writer = PdfWriter(clone_from=PdfReader(io.BytesIO(data)))
        writer.encrypt(password, owner_password=owner, permissions_flag=UserAccessPermissions.PRINT, algorithm=algorithm)
        with (root / filename).open("wb") as f:
            writer.write(f)
    many = canvas.Canvas(str(root / "large-200.pdf"), pagesize=(595.276, 841.89))
    for i in range(200):
        many.setFont("Helvetica", 20)
        many.drawString(55, 760, f"LARGE DOCUMENT PAGE {i + 1}")
        many.showPage()
    many.save()


def verify(root):
    from pypdf import PdfReader
    import pymupdf

    checks = []

    def check(condition, name):
        if not condition:
            raise AssertionError(name)
        checks.append(name)

    baseline = PdfReader(root / "baseline.pdf")
    edited = PdfReader(root / "edited.pdf")
    text = edited.pages[0].extract_text()
    check("REPLACED_TEXT" in text and "ORIGINAL_SENTENCE" not in text, "Independent parser confirms actual original-text replacement")
    check("Mini PDF 中文编辑测试" in text and "\x00" not in text, "Chinese and ordinary spaces have correct Unicode mappings")
    check(len(edited.pages[0].get("/Annots", [])) == 5, "All standard annotations survive saving")
    kinds = {str(a.get_object()["/Subtype"]) for a in edited.pages[0]["/Annots"]}
    check(kinds == {"/Highlight", "/Underline", "/StrikeOut", "/Ink", "/Text"}, "Annotation subtypes are interoperable PDF objects")
    note = next(a.get_object() for a in edited.pages[0]["/Annots"] if a.get_object()["/Subtype"] == "/Text")
    check("标准 PDF 便签" in note["/Contents"], "Chinese sticky-note contents survive")
    check(len(edited.pages[0].images) == 1, "Edited image is still an image object")
    check((root / "edited.pdf").stat().st_size < 1_000_000, "Chinese-font subsetting keeps example PDF under 1 MB")
    extracted = PdfReader(root / "extracted.pdf")
    check(len(extracted.pages) == 2 and "SECOND_PAGE" in extracted.pages[0].extract_text(), "Page extraction preserves requested order")
    check(len(PdfReader(root / "pages.pdf").pages) == 3, "Page insert, duplicate, delete, move and merge preserve page tree")
    ui = PdfReader(root / "ui-edited.pdf")
    check("UI 鼠标输入文字" in ui.pages[0].extract_text(), "Native mouse and modal-dialog editing roundtrips through independent parser")
    external = PdfReader(root / "external-edited.pdf")
    check("EXTERNAL_REPLACED" in external.pages[0].extract_text(), "Existing text from independent PDF generator is editable")
    check("外部生成的中文 PDF 测试" in external.pages[0].extract_text(), "Editing preserves independent CJK text")
    fields = external.get_fields()
    check(fields is not None and fields["example-field"]["/V"] == "Original form value", "Unrelated existing AcroForm field tree is preserved")
    widget = next(a.get_object() for a in external.pages[0]["/Annots"] if a.get_object()["/Subtype"] == "/Widget")
    check(str(widget.get("/V", "")) == "Original form value" and "/AP" in widget, "Widget value and appearance remain intact")
    check(len(external.outline) == 3, "Existing outline entries are preserved")
    protected = PdfReader(root / "owner-edited.pdf")
    check(protected.is_encrypted and protected.decrypt("read123") != 0, "Saving an encrypted PDF retains password protection")
    check("OWNER_EDIT" in protected.pages[0].extract_text(), "Authorized owner-password editing persists")
    for filename in ["baseline.pdf", "edited.pdf", "external-edited.pdf", "ui-edited.pdf", "multi-page.pdf"]:
        with pymupdf.open(root / filename) as doc:
            page = doc[0]
            pix = page.get_pixmap(matrix=pymupdf.Matrix(1.2, 1.2), alpha=False)
            pix.save(root / (filename[:-4] + "-independent.png"))
            check(pix.width > 100 and pix.height > 100, f"Independent renderer opens {filename}")
    (root / "independent-checks.json").write_text(json.dumps({"passed": len(checks), "checks": checks}, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"Independent checks: {len(checks)} passed")


if __name__ == "__main__":
    action, directory = sys.argv[1:]
    {"fixtures": fixtures, "verify": verify}[action](Path(directory))
