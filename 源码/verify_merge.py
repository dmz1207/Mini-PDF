"""Independent checks of merge outputs created by the native regression runner.

Usage: python verify_merge.py verify <test-directory>
Requires pypdf and PyMuPDF only for development, never in the application.
"""
import json
import re
import sys
from pathlib import Path

import fitz
from pypdf import PdfReader


def verify(root):
    cases = {
        "interleave-a-first.pdf": [1, 2, 3, 4, 5, 6, 7, 8],
        "interleave-b-first.pdf": [2, 1, 4, 3, 6, 5, 8, 7],
        "interleave-second-reversed.pdf": [1, 8, 3, 6, 5, 4, 7, 2],
        "interleave-blocks.pdf": [1, 3, 2, 4, 5, 7, 6, 8],
        "interleave-uneven.pdf": [1, 2, 3, 4, 5, 6, 8],
        "interleave-extra.pdf": [1, 2, 3, 4, 5, 6, 7, 8, 1],
        "interleave-rotated.pdf": [1, 2, 3, 4, 5, 6, 7, 8],
        "interleave-regrouped.pdf": [1, 3, 5, 7, 2, 4, 6, 8],
        "merge-close-saved.pdf": [1, 2, 3, 4, 5, 6, 7, 8],
        "interleave-all.pdf": [1, 2, 1, 3, 4, 5, 6, 8],
        "interleave-all-blocks.pdf": [1, 3, 2, 4, 1, 5, 6, 8],
        "merge-natural-order.pdf": [1, 2, 4, 6, 8, 1, 3, 5],
        "merge-reversed-files.pdf": [5, 3, 1, 8, 6, 4, 2, 1],
    }
    results = []

    def check(condition, message):
        if not condition:
            raise AssertionError(message)
        results.append(message)

    with fitz.open(root / "features.pdf") as original:
        for name, expected in cases.items():
            reader = PdfReader(root / name)
            check(len(reader.pages) == len(expected), f"{name}: exact page count")
            actual = [int(re.search(r"FEATURE PAGE (\d+)", p.extract_text()).group(1))
                      for p in reader.pages]
            check(actual == expected, f"{name}: independent parser confirms page order")
            rotations = [0] * len(expected)
            if name == "interleave-all.pdf":
                rotations[3] = 90
            elif name == "interleave-rotated.pdf":
                rotations[2] = 90
            elif name == "interleave-regrouped.pdf":
                rotations[1] = 90
            check([int(p.get("/Rotate", 0)) % 360 for p in reader.pages] == rotations,
                  f"{name}: page rotation is retained")
            with fitz.open(root / name) as output:
                matches = True
                for index, source in enumerate(expected):
                    a = output[index].get_pixmap(matrix=fitz.Matrix(.5, .5), alpha=False)
                    b = original[source - 1].get_pixmap(
                        matrix=fitz.Matrix(.5, .5).prerotate(rotations[index]), alpha=False)
                    matches &= (a.width, a.height, a.samples) == (b.width, b.height, b.samples)
                check(matches, f"{name}: independent renderer matches every source page")
    before = PdfReader(root / "batched-duplicates.pdf")
    after = PdfReader(root / "batched-duplicates-edited.pdf")
    check(len(before.pages) == len(after.pages) == 12, "batch duplicate imports retain all 12 pages")
    check([int(p.get("/Rotate", 0)) % 360 for p in before.pages] == [i % 4 * 90 for i in range(12)],
          "each batched duplicate retains its independent rotation")
    check(len({p.indirect_reference.idnum for p in before.pages}) == 12,
          "batched duplicates have distinct page dictionaries")
    check("FIRST_COPY_ONLY" in after.pages[0].extract_text(), "edited duplicate contains its own new text")
    check(all("FIRST_COPY_ONLY" not in p.extract_text() for p in after.pages[1:]),
          "editing a duplicate never changes another page")
    with fitz.open(root / "baseline.pdf") as source, fitz.open(root / "batched-duplicates.pdf") as old, fitz.open(root / "batched-duplicates-edited.pdf") as edited:
        correct = True
        unaffected = True
        for i in range(12):
            # Match intrinsic PDF rotation, including fractional MediaBox coordinates.
            source[0].set_rotation(i % 4 * 90)
            expected = source[0].get_pixmap(matrix=fitz.Matrix(.5, .5), alpha=False)
            actual = old[i].get_pixmap(matrix=fitz.Matrix(.5, .5), alpha=False)
            correct &= (expected.width, expected.height, expected.samples) == (actual.width, actual.height, actual.samples)
            if i:
                changed = edited[i].get_pixmap(matrix=fitz.Matrix(.5, .5), alpha=False)
                unaffected &= (actual.width, actual.height, actual.samples) == (changed.width, changed.height, changed.samples)
        check(correct, "independent renderer verifies every batched duplicate")
        check(unaffected, "independent renderer confirms all other duplicate pages stay unchanged after editing")
    shared = PdfReader(root / "batched-resources.pdf")
    check(len(shared.pages) == 12, "resource batch preserves all distinct source pages")
    images = set()
    for page in shared.pages:
        for obj in page["/Resources"]["/XObject"].values():
            if obj.get_object().get("/Subtype") == "/Image":
                images.add(obj.idnum)
    check(len(images) == 1, "all merged source pages share a single imported image resource")
    check((root / "batched-resources.pdf").stat().st_size < (root / "shared-resources.pdf").stat().st_size * 2,
          "merging shared resources does not multiply output file size")
    with fitz.open(root / "shared-resources.pdf") as original, fitz.open(root / "batched-resources.pdf") as output:
        check(all(original[i].get_pixmap().samples == output[i].get_pixmap().samples for i in range(12)),
              "independent renderer verifies every distinct shared-resource page")
    notes = PdfReader(root / "batched-notes.pdf")
    check(len(notes.pages) == 2, "duplicate-note merge retains both pages")
    annotations = [p["/Annots"][0] for p in notes.pages]
    check(len({a.idnum for a in annotations}) == 2, "duplicate pages own distinct indirect annotation dictionaries")
    check([a.get_object()["/Contents"] for a in annotations] == ["FIRST_NOTE_ONLY", "ORIGINAL_NOTE"],
          "independent parser confirms editing an indirect note leaves the other copy unchanged")
    report = {"assertions": len(results), "failures": 0, "checks": results}
    (root / "independent-merge.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"PASS: {len(results)} independent merge checks")


if __name__ == "__main__":
    action, directory = sys.argv[1:]
    if action != "verify":
        raise SystemExit("Usage: python verify_merge.py verify <test-directory>")
    verify(Path(directory))
