# Changelog

## 0.11.0 — 2026-10-23

- Add Preserve layout and Continuous text Word export. Keep ordinary text editable at source positions or merge it into normal flowing Word paragraphs.
- Fix effective font sizes, generated-space ordering, large intra-object gaps and simple two-column reading order.
- Preserve complete complex-equation regions and numbering, along with figure labels, using bounded high-resolution PDFium images. Retain staggered source-column order before extraction and flow selected pages into one Word section.
- Optionally infer simple equalities, subscripts/superscripts and uncomplicated fractions into OMML in continuous mode. Keep uncertain structures as artwork; adjustable local rules, no OCR/model/runtime addition.
- Preserve pre-existing temporary-path files on failed exports and continue atomic destination replacement.
- Document conversion boundaries and add native-worker, Unicode, formula-semantic, raster-fallback, repeated-page and failure-preservation checks.

## 0.10.1 — 2026-10-23

- Select the exact text to edit, including multiple lines, mixed fonts and pages. Edit, Delete and percentage resize act on the entire supported selection; unselected text at either end stays in place.
- Press F2 or double-click a text object to edit directly in the document. Ctrl+Enter applies; Esc cancels. Choose **Edit in window** for the independent preview editor, and switch back without losing the draft.
- Preserve unchanged character spacing and style fragments. Display the effective font size when a PDF uses a small font size with a scaled text matrix.
- Preview each selected page, preserve page boundaries in multi-page replacements, and keep the whole edit in one undo step.
- Fit native prompt buttons to high-DPI text so their captions remain visible.


## 0.10.0 — 2026-10-23

- Convert PDF content to editable Word DOCX or Excel XLSX using built-in ZIP/XML APIs. Preserve Unicode, common aligned tables, numbers/identifiers and optional Word images; no Office installation is required.
- Add a standard AcroForm workspace for creating, moving, resizing, copying, deleting, filling and ordering common fields, with default values, choices, flags, local history and XFDF data import/export.
- Render native widget appearances and reconnect imported field trees/resources during merge, insertion, duplication and extraction. Keep the original files unchanged.
- Use a bounded export worker with cancellation and owned-temporary-file cleanup; fix repeated progress-window disposal. Correct high-DPI form property layout and pending checkbox Apply state.
- Add an existing-text editor with original-font updates, native PDFium preview, conservative same-line/paragraph grouping, width-based wrapping, line spacing and fractional font sizes.
- Keep original text transformations, baseline/spacing, separate fill/stroke opacity, clipping and marks for supported single-object edits. Validate extracted Unicode after every change and roll back on failure.
- Require explicit font replacement when original glyph coverage cannot be confirmed. Preserve hidden OCR rendering; reject unsupported vertical/complex shaping, text clipping and ActualText replacement metadata.
- Add repeatable backend/UI and independent PDF/font/pixel preservation checks, plus a headless CI editing step. No new production dependency or PDFium rebuild.
- Retry incomplete corpus inventory downloads and handle gzip responses; continue direct-first downloads with configured proxy fallback.

## 0.9.0 — 2026-10-22

- Verification follow-up: confirm apparent save regressions with exact independent PDFium snapshots, preserve replay diagnostics, retain CI artifacts and update pinned actions to Node 24. The full 4,211-path gate passes with zero new regressions. Application/PDFium binaries and the historical baseline remain unchanged. [Evidence](源码/CI-Verification.md).

- Add cross-page text selection, reverse/document-order copying, edge scrolling and one-step multi-page highlighting.
- Improve hidden OCR hit testing and separate thumbnail/page-number layout at multiple display scales.
- Preserve imported/existing font programs and normalize merged font state; fix four public font crash cases after merge/insert/duplicate plus text editing.
- Restrict the UI quick raster path to small text-only pages; isolate malformed image failures in background workers and suppress worker crash dialogs.
- Preserve default/view/print optional-content group states across merge, insert, duplicate, extract and page organization; keep unrelated groups with identical names distinct. Imported pages remain vectors and text.
- Repair missing/non-stream references in malformed Contents arrays on private import copies, retaining valid content and the original file.
- Retry the tested crashing JPX page with Windows’ built-in PDF renderer. Its colors are not equivalent, so compatibility preview is clearly marked read-only and cannot be printed; each owned raster worker has time and memory bounds.
- Add pinned, hash-verified public-corpus download/probe/reference tools and per-file compatibility results for 4,211 PDF paths. Reduce baseline merge comparison differences from 143 to 3 and native failures from 5 to 1. Retain unresolved cases and independent evidence.
- Keep PDFium unchanged and exclude test corpora/development tools from the single portable EXE.

