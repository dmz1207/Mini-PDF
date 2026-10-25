# Word export / Word 导出

English first; 简体中文 follows. Updated for v0.11.0.

## Choose how to edit

Open **Menu > File > Convert PDF to Word…**. Choose the pages and an export mode.

| Mode | Result |
| --- | --- |
| Preserve layout (recommended) | Original page positions and dimensions, ordinary text in editable positioned text boxes, high-resolution equations and artwork |
| Continuous text (easy to edit) | Normal Word paragraphs in inferred reading order; source lines merge so body text can be edited and rewrapped continuously |

The two modes solve different needs. Positioned text boxes preserve appearance but do not form one flowing article. Continuous text is easier to edit, but pagination, footnotes, figures, tables and equation placement can change. Complex inline mathematics can still fragment or move when a complete formula region cannot be isolated safely. Formula-heavy lines or paragraphs can remain complete images, so not every character becomes editable. Selected source pages flow into one Word section using the first selected page’s paper size; resulting Word page counts may differ.

Ordinary supported text stays editable. Unsupported Unicode mappings, partially clipped or rotated text and complex artwork may remain images. Continuous mode also accepts readable publisher fonts, ordinary structural tags, text inside transformed Form containers and containing rectangular clips; it substitutes a standard Word font when necessary. Optional-content groups and partially clipped text remain artwork. Image-only scans remain images: this release does not include OCR. Native paragraph recovery works from an existing text layer; adding OCR alone would not recover a PDF's missing paragraph or equation structure.

The preview is a source/reference preview delivered by the isolated raster worker, not a Word rendering. Open the exported DOCX in Word to check its final layout. Mini PDF does not require Word to export.

## Research papers

Scientific papers are the main quality target: recover ordinary prose while protecting symbols and equation structure. A small built-in academic layout detector uses publication marks/DOIs as hints for IEEE, Elsevier, Springer Nature, Wiley, ACM and other common publishers. It measures the actual column count, gutter, dominant body size, line spacing and paragraph indentation on each page before formula and figure extraction. Publisher names never force a column count. Single-column pages, unequal/staggered columns, and full-width title/abstract bands share the same bounded geometry rules. Unknown publishers use geometric detection. Identifiable isolated vertical text notices in outer margins are cropped before remaining artwork is grouped, oriented using their measured text direction, and retained as horizontal images after the page body. Composite or unrecognized notices can still need manual cleanup. Complex formula regions keep their equation numbers where the region can be isolated. Figure labels stay with the figure. Source running headers, footers and captions may remain in the text and need cleanup in continuous mode.

Use Preserve layout when the exact original formula appearance matters. Use Continuous text when editing prose matters more than original pagination. Neither mode recovers arbitrary editable matrices, proofs, diagrams or LaTeX.

## Optional offline equations

In **Continuous text**, enable **Editable simple equations (offline)**. The local C# engine reads PDFium's Unicode symbols, effective font sizes, character baselines and bounding boxes, then emits native OfficeMath (OMML).

The conservative first version handles isolated simple equalities, identifiable subscripts/superscripts and uncomplicated horizontal-bar fractions. A source object must be wholly mapped into the inferred region. Hidden/clipped/marked/rotated objects, uncertain Unicode mappings, overlapping foreign content, ambiguous multi-line structures, roots, large operators, accents and general matrices are not forced into a formula.

Recognizable uncertain math regions are rendered by the existing PDFium at up to 300 DPI and embedded as images. Regions that cannot be isolated safely stay in the page artwork. Charts are not reconstructed as editable Word charts. Even a structurally plausible formula needs checking: this is geometric inference, not recovery of the original LaTeX or an assurance of mathematical correctness.

**Preserve layout never forces formulas into OMML.** Complex formulas remain artwork; simple standard-font symbols can remain positioned text. Editable equations reflow naturally in continuous paragraphs instead of being clipped to small original text boxes.

There is no learned model, online service, OCR pack, PdfPig or PdfiumViewer dependency. The existing PDFium parser/renderer and Windows ZIP/XML APIs are reused. Word fonts and equation spacing can differ from the PDF.

### Adjustable rules

Equation settings apply to the current export window and can be restored to defaults:

| Setting | Default | Allowed range |
| --- | --- | --- |
| Maximum script-to-body size ratio | 0.82 | 0.55–0.92 |
| Minimum baseline offset ratio | 0.18 | 0.12–0.50 |
| Maximum symbol gap ratio | 0.80 | 0.35–1.20 |
| Maximum fraction bar thickness | 0.80 pt | 0.20–1.50 pt |

