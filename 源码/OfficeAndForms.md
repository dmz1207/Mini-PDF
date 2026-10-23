# PDF conversion and forms / 转换与表单

English first; 简体中文 follows. Introduced in v0.10.0.

## Word and Excel export

Open Menu > File > Convert PDF to Word… or Excel…. Choose all/current/custom pages and preview the first selected page. Export uses standard DOCX/XLSX files and does not require Microsoft Office. It reads the existing PDF content, without installing an OCR engine.

| Output | Behavior |
| --- | --- |
| Word DOCX | Editable Unicode text, basic font/size/color/bold, common aligned tables and optional extracted images; flowing paragraphs and page sections |
| Excel XLSX | Editable cells grouped by source position; one worksheet per selected PDF page; optional automatic column detection |
| Image-only scan | Preserved as an image in Word when images are enabled; no recognized cells in Excel |
| Existing text layer | Extracted directly, including supported filled form values; password fields are masked |
| Identifiers/formulas | Leading-zero and long identifiers stay text; strings such as =2+2 are literal strings, never executable formulas |

This is content conversion with approximate layout recovery. Mixed fonts, complex columns, merged cells, reading order, equations, charts, transparency and image transformations may need correction. Excel does not recreate formulas, charts or embedded illustrations. Word is not a pixel-identical replica and does not retain PDF annotations as editable Word annotations.

A separate export worker receives an immutable snapshot of the current document. It has a 768 MiB process budget. Cancel/close stops only that owned worker and removes its temporary package; completion atomically replaces the chosen destination. Failure leaves the original PDF and a pre-existing destination unchanged. Native conversion failures stay isolated from the reader.

Resource limits include 2,000 selected pages, 256 inferred Excel columns, 32,767 characters per Excel cell, up to 128 extracted images per page and a 16-megapixel per-image decode limit. Excess/unreadable images are reported rather than silently advertised as exported. Extreme files can hit the worker budget or other limits; they are not guaranteed to convert.

## Standard AcroForm tools

Press Ctrl+Shift+F or choose Menu > File > Form tools….

| Task | Action |
| --- | --- |
| Fill | Select a field in the page/list; edit its value, tick a checkbox or choose options on the right; Update field |
| Create | Enable Design fields, choose a field type and drag a rectangle on the page |
| Position/size | Drag the selected field; drag a corner to resize; change the displayed page to work there |
| Properties | Name, tooltip, current/default value, export-value/display-label pairs, size, alignment, maximum text length and flags |
| Field types | Text, multiline text, password, checkbox, radio group, dropdown, editable dropdown, single/multiple-selection list |
| Manage | Duplicate, delete, move up/down, workspace undo/redo; duplicating a radio adds an option to that group |
| Data | Import/export Unicode XFDF; restore default values; respect read-only and NoExport |
| Commit | Apply to document, then Ctrl+S to save the PDF; normal document Undo reverts the whole apply |

Font size 0 means automatic. Choices use one export-value|label pair per line. A radio group's widgets need distinct export values. Full field names can use dots for hierarchy; a field cannot simultaneously be a value leaf and a parent branch. Required fields are counted to help checking; this is not a submission/validation server.

The designer works on a private model. Preview uses PDFium and creates real saved widget appearances, including available system CJK fonts. Original font programs outside new appearances are retained. Common field values and hierarchy are restored during merge/import/duplicate/extract; duplicated fields get independent names. Bad annotation-array entries are skipped on private import copies while valid content is retained. The original input file is unchanged.

### Compatibility boundaries

- Common standard AcroForms are supported; dynamic XFA is not.
- Existing signature/action-button/unknown fields are view-only. Use the separate certificate tool for supported digital signing.
- JavaScript calculations/validation, field/document actions, network submission, arbitrary rich text, barcode generation and Acrobat automation are not implemented. Existing action scripts are not executed.
- Digitally signed PDFs are read-only. Encrypted PDFs are view-only in the form designer; the tool does not silently remove their protection. Data inspection/export can still work when reading is permitted.
- Importing form/layer pages into an encrypted target is rejected with rollback. An encrypted source can be combined into a new unencrypted output with permission; inspect the resulting document.
- Form reset/import changes writable supported fields. Invalid choices or invalid XFDF fail without partially applying that operation. External XML entities are disabled.
- Combining restores ordinary field structure and appearances, not an arbitrary source document's outline, action graph or automation behavior.

