# Existing-text editing / 原文字编辑

English first; 简体中文 follows. Introduced in v0.10.0.

## Using the editor

Select text and press F2 for in-document editing, or use Edit in window for the independent preview editor. The original font is the default. Choose the current text object, adjacent same-line fragments or a conservative same-style horizontal paragraph. Change the text, font size or color; for supported blocks, enable wrapping and set the width in millimeters and line spacing.

The right pane shows an actual PDFium-rendered preview, with a text-area/whole-page toggle. Apply becomes available after a successful preview. Apply changes the real PDF text, without adding a white rectangle. The document's normal Undo restores the complete operation. Preview and Cancel leave the PDF untouched, and accepting unchanged content creates no history entry.

New characters are accepted in the original font only when its glyph coverage can be confirmed. Existing embedded subsets often contain few characters. Choose Auto or an installed system font explicitly to add unsupported characters; inspect the preview because replacement metrics can differ. No font pack is shipped.

## Exact selection and two editing views (v0.10.1)

Select text in the reading view and press **F2** to open an editor over the document. **Ctrl+Enter** applies; **Esc** cancels. The expanded Edit toolbar exposes **Edit text** and **Edit in window**. Double-clicking a text object also opens in-document editing. The separate window offers the PDFium preview and font, size, color, wrapping and spacing controls; **Edit in document** switches back. Draft text is retained across switches. Leaving a changed inline draft asks Apply/Discard/Cancel; Cancel retains the draft.

A supported text selection includes all selected lines and mixed-style fragments, rather than only the last clicked PDF object. It preserves the unselected prefix and suffix of partially selected text objects, original unchanged character spacing, style fragments, other columns and the rest of the page. Delete and percentage resize use the same selection. Effective font size includes the PDF text matrix's scale.

Multi-page selections include explicit **Page** separators in the text. Keep these markers so each page keeps its own content. The preview page selector inspects each selected page. Every affected page is generated and verified before committing the transaction; one undo restores all pages. A missing boundary is rejected before mutation.

Range editing is limited to visible, unmarked, unclipped, horizontal top-level text with an exact character-to-object mapping. Unsupported selections are rejected in full. Supported individual-object editing remains available for hidden OCR text or other special object state. The app does not infer a complete semantic paragraph from every PDF. Reflow can change the selected text's layout; inspect the window preview for longer replacements.

Native input prompts now size their buttons to their captions at the actual display scaling.

## Preservation and boundaries

Supported in-place edits retain the original object/font, transformation, baseline, relevant character positions, independent fill/stroke opacity, clipping path and marks. Reflowing a line or paragraph necessarily changes its text objects and line layout. Grouping is conservative: it stops at column gutters, incompatible styling, rotation or large line gaps.

Invisible/transparent OCR text retains its rendering mode and scan image. Correcting a hidden recognition layer does **not** change letters baked into the scan. Nested Form text can be selected/searched by the reader but cannot be edited here. Outlined glyphs are paths, not editable characters.

The editor rejects vertical and unsupported complex-script/combining/supplementary text, text used for clipping and ActualText alternate-text metadata. These need fuller shaping/structure-aware editing. This is local block editing, not document-wide Word reflow or permanent redaction.

Each edit validates extracted Unicode and the whole page again after serialization. A public SimFang sample exposed a PDFium re-encoding failure that removed an unrelated letter; this case now rejects the edit and restores the original state. This guard catches that tested loss, without claiming every possible visual or semantic defect is detectable.

## Reproducible checks

On x64 Windows, install the development requirements and run from the project root:

~~~powershell
powershell -ExecutionPolicy Bypass -File .\源码\test-editors.ps1 -Suite Text
~~~

Use -CoreOnly for an unattended CI environment and -UseExistingBuild when the core was just built. The suite creates fixtures in work/text-edit-tests, compiles a separate friend test executable, and verifies output independently with pypdf and MuPDF. Python, test PDFs, test executables and the independent renderer are not shipped in Mini PDF.

The checks cover standard/embedded/CJK fonts, clipping, marks, hidden text, rotation, stroke colors/opacity, line/paragraph selection, wrapping, rollback, new-font replacement, unchanged confirmation, preview/cancel/apply and undo/redo. The public SimFang guard runs when the pinned corpus has been downloaded; missing cases and missing system CJK fonts are explicitly reported. Current exact counts and release identity are in [Test record](测试记录.md).

Additional public text-object probes rewrite supported first-page text using existing glyphs and compare 600-pixel renders and non-whitespace text with an independent renderer. Such probes complement the fixtures; they do not establish arbitrary editing support for every public PDF.

Implementation: TextEditing.cs, SelectionEditing.cs, InlineTextEditing.cs, TextEditWindow.cs and separate editor tests/verifiers. It reuses the pinned PDFium and system UI, with no production runtime added.

## 中文

选中文字后按 F2，在文档内直接编辑完整选区，Ctrl+Enter 应用、Esc 取消；支持多行、混合字体和跨页。编辑工具栏提供“原文编辑”和“窗口编辑”，两种界面切换保留草稿；独立窗口中取消切换会回到文档内草稿。
跨页文本带 Page 分隔标记，请保留标记；可切换预览页面。修改、删除、百分比缩放覆盖整个支持的文字选区，首尾未选文字保留位置，整次操作可一次撤销。离开尚未应用的文档内草稿时会询问应用/放弃，取消可继续编辑。
隐藏、带标记、剪裁或嵌套文字不支持这样整组选区改写，请按支持范围单对象编辑；删除不是永久抹除。

展开编辑，选择对象工具后双击支持的原文字。默认保留原字体，可改当前对象、相邻同一行片段或同样式横排段落；调整字号、颜色、换行宽度及行距。右侧为真实 PDFium 预览，成功后才能应用，整次修改可一次撤销。预览/取消不改原文档，原样确认不新增历史，不用白色矩形遮挡旧文字。

原字体子集经常缺少新字，无法确认字形支持时需要主动选择“自动”或系统字体，并核对变化后的字宽。单对象修改保留原有变换、基线、字距、透明度、剪裁和标记；换行重排按需改变对象。分组会在分栏、样式、旋转或行距不兼容时停止。

隐藏 OCR 层修改继续保持隐藏，不改变扫描图像。嵌套 Form、轮廓文字、竖排、复杂整形、组合字符、补充字符、文字剪裁和 ActualText 暂不支持保真改写；不提供整篇 Word 式重排或永久抹除。应用后会重新保存到内存并核对整页文字，发现测试过的重编码丢字时回滚。这个检查不代表能发现所有视觉及语义差异。

上面的独立测试入口覆盖字体、隐藏层、旋转、剪裁、颜色、换行、预览、撤销与回滚；测试文件都在 work，不装入应用。确切数量、发行哈希及公开样本的测试边界见测试记录。
