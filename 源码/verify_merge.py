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
            if name == "interleave-rotated.pdf":
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
    report = {"assertions": len(results), "failures": 0, "checks": results}
    (root / "independent-merge.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"PASS: {len(results)} independent merge checks")


if __name__ == "__main__":
    action, directory = sys.argv[1:]
    if action != "verify":
        raise SystemExit("Usage: python verify_merge.py verify <test-directory>")
    verify(Path(directory))
