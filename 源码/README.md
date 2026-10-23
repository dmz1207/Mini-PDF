# Mini PDF

[English](README.md) | [简体中文](README.zh-CN.md)

**A small, native Windows PDF reader and editor.**

Mini PDF combines a compact, Sumatra-inspired reading interface with PDF text, object, annotation, page, watermark, signature and printing tools. It uses Windows controls and PDFium, with no Electron, WebView or bundled browser runtime.

The **v0.10.0 preview** is one approximately **2.93 MiB executable** for **Windows 10 / Windows 11 x64**. English is the default interface and documentation; Simplified Chinese is the second option.

[**Download Mini PDF.exe**](https://github.com/dmz1207/Mini-PDF/raw/refs/heads/main/Mini%20PDF.exe) · [Source code](.) · [Test record](测试记录.md) · [Report an issue](https://github.com/dmz1207/Mini-PDF/issues)

**Historical v0.9.0 CI verification (2026-10-22):** the full 4,211-path regression check passes. Exact fresh-process comparisons distinguish PDFium replay variance from save damage; known baseline exceptions remain documented. [Investigation and results](CI-Verification.md). That verification did not change the v0.9.0 application binary.

## New in v0.10.0

- Convert PDF content to editable Word DOCX or Excel XLSX using built-in ZIP/XML APIs. Preserve Unicode, common aligned tables, numbers/identifiers and optional Word images; no Office installation is required.
- Add a standard AcroForm workspace for creating, moving, resizing, copying, deleting, filling and ordering common fields, with default values, choices, flags, local history and XFDF data import/export.
- Render native widget appearances and reconnect imported field trees/resources during merge, insertion, duplication and extraction. Keep the original files unchanged.
- Edit supported existing text in place, retaining its original PDF font, transformation, baseline, spacing, opacity, clipping and marks. No white covering rectangle is added.
- Choose one text object, adjacent fragments on one line, or a conservative same-style horizontal paragraph. Adjust size/color, local wrapping width and line spacing.
- Inspect a debounced PDFium preview of the text area or whole page before applying. One undo restores the complete change; accepting unchanged content creates no undo entry.
- If a new character cannot be confirmed in the original font, choose Auto or an installed system font explicitly. Font programs and rendering metrics can then change; inspect the preview.
- Preserve hidden OCR text without changing the scan image. Reject unsupported vertical/complex-script text, text used for clipping and ActualText replacement metadata rather than silently damaging it. Nested Form and outlined text remain outside this editor.
- Reuse the existing PDFium and native Windows UI; no new runtime, font pack or OCR model is bundled. See [Text editing](TextEditing.md) for scope and reproducible checks.

## Refined in v0.9.0

- Drag text across pages in continuous and facing-page views; copy in document order and highlight the entire selection in one undoable action. Hold near the top or bottom edge to extend selection while scrolling.
- Improve hit testing for hidden OCR layers, including transparent text and nested Forms. Verify a public scanned PDF and six Chinese scan/text-layer fixtures; image-only files still need external OCR.
- Give sidebar thumbnails and page numbers separate space at 100%, 125%, 150% and 200% display scaling.
- Preserve existing/imported font programs instead of applying new-font subsetting indiscriminately. Normalize merged font state before later text edits; fix four public CJK/font crash cases across merge, insert and duplicate workflows.
- Restrict the cooperative UI render attempt to small text-only pages. Images and Forms render in background processes, which recover from the tested malformed JPX failure.
- Preserve default/view/print optional-content group states across merge, insert, duplicate, extract and page organization; keep unrelated groups with identical names distinct. Imported pages remain vectors and text.
- Repair missing/non-stream references in malformed Contents arrays on private import copies, retaining valid content and the original file.
- Retry the tested crashing JPX page with Windows’ built-in PDF renderer. Its colors are not equivalent, so compatibility preview is clearly marked read-only and cannot be printed; each owned raster worker has time and memory bounds.
- Test 4,211 public PDF paths from pinned PDFium, PDF.js and veraPDF corpora (4,206 distinct contents). Record actual passes, passwords, malformed files, time limits and remaining engine/import differences in [Compatibility](Compatibility.md); the test corpus is not bundled.

## Earlier v0.8.0 improvements

- Use a bounded raster pool (up to two independent PDFium processes) for complex pages, viewport tiles and thumbnails. Immutable shared input and raster mappings avoid per-worker PDF copies and explicit pixel-buffer copying.
- A cooperative progressive-render fast path handles small text-only first pages; expensive rendering continues in workers. Visible requests take priority over neighboring-page prefetch.
- Reuse dimension/rotation keyed LRU images across unchanged layouts and zoom revisits. Cache page dimensions and index visible rows for long documents.
- Batch merged pages by source so shared images/fonts are imported once, then restore the requested page order in one permutation. Repeated pages use separate batches to preserve independent content and annotation editing.
- Reuse immutable bytes for undo snapshots and a bounded serialization buffer. Forward later opens directly to the running window before unpacking another engine.
- Add Ctrl+6–9 inside the page workspace: all-file page interleave, all-file N-page blocks, natural filename sort and reversal within each file. Unequal lengths, original rotations and undo are preserved.
- Separate input, scheduling, thumbnail caching and open-request handling into readable source modules; remove obsolete native bindings. See the measured results and native-engine status in [Performance](Performance.md).

### Existing merge refinements

- File stacks use a dedicated + / - control. Expanding replaces the stack with its real pages; the first page is never duplicated as an extra cover.
- Permanent file dividers and a separate blue insertion line make file boundaries and the exact drop position visible. Drag hints show the target file and original page number.
- Drag an expanded file's title to move the entire file; drag its page image to move a page. Hover to expand a collapsed destination, or drag near the edge to scroll. Esc, capture loss and release outside the board cancel safely.
- Alternate odd/even scans, start with either file, reverse the second scan, or alternate blocks of N pages. Each arrangement is undoable and keeps original page provenance and rotations.
- Ctrl+1 regroups by source and original page number; Ctrl+2 through Ctrl+5 choose interleave modes inside the workspace. Reading shortcuts retain their usual behavior outside it.

## Included tools

- Native print workspace with preview, printer properties, page setup, ranges, odd/even filtering, reverse order, copies, collation and driver-supported duplex.
- Fit, actual size, shrink-only and exact custom print scale; 2/4/6/9/16-up, booklet imposition, poster tiles with overlap and crop marks.
- Default printing sends PDF text and graphics directly through PDFium to the Windows printer context. It avoids a fixed-resolution page screenshot. Optional image printing offers 300, 600 or 1200 DPI.
- Draw, type or import a visible signature. Add text or image watermarks to selected pages, with rotation, opacity, tiling, page counters and preview.
- Real SHA-256 certificate signatures using Windows cryptography, with separate integrity, revision-coverage and offline certificate-trust results.
- Drag PDF or image files into Combine Files, expand groups into page thumbnails and drag individual or selected pages between groups.
- Page organizer with multi-selection, drag sorting, duplicate, rotation, deletion and undo/redo. Apply to the current PDF in one undoable operation.
- Visible **Select**, **Hand**, **Highlight** and **Pages** toolbar controls. Use V for selection, H for the hand, Esc to return to selection, or hold Space to pan temporarily.
- Highlight selected text immediately using twelve preset colors or a custom color.

## Getting started

1. Place Mini PDF.exe in a writable folder and double-click it. No installer or activation is required.
2. Open a PDF with Ctrl+O, or drop PDF files on the window. Additional files get tabs; the selected tab supplies the reading area.
3. Use Select to select and copy text. Use Hand to drag a zoomed page freely in both directions.
4. Click Edit to expand object, text, image and drawing tools. Use Object and double-click a text block to change it.
5. Use Pages to organize the current document, or Menu > Pages > Merge PDF files to combine files into a new document.
6. Save with Ctrl+S, or save a copy with Ctrl+Shift+S. Ctrl+P opens the print workspace.

The app uses the .NET Framework 4.x supplied with Windows. Node.js, Python, Electron and a modern .NET SDK are not needed to run it. It does not require an account, add advertising or telemetry, or change your default PDF association.

**Compatibility:** tested on Windows 10 Home Chinese x64, build 19045. Windows 11 uses the same target APIs but still needs testing on a separate Windows 11 machine. No x86 or ARM64 build is provided.

## Compact reading interface

The collapsed menu, rounded draggable document tabs and window buttons share one top row. The reading toolbar sits below. Editing tools, sidebar and status bar are hidden until needed. Reading mode retains a normal system title bar and close button.

- Hold the left mouse button on a tab and drag continuously to any position. Release to finish; Esc restores the previous order. Edge hovering scrolls overflowing tabs.
- A tab's ×, middle click, Ctrl+W or Ctrl+F4 closes it. Unsaved documents prompt before closing.
- Ctrl+Tab / Ctrl+Shift+Tab cycle tabs. Opening the same file selects its existing tab; later Explorer opens normally enter the existing window.
- Ctrl+Shift+N or the --new-window argument opens an independent window.
- Single, double, book, continuous and grid layouts show pages of the selected document. Grid view adds columns when zoomed out; double-click a page to return to single-page reading.
- Ctrl+6 fits a single page; Ctrl+7 fits the current two-page spread to width; Ctrl+2 fits the current layout to width.
- Exact decimal zoom supports 1%–6400%. Enter a value such as 123.5% and press Enter. Manual 100% retains its actual scale.
- Fit Width minimizes external side gutters to about 0–1 pixels, with possible two-pixel spread rounding. Page gaps are four pixels; Windows scrollbars are about ten pixels at 100% display scaling.
- High zoom renders visible areas as native-resolution tiles. The image-cache target is 64 MiB; individual pages and document histories have separate limits.

**Hand and selection:** click Select or press V to select text; click Hand or press H for left-button panning. Esc returns to selection. Holding Space temporarily pans without changing the selected tool; middle-button panning also works. V/H shortcuts apply while the reading area has focus, so text fields retain ordinary typing.

## Editing and page management

| Area | Available features |
| --- | --- |
| Existing text | Preserve original fonts for supported top-level text edits; native preview, same-line/paragraph grouping, local wrapping, explicit font replacement and one-step undo |
| New content | Add Chinese/English multiline text, images, rectangles and visible signatures |
| Objects | Select, move, resize and delete text, images and shapes; double-click text or notes to edit |
| Annotations | Highlight, underline, strikeout, ink, sticky notes and annotation removal |
| Highlight colors | Select text, then choose a toolbar or right-click highlight color; twelve presets plus a full custom color picker |
| Pages | Insert blank pages, duplicate, rotate, delete, extract, reorder and combine |
| Watermarks | Text/images, opacity, angle, position, tiling, page counters, ranges, Mini PDF replacement and checked third-party watermark removal |
| Navigation | Thumbnails, outline, current-page annotations, search, text selection/copy, internal and web links |
| Forms | Create, edit, fill and manage common AcroForm fields; Unicode XFDF data import/export |
| Export | Editable Word DOCX / Excel XLSX, current-page PNG, document TXT and printing |
| History | Per-document undo/redo, up to twelve steps with a 64 MiB target |
| Save | Reload validation, atomic replacement, safe new-font subsetting and preservation of existing encryption |

In continuous or facing-page views, hold the left mouse button and drag across page boundaries. Drag near a vertical edge to scroll; copy keeps document order even when selecting backwards. Highlight applies to all selected pages and one undo removes the entire operation. Selection alone leaves the PDF unchanged.

Select text in Select mode, then choose a color from Highlight or the right-click menu. Ctrl+Alt+H uses the most recent highlight color. With no text selected, the highlight tool supports a rectangular area. Highlights and other markup use standard PDF annotations.

### Organize pages and combine files

Open Pages or press Ctrl+Shift+P to manage the active document. Expand a file group to see its individual pages. Ctrl-click selects multiple pages; Shift-click selects a range. Hold the left mouse button to drag selected pages to another position. Rotate, duplicate, remove, undo and redo are available. Apply commits the whole operation to the active document; its normal undo restores the previous state. Destinations attached to retained original pages are preserved during reorder/delete; links to deleted pages may no longer resolve.

Combine Files accepts dropped or selected PDF, PNG, JPEG and BMP files. Drag collapsed groups to change file order, or expand groups and drag pages across them. Images are placed on A4 pages. Combining creates a new unsaved document and preserves the original files. Word/Excel import is not provided. Source outlines are not unified. Standard AcroForm field trees and appearances are restored; arbitrary form actions and document automation are not reconstructed. Default/view/print optional-content group states are preserved in the tested merge workflows, including distinct groups with identical names; arbitrary named alternative configurations are not all retained. Importing layered or form pages into an encrypted target is rejected without changing that target; combining an encrypted source into a new unencrypted output is supported. Three native merge comparisons remain unresolved; see [Compatibility](Compatibility.md). Each workspace supports up to 10,000 pages and twenty workspace undo steps.

The + / - square expands or collapses a file without adding a separate cover. The file title above its first page is the handle for moving the whole file; the image below selects and moves that page. Other thumbnails retain their source filename and original page number. Hover for the full filename. Wider thumbnail gaps and darker gray dividers separate files; the blue capped line marks the actual insertion point, with a destination hint. Hover over a collapsed destination to expand it; edge dragging scrolls. Releasing outside the board or pressing Esc cancels the move.

**Arrange scans:** A and B below mean the first two non-empty file groups in the current workspace order.

| Workspace shortcut | Arrangement |
| --- | --- |
| Ctrl+1 | Regroup by source, using original import order and page numbers; retain duplicates and rotations |
| Ctrl+2 | A1, B1, A2, B2... (odd scan first) |
| Ctrl+3 | B1, A1, B2, A2... (even scan first) |
| Ctrl+4 | A1, B-last, A2, B-previous... (second scan in reverse order) |
| Ctrl+5 | Alternate two-file N-page blocks; for N=2: A1, A2, B1, B2... |
| Ctrl+6 | Interleave every non-empty file group, one page each: A1, B1, C1, A2, B2... |
| Ctrl+7 | Interleave every file in blocks of N pages: A1, A2, B1, B2, C1, C2... |
| Ctrl+8 | Natural filename order: scan1, scan2, scan10; keep each file's page order |
| Ctrl+9 | Reverse pages inside each current file group; keep the file order |

Use the Arrange menu for the same commands. Ctrl+2–5 operate on the first two non-empty groups and retain later groups. Ctrl+6–7 include all non-empty groups in their current order, skipping exhausted inputs until every remaining page is included. Interleaved inputs become one expanded group with each page's original source details intact. Press Ctrl+1 before choosing another interleave mode, or Ctrl+Z to undo. Regrouping does not recreate deleted pages. These shortcuts apply only in Combine Files / Organize Pages; Ctrl+2 still means Fit Width in the reader.

Closing a changed Combine Files workspace asks whether to save the current arrangement as a PDF. Yes opens native Save As; No discards; Cancel keeps editing. Canceling Save As or a failed export keeps the workspace intact. View-only expansion and unchanged page order do not trigger a prompt. Closing changed Organize Pages offers to apply the changes instead; application remains one undoable document operation. Save As requires a separate filename if the destination is already open in the reader.

### Scanned PDFs with a text layer

Existing OCR text can be read, selected across pages, copied, searched and edited when it is a top-level text object. Editing preserves the text's invisibility/transparency and the original scan. Changing the recognition layer does not alter letters inside the scan image. Nested Form text remains readable/searchable but cannot be edited character by character. No OCR engine is bundled.

## PDF to Word / Excel and form tools

Use **Menu > File > Convert PDF to Word… / Excel…**. Select all/current/custom pages and preview the first selected page. Word exports editable text, basic fonts/colors, aligned tables and extracted images to DOCX. Excel groups text by position into editable cells, with one worksheet per PDF page; turn off automatic column detection for a single text column. Conversion runs in a separate, cancelable process and leaves the PDF untouched.

These are content conversions, with approximate layout reconstruction. Complex multi-column pages, merged cells, charts and reading order may need correction. Image-only pages remain images in Word and contain no recognized cells in Excel. Existing text layers are usable, but **no OCR is included**. Text that looks like an Excel formula is exported as literal text; leading-zero identifiers are preserved.

Press **Ctrl+Shift+F** or choose **Menu > File > Form tools…**. Select a field, change its value on the right and choose Update field. Enable Design fields to add text, multiline/password fields, checkboxes, radio groups, dropdowns and single/multiple-selection lists. Drag to create/move, or drag a corner to resize. Name, tooltip, default/current values, options/export values, font size, alignment, length, read-only/required/NoExport flags and field order can be edited. Radio groups can have multiple selectable widgets. Undo/redo works inside the workspace.

Apply to document commits the form changes as one undoable operation; **Ctrl+S then saves the PDF**. Import/export uses standard Unicode XFDF data. Reset values respects defaults and read-only fields. Standard field parents, values and appearances are retained during tested merge/insert/duplicate/extract operations; copied fields get independent names.

This covers common **AcroForm** authoring and filling. Dynamic XFA, JavaScript calculations/actions, submissions and editing existing signature/action-button fields are unsupported. Digitally signed documents and encrypted PDFs remain view-only in this workspace; it does not remove encryption to enable editing. Certificate signing remains a separate tool. See [Conversion and forms](OfficeAndForms.md) for details and verification.

## Printing

Ctrl+P opens Mini PDF's own native Windows print workspace. Printer Properties and Page Setup use Windows/the printer driver. It offers:

- All/current/custom pages, explicit ranges such as 5,1-3,8, odd/even filtering and reverse order.
- Copies, collation, driver duplex defaults, simplex, long-edge or short-edge duplex where supported.
- Fit printable area, actual size, shrink oversized pages or an exact custom percentage; auto/portrait/landscape orientation.
- Driver paper sizes, choosing paper from PDF page dimensions, optional extra margins, grayscale and annotation inclusion.
- 2/4/6/9/16 pages per side, booklet pairs with blank padding/fronts/backs/right binding, and poster tiles with overlap/crop marks.
- A preview of the same layout and sheet sequence used by the print job.

The default sends source PDF text and vector paths to the Windows printer device context using PDFium's native EMF output. Viewer zoom does not affect print resolution. Existing image detail stays limited by the source image; complex transparency or printer drivers may rasterize some content. Actual paper detail, color, minimum margins and duplex depend on the printer and its driver. Mini PDF cannot make a low-resolution scan sharper than its source.

Enable **Print as image (compatibility mode)** only when needed. Choose 300, 600 (default) or 1200 DPI. Rendering uses bounded tiles instead of allocating a full high-resolution page bitmap. Existing AcroForm appearance streams are prepared in temporary in-memory copies for printing; the open PDF and its form fields are preserved.

Booklet printing normally uses landscape with short-edge duplex. For manual duplex, use front-only and back-only sequences and follow your printer's feed direction. Poster output is one-sided tiled artwork; check the preview and paper selection before printing.

## Signatures, watermarks and certificates

**Visible signatures:** open the signatures and watermarks menu and choose Place visible signature. Draw with the mouse, type a name or import an image. Name entry stays enabled in every mode and typing automatically switches to typed preview. Chinese names use a suitable Windows CJK font. Image white backgrounds can be removed. Set Page width (mm); the preview responds and height remains proportional. Confirm, then click the PDF to place that size or drag a rectangle for another size. A mouse-following preview shows placement. The image is selected immediately; drag it to move, use the bottom-right corner to resize without distortion, or right-click to enter a scaling percentage. Esc cancels placement. This is editable artwork, not cryptographic identity; it is not stored as a reusable file or uploaded.

**Watermarks:** add text or images with color, opacity, rotation, center/top/bottom, foreground/background, tiling and page ranges. Text supports {page} and {pages}; preview shows the current page and Apply is undoable. Automatic replacement recognizes Mini PDF-tagged watermarks.

**Remove watermarks:** the removal workspace also works with third-party PDF objects. Choose a preview page, filter the candidate list, and check the text, image, whole Form group or stamp annotation to remove. Show all page objects exposes content omitted by the initial candidate filter. Before/after preview leaves the document untouched until Remove checked objects. Match same watermarks searches the specified page range for equal content/style and similar page-relative position/size. A separate button selects Mini PDF-tagged watermarks. The whole removal is one undoable document edit. Complex groups are removed as a whole, so inspect the preview. Large images without a computed fingerprint, or stamps without identifying content, may need per-page selection. Watermarks baked into scan pixels or combined with body content cannot be independently erased by object deletion; this does not provide image inpainting or guaranteed universal watermark recognition.

**Certificate signatures:** select an existing Windows personal certificate with a private key or load a PFX/P12 with its password. PFX keys are loaded ephemerally; Mini PDF does not install the certificate or persist its private key. Certificate signing creates a separate copy with a SHA-256 detached CMS approval signature. Existing AcroForm fields are preserved. Place any visible signature artwork before signing; the certificate approval signature itself is invisible.

Signature inspection separates cryptographic byte integrity, coverage of the current file and offline Windows certificate-chain trust. A self-signed certificate can have valid integrity while remaining untrusted. Bytes appended after a signature are flagged as later, unverified content. Signed documents open read-only; Save As copies their original bytes to preserve signatures.

Current certificate scope is deliberately bounded: PDFs below 128 MiB, one new approval signature on an unsigned editable PDF, no encrypted/XFA signing or additional signatures on already signed documents. Inspection supports detached CMS signatures, including the basic integrity of CAdES detached signatures. It does not validate trusted timestamps, revocation, DocMDP permissions or later revisions, or provide PAdES/LTV qualification, cloud signing or legal-identity assurance. Unsupported signatures are reported rather than marked valid. Certificate viewing uses Windows UI.

## Languages and shortcuts

Menu > Language lists English first and 简体中文 second. Changes apply immediately to the current window while keeping document content, edits and reading state. Each new independent window starts in English; no language-settings file is created. Windows-owned file, certificate and driver dialogs follow Windows display language. Help lists the English guide first and the Chinese guide second; F1 opens English help.

| Action | Shortcut |
| --- | --- |
| Open / save / save as | Ctrl+O / Ctrl+S / Ctrl+Shift+S |
| New PDF / independent window | Ctrl+N / Ctrl+Shift+N |
| Fit page / fit width / actual size | Ctrl+1 / Ctrl+2 / Ctrl+0 |
| Automatic single / two-page width | Ctrl+6 / Ctrl+7 |
| Book / grid | Ctrl+8 / Ctrl+9 |
| Select / hand / temporary pan | V / H / hold Space |
| Highlight selected text | Ctrl+Alt+H |
| Form tools | Ctrl+Shift+F |
| Organize pages / print | Ctrl+Shift+P / Ctrl+P |
| Find / next result | Ctrl+F / Enter or F3 |
| Undo / redo | Ctrl+Z / Ctrl+Y |
| Sidebar | F12 or F9 |
| Reading mode / full screen / presentation | Ctrl+H / F11 / Ctrl+L |
| Exit tool/mode or cancel tab drag | Esc |
| Collapsed menu | F10 |

## Portable operation and size

The EXE uses Windows LZMS compression with no additional decompression library. The first window extracts its engine, guides and open-source notices into a hidden .MiniPDF-runtime-* directory beside itself, and directs its TEMP/TMP there. Normal exit removes generated files; the next launch cleans stale runtime directories after an unexpected exit. Active instances and unrelated files created by the user in a runtime directory are protected.

Later opens are sent to the existing window before extraction. Rendering workers use the already extracted engine and create no additional engine copies; worker state is released after idle time or application exit.

The portable folder must be writable. Save documents outside the runtime directory. The program source remains readable; removing whitespace does not reduce the compiled application. Test programs, Python dependencies, debug symbols and build tools are excluded from the EXE.

## Current limits

This remains a preview. Text editing supports local same-style horizontal blocks, rather than document-wide Word-style flow. Explicit replacement fonts may change metrics. Outlined or nested Form text, vertical/complex-script text and ActualText metadata cannot be faithfully rewritten by this editor. Dynamic XFA, form JavaScript/actions/submissions, built-in OCR, permanent redaction, measurement, document-wide reflow, annotation replies or Adobe cloud workflows are included. Existing links can be followed but new link creation is not provided. Very large PDFs can exceed the history/cache targets, especially with multiple tabs. Public adversarial tests still expose engine-level image, page-tree and layer-import limitations; see [Compatibility](Compatibility.md) for the exact scope and remaining cases.

## Build and verification

From the complete repository on x64 Windows:

~~~powershell
powershell -ExecutionPolicy Bypass -File .\源码\build.ps1
~~~

The script uses the Windows .NET Framework C# compiler, downloads pinned PDFium **158.0.8111.0 x64** without V8/XFA and verifies SHA-256. Downloads, builds and tests stay in the project's work directory. Output is Mini PDF.exe in the root.

Basic program tests run with build.ps1 -Test. Full public-fixture coverage first needs the pinned download/run described in [Compatibility](Compatibility.md). Install development packages with python -m pip install -r .\源码\requirements-dev.txt, plus OpenSSL for independent CMS verification:

~~~powershell
python .\源码\verify_pdf.py fixtures .\work\tests
python .\源码\verify_features.py fixtures .\work\tests
python .\源码\verify_fixes.py fixtures .\work\tests
python .\源码\selection_fixtures.py .\work\tests .\work\compatibility
python .\源码\verify_text_edit.py fixtures .\work\tests
python .\源码\verify_office_forms.py fixtures .\work\tests
powershell -ExecutionPolicy Bypass -File .\源码\build.ps1 -Test
python .\源码\verify_pdf.py verify .\work\tests
python .\源码\verify_features.py verify .\work\tests
python .\源码\verify_fixes.py verify .\work\tests
python .\源码\verify_merge.py verify .\work\tests
python .\源码\verify_selection.py .\work\tests
python .\源码\verify_compatibility.py .\work\tests
python .\源码\verify_text_edit.py verify .\work\tests
python .\源码\verify_office_forms.py verify .\work\tests
powershell -ExecutionPolicy Bypass -File .\源码\test-launcher.ps1
~~~

Run the pinned public corpus with test-corpus.ps1 as described in [Compatibility](Compatibility.md). Its archives, fixtures and probe executables stay in work and are excluded from the app.

Missing external fixtures are explicitly skipped by the program runner. These packages and generated test certificates are not needed by the application. Tests inspect native controls and real mouse/key messages, persisted PDFs, page order/destinations, OCR image preservation, print geometry/vector records and form appearances, CMS signatures/tamper detection, and isolated EXE startup/cleanup. Printing tests use preview controllers and do not send physical print jobs. The detailed test record reports the verified build, counts and hash. Passing tests do not guarantee every PDF or printer configuration is free of defects.

## Technology, references and licenses

C# 5 / Windows Forms and .NET Framework; PDFium for PDF processing; the built-in Windows PDF renderer for explicitly restricted compatibility previews; Windows GDI/EMF for printing; Windows Cryptographic APIs via System.Security for CMS and certificate chains; Windows Compression API for LZMS packaging.

Reading behavior and compact tabs are inspired by [SumatraPDF](https://github.com/sumatrapdfreader/sumatrapdf); feature references include [PDF Looker Pro](https://github.com/dongfangxy/pdf-looker-pro) and Adobe Acrobat's printing/page workflows. Mini PDF uses its own application code and does not bundle or link SumatraPDF, MuPDF, Electron or Adobe SDKs. MuPDF is used only as an independent development-test renderer.

Application/launcher code is MIT licensed. [PDFium](https://pdfium.googlesource.com/pdfium/) and its dependencies retain their separate notices under third-party-licenses and inside Help > Open-source licenses. These notices are attribution requirements, not activation or payment requirements. Retain them when redistributing.
