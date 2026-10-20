# Changelog

[English](CHANGELOG.md) | [简体中文](CHANGELOG.zh-CN.md)

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