## 0.8.0 — 2026-10-21

- Add bounded background raster workers with immutable shared inputs, mapped pixel buffers, visible-page priority, adjacent-page prefetch, cancellation and process recovery.
- Render directly into owned output buffers; use cooperative progressive rendering for ordinary first pages without worker startup delay.
- Cache page sizes and visible rows; retain dimension/rotation keyed LRU images across unchanged layouts and zoom revisits. Render only viewport tiles for larger pages.
- Batch merge imports by source, reuse shared resources and restore exact page order once; isolate repeated pages so indirect annotations and content remain independently editable.
- Move sidebar and Combine Files thumbnails off the UI thread. Reuse immutable bytes for undo snapshots and a bounded PDF serialization buffer.
- Forward subsequent opens before single-EXE extraction. Stop workers at idle/exit and dispose canceled, posted and cached images deterministically.
- Add all-file page/block interleaving (Ctrl+6/7), natural filename sorting (Ctrl+8), and reversal within each file (Ctrl+9), preserving unequal inputs, rotations and undo.
- Separate and format input, raster scheduling, thumbnail cache and open-request modules; remove obsolete native imports. Keep the pinned PDFium DLL and rendering-quality flags unchanged.
- Store the application icon once in the launcher and reuse it for the native window.
- Add reproducible performance tools and worker/pixel/lifetime/multi-file regression coverage; update English and Chinese documentation.

## 0.7.2 — 2026-10-21

- Increase page spacing and darken file boundaries. Prompt to save/discard/cancel changed merge workspaces; retain edits after canceled or failed saves. Offer Apply when closing the page organizer.
- Replace expanded file covers with real pages and a dedicated +/- toggle; keep the first page exactly once.
- Add permanent file dividers, accurate before/after insertion lines, full source/page tooltips and drag destination hints.
- Keep file-title dragging distinct from page dragging; support hover expansion, edge scrolling, cancellation and layout changes during a drag.
- Add A-first, B-first, second-file-reversed and N-page-block interleaving, with original-source regrouping and Ctrl+1 through Ctrl+5 inside the workspace.
- Preserve original scan files, page provenance, rotations, later file groups and one-step undo/redo. Keep English and Chinese guides synchronized.

## 0.7.1 — 2026-10-21

- Fix name entry in visible signatures: input is always available, typing switches to a live CJK/Latin preview.
- Add physical width settings with responsive preview, higher-resolution typed/drawn export, click/drag placement and immediate object selection.
- Preserve proportions and support rotated-view corner resizing; allow reselecting signatures over body text.
- Add third-party watermark text/image/Form/stamp selection, before/after preview, page-range content/position matching and undo.


## 0.7.0 — 2026-10-21

- Add a native printing workspace with previews, ranges/parity/reverse, driver properties, paper/orientation, copies/collation/duplex, exact scaling, N-up, booklet and poster layouts.
- Preserve PDF vector text/graphics through the native Windows print device context by default; offer 300/600/1200 DPI image-print fallback and prepare existing form appearances without changing the original.
- Add visible draw/type/image signatures, reversible batch text/image watermarks, SHA-256 CMS certificate signing and separate integrity/current-file/offline-trust inspection.
- Add a drag-and-drop combine workspace and current-document page organizer, multi-selection, cross-file page sorting, duplicate/rotate/delete and undo/redo.
- Add quick Select/Hand/Highlight/Pages controls, V/H/Esc and temporary Space panning, selected-text highlighting with twelve colors and custom colors.
- Preserve compact title tabs and English-first bilingual UI; update both guides and document certificate/printing limits.

[English](CHANGELOG.md) | [简体中文](CHANGELOG.zh-CN.md)

## v0.6.1 — 2026-10-20

- Merge the menu, rounded blue tabs, and minimize/maximize/close controls into the top title row. Remove the old blank title strip and raised tab borders; use flat toolbars and menus.
- Fix drag capture loss caused by removing native TabPages during a move. Keep slots alive while reordering documents and display a tab that follows the pointer.
- Support one continuous left-button hold across multiple tabs, reversing direction and dropping at any position. Esc restores the previous order; edge hovering reaches hidden tabs.
- Preserve each document's content, edits, selected page, exact zoom, scroll position and undo history.
- Keep native window movement, resize and caption hit codes, double-click maximize/restore, reading mode with a normal caption, and true full screen.
- Fix maximized work-area geometry and the extra caption height added on restore.
- Remove the original system caption in normal mode so there cannot be two rows of window controls. Keep it only in reading mode; check both non-client sizing variants and the actual packaged EXE.
- Update both embedded guides and English-first / Chinese-second documentation.
- EXE: 2,955,264 bytes, approximately 2.82 MiB; only 4 KiB larger than v0.5, with no new runtime dependency.
- Pass 238 program regressions, 33 independent PDF checks, and 14 portable-launcher checks: 285 in total on Windows 10 x64. Windows 11 has not been separately machine-tested.