This is a useful common-form toolkit, not the complete Acrobat Pro product.

## Verification

From the project root on x64 Windows:

~~~powershell
python -m pip install -r .\源码\requirements-dev.txt
powershell -ExecutionPolicy Bypass -File .\源码\test-editors.ps1 -Suite OfficeForms
~~~

-CoreOnly omits interactive window checks for CI. All generated PDFs, Office files, worker processes and fixtures stay in work. The final software contains none of these development dependencies.

Tests cover original/created fields, Unicode and nested names, check/radio/choice/multiselect values, permissions, XFDF, invalid-data rollback, external-entity rejection, move/resize/cancel, pending Apply state, high-DPI layout, local/document history, encrypted/XFA restrictions and page imports. The export tests exercise the actual worker, repeated window disposal, cancellation cleanup, editable tables, literal formula text, identifiers, source preservation and image-only pages.

pypdf independently inspects field trees, widget parents, appearance streams and values. MuPDF compares unrelated source content and merged/duplicated/extracted form pages. Standard ZIP/XML parsing checks Office structure and editable tables/cells. Additional developer validation opened the samples in installed Word and Excel read-only and rendered their pages for visual inspection; the shipped app does not depend on Office.

Public-corpus coverage is described in [Compatibility](Compatibility.md). It tests selected pages and preservation operations, not every field interaction or every conversion. Exact counts and release hashes are in [Test record](测试记录.md).

Implementation stays in OfficeLayout.cs / OfficeExport.cs / OfficeWindow.cs and PdfForms.cs / FormWindow.cs / FormCanvas.cs / FormRendering.cs / FormImport.cs. It uses existing PDFium, Windows Forms and system ZIP/XML APIs. No PDFium C++ rebuild, browser runtime, Office automation component or OCR model is bundled.

## 中文

“文件 > PDF 转 Word / Excel”支持页码范围与首个选中页预览。Word 输出可编辑文字、常见对齐表格及可选原图片；Excel 按位置整理单元格，每个来源页生成一个工作表。源文件不修改，独立进程可取消，临时文件自动清理。不依赖安装 Office。复杂版式、分栏、合并单元格、图表和阅读顺序可能需要整理，不保证逐像素还原。

纯扫描图在 Word 中保留为图片；Excel 不会自动识别文字。已有文字层直接提取，表单密码值显示为星号。类似公式的内容按普通文字写入，编号前导零保留。转换有页数、图片像素、单元格及进程内存限制，超限图片会报告，其他极端文件可能无法完成。

Ctrl+Shift+F 打开表单工具。先选择字段，在右侧填写或勾选并更新。设计模式可拖框创建、拖动移动、角点缩放，编辑名称、提示、当前/默认值、选项、导出值、字号、对齐、字数和标记，也可复制、删除及排序。字段类型包括文字、多行、密码、复选、单选、下拉、可编辑下拉和单选/多选列表。字号 0 为自动；选项每行写“导出值|显示文字”，单选组的每个控件需要不同导出值。

工作区支持撤销/重做、重置默认值、Unicode XFDF 导入导出，尊重只读和不导出标记。应用只提交到当前文档，之后 Ctrl+S 才写入 PDF；整次应用可一次撤销。已测试合并、插入、复制、提取的字段值、父子引用和外观保留，副本字段不互相联动。

这是常见标准 AcroForm 工具，不是完整 Acrobat Pro。动态 XFA、脚本计算/验证、动作/提交、条码及任意富文本不支持；已有签名/动作按钮只读。签名和加密文档在表单工具中只读，不会为编辑而去掉加密。证书签署仍在独立菜单。无 OCR 引擎。

上面的可复现测试覆盖实际控件、后台导出、取消、数据和原文保留；另用独立 PDF 引擎、解析器和 Office 检查结果。公开库检查仍保留引擎异常与超时，不能据此承诺所有场景没有缺陷。
