# Mini PDF

[English](README.md) | [简体中文](README.zh-CN.md)

**A small, native Windows PDF reader and editor.**

Mini PDF combines a compact reading interface with text, object, annotation, and page editing. It uses Windows controls and PDFium, with no Electron, WebView, or bundled browser runtime.

The **v0.5.0 preview** is a single **2.81 MiB executable** for **Windows 10 / Windows 11 x64**. The application starts in English and includes an immediate English / Simplified Chinese language switch.

[**Download Mini PDF.exe**](https://github.com/dmz1207/Mini-PDF/raw/refs/heads/main/Mini%20PDF.exe) · [Source code](.) · [Test record](测试记录.md) · [Report an issue](https://github.com/dmz1207/Mini-PDF/issues)

## What's new in v0.5

- Document tabs appear above the reading toolbar, with a larger menu button at the left of the same row.
- Drag tabs left or right to change their order. Hold a dragged tab near either end of an overflowing tab row to reach hidden tabs.
- Each document keeps its own page, zoom, view, scroll position, editing tool, and undo history. Reordering tabs does not edit the PDF.
- Larger toolbar text and icons improve readability while keeping the interface compact.
- English is the default repository documentation; a complete [Chinese version](README.zh-CN.md) remains available.

## Getting started

1. Download `Mini PDF.exe` and place it in a writable folder.
2. Double-click it, then open a PDF with `Ctrl+O`, or drag PDF files onto the window.
3. Open more files to create tabs. Only the selected document appears in the reading area.
4. Click **Edit** to expand editing tools. Use **Object**, select a text block, and double-click to edit it.
5. Save with `Ctrl+S`, or use `Ctrl+Shift+S` to save a copy.

There is no installer, activation, account, advertising, or telemetry. The app does not change your default PDF file association. It uses the .NET Framework 4.x provided by Windows; Node.js, Python, Electron, and a modern .NET SDK are not required to run it.

**Compatibility:** tested on Windows 10 Home Chinese x64, build 19045. Windows 11 x64 uses the same target APIs, but has not yet been tested on a separate Windows 11 machine. ARM64 and 32-bit Windows builds are not provided.

## Interface and help languages

Open **Menu > Language** to choose **English** (first) or **简体中文** (second). English is the default for each new independent window. Switching is immediate and preserves open documents, edits, reading state, and entered text. The choice applies to the current window; no language settings file is created. Native Windows file and print dialogs follow your Windows display language.

The **Help** menu lists the English user guide first and the Chinese guide second. Both are embedded in the EXE; `F1` opens the English guide.

## Reading and document tabs

With one document open, the app uses one compact toolbar. With multiple documents, tabs and the menu share a row above it. Menus stay collapsed behind `☰`; press `F10` to open them. Editing tools, the sidebar, and the status bar are hidden until needed.

- Drag the text area of a tab to reorder it. Release to finish, or press `Esc` to stop dragging.
- Click a tab's `×`, middle-click the tab, or use `Ctrl+W` to close it. Unsaved changes trigger a save prompt.
- Opening an already open file selects its existing tab. Opening another PDF from Explorer normally sends it to the existing window.
- `Ctrl+Shift+N` opens an independent window. The command-line flag `--new-window` provides the same behavior.
- `Ctrl+6` fits one page; `Ctrl+7` fits the current two-page spread to the available width.
- Single-page, double-page, book, and grid layouts always refer to pages **within the selected PDF**. They do not combine different documents.

### Views and zoom

Single-page, double-page, book, continuous scrolling, and multi-page grid views are available. In grid view, zooming out increases the number of columns; double-click a page to return to single-page reading.

The zoom box accepts exact integer or decimal percentages from **1% to 6400%**, such as `87%` or `123.5%`. Press Enter to apply. Manual `100%` keeps its actual scale; use Fit Width to fill the window. There is a dedicated `适宽` (Fit Width) toolbar button, a menu item, and the `Ctrl+2` shortcut.

Fit Width uses the actual size of the current page or spread, including mixed paper sizes and an odd final page. External side margins are reduced to about 0–1 pixels, with a possible 2-pixel rounding difference for a spread. Page gaps are 4 pixels. Native Windows scrollbars are approximately 10 pixels wide at 100% system scaling.

At high zoom, PDFium renders visible areas as native-resolution tiles rather than enlarging a low-resolution screenshot. The image-cache target is 64 MiB; a single full-page bitmap can exceed that target within its separate rendering limit.

### Focused reading

`Ctrl+H` hides document controls while keeping the native title bar, window buttons, and window position. `F11` enters borderless full screen; `Ctrl+L` starts a single-page presentation. Press `Esc`, or use the right-click menu, to leave these modes.

## Editing features

| Area | Available features |
| --- | --- |
| Existing text | Select and replace top-level PDF text objects; the old text object is removed rather than covered with a white rectangle |
| New text | Add Chinese or English text, including multiple lines; choose fonts, size, and color |
| Objects | Select, move, resize, or delete text, images, and shapes; insert images while keeping their aspect ratio |
| Annotations | Highlight, underline, strikeout, freehand ink, sticky notes, note editing, and annotation deletion |
| Drawing | Add rectangles; choose stroke color and width |
| Pages | Rotate, insert blank pages, duplicate, delete, move, extract, and merge |
| Export | Current page as PNG, document text as TXT, and printing of the current page or a page range |
| Navigation | Thumbnails, document outline, current-page annotation list, search, text selection and copy, internal links, and web links |
| History | Independent undo/redo history for each document, up to 12 steps with a 64 MiB history target |
| Save | Generated-document reload checks, complete rewriting, atomic replacement through a temporary file beside the destination, and font subsetting |
| Passwords | Open with a valid password, respect editing and printing permissions, and preserve encryption when saving |

Highlight, underline, strikeout, ink, and sticky notes are saved as standard PDF annotations. Added text, images, and rectangles become page content. Existing PDF links are readable; creating or editing links is not currently provided.

Use the object tool to drag existing objects. Double-click a selected text block or note to edit it. Press `Delete` to remove the selected object. Direct corner resizing is available for unrotated page objects; the resize command also accepts a percentage.

### Scanned PDFs with a text layer

Mini PDF can read, select, copy, and search existing OCR text layered over a scanned image. Top-level invisible or transparent OCR text can be edited while preserving its visibility settings and the original scan image, including when the scan is above the text layer.

**Changing OCR text changes the recognition layer, not the letters printed in the scan image.** OCR text inside nested Form objects remains readable and searchable but cannot currently be edited character by character. Mini PDF does not include an OCR engine to recognize a scan that has no text layer.

## Keyboard shortcuts

| Action | Shortcut |
| --- | --- |
| Open / save / save as | `Ctrl+O` / `Ctrl+S` / `Ctrl+Shift+S` |
| Next / previous document tab | `Ctrl+Tab` / `Ctrl+Shift+Tab` |
| Close current tab | `Ctrl+W` or `Ctrl+F4` |
| New document / independent window | `Ctrl+N` / `Ctrl+Shift+N` |
| Fit page / fit width / actual size | `Ctrl+1` / `Ctrl+2` / `Ctrl+0` |
| Automatic single page / two-page width | `Ctrl+6` / `Ctrl+7` |
| Book view / multi-page grid | `Ctrl+8` / `Ctrl+9` |
| Reading mode / full screen / presentation | `Ctrl+H` / `F11` / `Ctrl+L` |
| Leave reading mode, full screen, or presentation; stop a tab drag | `Esc` |
| Show or hide sidebar | `F12` (`F9` also supported) |
| Find / next result | `Ctrl+F` / `Enter` or `F3` |
| Select page text / copy | `Ctrl+A` / `Ctrl+C` |
| Undo / redo | `Ctrl+Z` / `Ctrl+Y` |
| Previous / next page or spread | `PageUp` / `PageDown` |
| Rotate view left / right | `Ctrl+Shift+-` / `Ctrl+Shift++` |
| Edit page rotation | `Ctrl+R` / `Ctrl+Shift+R` |
| Open collapsed menu | `F10` |

Use `Ctrl+mouse wheel` to zoom, the mouse wheel to scroll, and middle-button dragging to pan. Right-click the document for reading and editing commands. Text fields retain their normal editing shortcuts.

## Portable operation and cleanup

The EXE is **2,951,168 bytes**, approximately **2.81 MiB**. Its resources use the Windows Compression API with LZMS and 8 MiB blocks. No additional decompression library is shipped.

On startup, the app extracts the engine, help text, and licenses to a hidden `.MiniPDF-runtime-*` folder beside the EXE. The application's `TEMP` and `TMP` point to that folder. Normal exit removes extracted files; the next launch cleans up stale runtime folders after an unexpected exit. Active instances keep their own files protected. Extra files created by the user inside a runtime folder are preserved.

The portable directory must be writable. Documents are saved to the program folder or a user-selected location; they are not automatically saved inside the extracted runtime folder.

## Current limitations

This is a preview release, and the editing scope differs from a full commercial PDF editor:

- No built-in OCR, interactive form filling, digital signatures, signature verification, permanent redaction, measurement, stamps, or annotation replies.
- Text editing works on PDF text objects, not Word-style flowing paragraphs. Replacement fonts can change character widths and do not preserve every original font or layout property.
- Text converted to outlines, or text inside nested Form objects, cannot currently be edited character by character. Some grouped objects can be manipulated as a whole.
- Editing the recognition layer does not change the scan image. Image retouching is not provided.
- Editing can invalidate existing digital signatures.
- Undo keeps at least the most recent step, so a very large document can exceed the history target. Multiple open documents also require memory for their own PDF data and histories.
- The language switch translates application controls and help. Native system dialogs follow the Windows display language.

## Build from source

Download or clone the complete repository. From the project root, run Windows PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\源码\build.ps1
```

No additional SDK is required. The script uses the C# compiler supplied with the system .NET Framework. The first build downloads the pinned PDFium **158.0.8111.0 x64** archive and verifies SHA-256. PDFium is built without V8 and XFA.

Downloads, intermediate files, and tests stay under the project's `work/` folder. The output is `Mini PDF.exe` in the project root. The application is optimized, debug symbols are omitted, and test and packing tools are excluded from the portable payload. Source files remain readable; removing whitespace does not reduce compiled code size.

```text
Mini-PDF/
├─ Mini PDF.exe             Portable application
├─ README.md                English documentation (default)
├─ README.zh-CN.md          Chinese documentation
├─ LICENSE                  MIT license for application code
├─ CHANGELOG.md             Version history
└─ 源码/                     Source code
   ├─ *.cs                 Application, launcher, and regression tests
   ├─ build.ps1            Build and production-assembly tests
   ├─ test-launcher.ps1     Portable EXE, file forwarding, and cleanup tests
   ├─ verify_pdf.py         Independent PDF fixtures and verification
   ├─ README.md             English source guide
   ├─ README.zh-CN.md       Chinese source guide
   ├─ 使用说明.txt            Embedded application help
   ├─ 测试记录.md             Detailed test record
   └─ third-party-licenses/ PDFium and dependency notices
```

## Verification

The current release passed **210 program regressions, 33 independent PDF checks, and 12 portable-launcher checks: 255 checks in total**, on Windows 10 x64.

Coverage includes native windows and mouse messages, text dialogs, tab dragging in both directions, overflow scrolling, cancellation and capture loss, menu relocation, per-document state, unsaved-change prompts, layouts, exact zoom, scan/OCR layers, editing, passwords, save-failure protection, 200-page files, and print preview. Printing tests use a preview controller and do not send a physical print job.

Independent checks use ReportLab fixtures, pypdf parsing, and MuPDF rendering to verify saved text, image and annotation preservation, page trees, existing outlines and unmodified form fields, encryption, and unchanged scan pixels after OCR-layer edits. Compression is checked with a SHA-256 round trip. The final portable EXE is tested in an isolated folder without an adjacent DLL, including later file-open requests and cleanup after unexpected termination.

These results describe tested flows, not a guarantee that every PDF, printer, or Windows configuration is free of defects. Windows 11 still needs separate machine testing.

Run the built-in regressions:

```powershell
powershell -ExecutionPolicy Bypass -File .\源码\build.ps1 -Test
```

For the full independent checks, install the development-only Python packages `pypdf`, `reportlab`, `pymupdf`, and `cryptography` in your development environment, then run:

```powershell
python .\源码\verify_pdf.py fixtures .\work\tests
powershell -ExecutionPolicy Bypass -File .\源码\build.ps1 -Test
python .\源码\verify_pdf.py verify .\work\tests
powershell -ExecutionPolicy Bypass -File .\源码\test-launcher.ps1
```

These Python dependencies are not bundled and are not needed to run Mini PDF. Generated PDFs, screenshots, downloads, and caches can be removed by deleting the build's `work/` directory after testing; keep your own documents elsewhere.

Current EXE SHA-256:

```text
313FDFD5CC05AAC312499E9DC947B7712F602BBAB14E6965F05EFDCC9E97884B
```

## Technology, references, and licenses

- Interface: C# 5, Windows Forms, and the Windows-supplied .NET Framework 4.x.
- Engine: [PDFium](https://pdfium.googlesource.com/pdfium/), using the [pinned x64 binary build](https://github.com/bblanchon/pdfium-binaries/releases/tag/chromium%2F8111).
- Compression: [Microsoft Windows Compression API](https://learn.microsoft.com/en-us/windows/win32/cmpapi/using-the-compression-api).
- Reading behavior references: [SumatraPDF viewing modes](https://www.sumatrapdfreader.org/docs/Viewing-modes), [DisplayModel](https://github.com/sumatrapdfreader/sumatrapdf/blob/master/src/shared/DisplayModel.cpp), [menus](https://github.com/sumatrapdfreader/sumatrapdf/blob/master/src/Menu.cpp), and [selection](https://github.com/sumatrapdfreader/sumatrapdf/blob/master/src/shared/Selection.cpp).
- Feature reference: [PDF Looker Pro](https://github.com/dongfangxy/pdf-looker-pro).

The application code is independently implemented; SumatraPDF's GPL application code and PDF Looker Pro's application code were not copied into it. Application and launcher sources use the [MIT license](LICENSE). PDFium and its dependencies retain their [respective license notices](third-party-licenses), embedded in the EXE and accessible from the Help menu. They are open-source attribution notices, not activation requirements.

When reporting a problem, include the Windows version, Mini PDF version, reproduction steps, expected behavior, and actual behavior. Share only PDFs that you are allowed to make public.