## v0.5.0 — 2026-10-20

- Place document tabs above the reading toolbar, with the menu button at the left of the same tab row.
- Add tab reordering by dragging, with edge scrolling for overflowing tabs, Escape cancellation, and safe capture-loss handling.
- Preserve the selected document, page, exact zoom, scroll position, editing tools, unsaved changes, and undo history during reordering.
- Enlarge the menu icon and click target, toolbar text, common icons, and menu text while keeping compact rows.
- Keep a dedicated Fit Width button and `Ctrl+2`; retain `Ctrl+6` for automatic single-page fit and `Ctrl+7` for automatic two-page width.
- Add a default English application interface and an immediate English / Simplified Chinese language menu. Keep English first and Chinese second.
- Embed both user guides and expose them from Help, with English first. Translate menus, toolbars, context commands, editing dialogs and application messages while preserving user data.
- Make English the default repository and source documentation, with complete Chinese documentation as the second language option.
- EXE: 2,951,168 bytes, approximately 2.81 MiB. No Electron, browser runtime, or additional application dependency was added.
- Pass 210 program regressions, 33 independent PDF checks, and 12 portable-launcher checks: 255 in total, on Windows 10 x64. Windows 11 has not yet been tested on a separate machine.

## v0.4.0 — 2026-10-20

- Add native document tabs with independent page, zoom, view, scroll position, and undo history. Only the selected PDF appears in the reading area.
- Forward subsequent PDF opens to the existing window; support multiple filenames, drag-and-drop, duplicate-file detection, and save prompts when closing modified tabs.
- Add `Ctrl+Tab` / `Ctrl+Shift+Tab`, `Ctrl+W` / `Ctrl+F4`, and `Ctrl+Shift+N` for tab navigation, closing, and a separate window.
- Add automatic single-page and double-page width shortcuts and toolbar buttons.
- Fix outer gray strips caused by hidden-sidebar space and duplicate scrollbar reservations; fit actual page or spread sizes, including mixed paper sizes and odd final pages.
- Use approximately 10-pixel native scrollbars and 4-pixel page gaps. Preserve physically meaningful manual zoom percentages.
- Preserve the native title bar and each tab's editing tool when leaving reading mode.
- EXE: 2,940,928 bytes, approximately 2.80 MiB; 8 KiB larger than v0.3.
- Pass 166 program regressions, 33 independent PDF checks, and 12 portable-launcher checks: 211 total. Windows 11 was not separately machine-tested.

## v0.3.0 — 2026-10-20

- Introduce one compact toolbar, collapsed menus, and on-demand editing tools.
- Keep the native caption and window controls in reading mode; retain true borderless full screen.
- Expand precise zoom to 1%–6400% with native-resolution viewport tiles and a bounded image cache.
- Fix selection of hidden OCR text behind a scan image; preserve invisible and transparent text properties when editing.
- Keep normal text-field shortcuts such as `Ctrl+A` in the zoom input.
- EXE: 2,932,736 bytes, approximately 2.80 MiB.
- Pass 129 program regressions, 33 independent PDF checks, and 6 launcher checks: 168 total.

## v0.2.0 — 2026-10-20

First public preview.

- Native Windows 10 / 11 x64 controls and a portable single EXE.
- Single-page, double-page, book, continuous, and grid layouts; precise integer and decimal zoom input.
- Reading mode, full screen, presentation, collapsible sidebar, and native context menus.
- Original-text replacement, Chinese and English text insertion, image and shape manipulation, annotations, page management, export, and printing.
- Validated atomic saving, undo/redo, font subsetting, password handling, and document permissions.
- Fix startup shortcut validation, context-menu lifetime, bitmap-buffer lifetime, font mapping, and saved-state tracking.
- Exclude test code from the release payload and use Windows LZMS compression; portable EXE approximately 2.79 MiB.
- Pass 99 program regressions, 22 independent PDF checks, and 6 launcher checks on Windows 10 x64. Known limitations remain documented in the README.