Ratios use the local nominal font size. More permissive settings can join unrelated symbols or misclassify scripts. Settings are heuristics, not a confidence guarantee or a trained document profile.

## Implementation and limits

- Common Computer Modern Roman/Sans/Typewriter, Latin Modern and Nimbus body families map to standard Word fonts. Computer Modern Roman is treated as prose rather than automatically classified as math. Word substitution is not exact font embedding.
- OfficeGeometry.cs retains sub-point coordinates through a 1/1000-point mapping grid and applies the effective text matrix. Generated spaces use their owning object's matrix; discretionary hyphens are retained.
- WordLayout.cs prepares private snapshots, keeps ordinary text positions/styles and renders the remaining appearance one page at a time. It does not modify the open PDF.
- OfficeMath.cs groups symbols, recognizes limited structures and creates fallback crops. Analysis is bounded; extreme pages retain artwork.
- WordRegions.cs preserves complete complex-equation and figure regions, including their labels, instead of scattering them into fragments.
- AcademicLayout.cs holds the lightweight publication hints and per-page geometric profile; no downloaded templates or model run at runtime.
- WordObjects.cs traverses bounded nested containers and checks containing clips without modifying the open source.
- WordFlow.cs reconstructs column order and merges compatible lines into ordinary paragraphs, with inline image/equation content.
- Background images target 300 DPI, with a 16-megapixel and 14,000-pixel-dimension bound. They are high-resolution raster artwork, not original PDF vectors.
- Export uses the existing cancellable worker and atomically replaces the destination after package validation. Pre-existing temporary-path files are not overwritten or deleted.
- Editing text in Word does not permanently redact information from the original PDF.

The user-supplied eight-page paper is a private local quality sample and is not distributed in the repository. Public reproducible checks use generated fixtures in verify_office_forms.py, with independent ZIP/XML/image inspection and the actual export worker. Local development also checks generated documents through installed Microsoft Word; Office is not shipped or required.

## 中文

“保留版式”保留原页位置，普通文字通过文本框编辑，公式与图形保留高清外观。“连续正文”把行合并成普通 Word 段落，便于全文连续修改、重新换行，但页数、表格、脚注、图片与公式位置可能变化。两种模式不能同时保证原稿外观和完全自由的全文重排。

连续正文可选离线简单公式转换：复用 PDFium 读取符号、实际字号、基线和坐标，用 C# 几何规则生成 OMML。常见 Computer Modern、Latin Modern、Nimbus 正文字体映射到 Word 标准字体；这是字体替代，不是嵌入原字体。首版支持明确的简单等式、上下标和简单分式。根号、矩阵、复杂算子、编码或结构不明确的内容保留图像；能够安全分离的公式区域用最高 300 DPI 图像保留。保留版式模式始终保留公式外观，避免小文本框裁切公式。

参数可在本次导出窗口调整，范围与默认值见上表。放宽阈值可能增加误判，不能保证公式语义正确。重点面向科研论文：内置 IEEE、Elsevier、Springer Nature、Wiley、ACM 等出版标记与 DOI 线索，结合每页实际栏数、栏缝、正文字号、行距和缩进自动匹配。出版社名称不会强制套用栏数；单栏、非居中或交错双栏、跨栏标题与摘要使用同一套有界几何规则，未知出版商也走几何检测。能识别的独立外侧细长竖排文字先单独裁图，按测得的文字方向旋转后保留到正文之后；组合或未识别的边注可能仍需人工整理。连续正文支持可读的出版商字体替代、普通结构标签、嵌套 Form 容器及完整包围文字的矩形裁剪；部分裁剪和不明确的映射仍保留图像。先识别双栏再提取公式和图形，复杂公式及可安全分离的编号整体保留，图表标注跟随图表。连续正文里含复杂公式的行或段落可能仍保留为图像，页眉、页脚和题注可能需要整理。没有 AI 模型、联网服务或 OCR，也不额外引入 PdfPig/PdfiumViewer。已有文字层的正文重建不需要 OCR；扫描页仍是图片。

极端页面和不支持的文字状态可能以图像保留。普通正文、原生公式、图像分别具有不同的可编辑程度。转换从私有副本读取，不修改打开的原始 PDF；确认导出后才替换用户选择的目标文件。导出后请在 Word 中核对实际内容。
