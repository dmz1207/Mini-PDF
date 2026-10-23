using System.Collections.Generic;

namespace MiniPdf {
    internal static partial class UiText {
        static void AddFeatureTranslations(Dictionary<string,string> strings){
            // One entry per line keeps the optional feature vocabulary separate.
            const string pairs=@"此文字带有替代文本标记，暂不支持保真改写。|This text has ActualText replacement metadata. Faithful rewriting is not supported yet.
竖排文字暂不支持保真改写。|Vertical text rewriting is not yet supported with preservation guarantees.
此文字用于剪裁，暂不支持安全改写。|This text defines clipping and cannot be safely rewritten yet.
文字超出页面边界。可启用自动换行并调整宽度。|Text extends beyond the page. Enable wrapping and adjust the width.
保留原字体|Keep original font
当前文字对象|Selected text object
同一行文字|Text on the same line
相邻同样式段落|Adjacent matching paragraph
自动换行|Wrap text
宽度（毫米）|Width (mm)
行距倍数|Line spacing
预览文字区域|Preview text area
应用修改|Apply changes
原字体：|Original font: 
 个文字对象| text object(s)
仅修改识别层，扫描图像保持原样。|Edits the recognition layer; the scan image stays unchanged.
正在生成预览…|Generating preview...
正在等待预览…|Waiting for preview...
预览通过。应用后可撤销；未修改的对象保持原样。|Preview ready. Changes can be undone; other objects remain unchanged.
文字区域不在页面内。|The text area is outside the page.
无法读取原字体的字宽，请选择其他字体。|Cannot read glyph widths. Choose another font.
文字字号不支持编辑。|This text size is outside the editing range.
无法读取原字体。|Cannot read the original font.
无法读取可编辑文字。|Cannot read editable text.
此页对象较多，请按单个文字对象编辑。|This page has many objects. Edit one text object at a time.
文字块较大，请缩小编辑范围。|The text block is too large. Select a smaller scope.
文字变换不支持编辑。|This text transformation cannot be edited.
无法确认原字体包含新增字符 U+|Cannot confirm that the original font contains new character U+
。请选择自动或系统字体，并核对预览。|. Choose Auto or a system font and check the preview.
字距超出可编辑范围。|Character spacing is outside the editing range.
文本框太窄，无法容纳一个字符。|The text box is too narrow for a character.
换行过多，请增大文本框宽度。|Too many lines. Increase the text box width.
文字已变化，请重新选择。|The text changed. Select it again.
请输入有效文字和字号。|Enter valid text and font size.
请输入有效文本框宽度。|Enter a valid text box width.
请输入有效行距。|Enter valid line spacing.
此文字需要暂未支持的复杂排版，请保留原文或使用其他编辑器。|This text needs unsupported complex shaping. Keep the original or use another editor.
此文字带有隐藏、图层或剪裁属性，请保留原字体并按单个对象编辑。|This text has hidden, layer or clipping state. Keep its original font and edit one object.
无法保留文字间距。|Cannot preserve character spacing.
原字体无法正确表示输入文字，请选择“自动”或其他系统字体后重试。|The original font cannot represent this text correctly. Choose Auto or a system font.
全部文件逐页交错|Interleave all files, one page each
全部文件每 N 页交错…|Interleave all files in blocks of N…
按文件名自然排序|Sort files by name (natural order)
各文件内部倒序|Reverse pages within each file
交错：全部 {0} 个文件|Interleaved: all {0} files
每份连续取几页？例如 2：A1、A2、B1、B2、C1、C2…|Pages per file in each round? For 2: A1, A2, B1, B2, C1, C2…
合并页面缩略图|Merge page thumbnails
页面已清空，放弃调整并关闭吗？|No pages remain. Discard the changes and close?
应用当前页面调整吗？\n是：应用；否：放弃；取消：继续编辑。|Apply the current page changes?\nYes: apply; No: discard; Cancel: keep editing.
将当前排列保存为 PDF 吗？\n是：保存；否：不保存；取消：继续编辑。|Save the current arrangement as a PDF?\nYes: save; No: discard; Cancel: keep editing.
保存合并的 PDF|Save combined PDF
合并文档.pdf|Combined.pdf
原第 {0} 页|Original page {0}
页面 {0}|Page {0}
所在文件组：{0}|File group: {0}
移动文件到 {0} 之后|Move file after {0}
移动文件到 {0} 之前|Move file before {0}
在 {0} 之前|before {0}
插入到 {0}：{1}|Insert into {0}: {1}
末尾|end
停留可展开|hover to expand
拖动文件名可移动整个文件；＋/－展开或收起。|Drag the file title to move the whole file; +/- expands or collapses it.
排列参数无效。|Invalid ordering parameters.
请先添加两份非空文档。|Add two non-empty documents first.
交错：{0} + {1}|Interleaved: {0} + {1}
自动排列 ▾|Arrange ▾
自动排列|Arrange pages
按来源和原页码归组|Group by source and original page number
逐页交错：A 在前|Alternate pages: A first
逐页交错：B 在前|Alternate pages: B first
第二份倒序后交错|Alternate with second file reversed
每 N 页交错…|Alternate every N pages...
每 N 页交错|Alternate every N pages
每份连续取几页？例如 2：A1、A2、B1、B2…|Pages per block? For example 2: A1, A2, B1, B2...
请输入 1–10000 的整数。|Enter an integer from 1 to 10,000.
签名方式|Signature mode
姓名（输入后立即预览）|Name (live preview)
页面上的宽度 (mm)|Page width (mm)
高 {0:0.#} mm · 保持比例|Height {0:0.#} mm · aspect ratio locked
单击页面放置，或拖框设定大小；放置后拖动角点缩放。\n可见签名是图片；数字签名请使用证书菜单。|Click the page to place, or drag to size; drag the corner to resize afterwards.\nA visible signature is an image; use the certificate command for digital signing.
请输入姓名。|Enter your name.
请导入含有签名的图片。|Import an image containing a signature.
请在预览区域绘制，或直接输入姓名。|Draw in the preview area, or type your name above.
单击放置签名，或拖框设定大小；Esc 取消。|Click to place the signature, or drag to size; Esc cancels.
签名已放置：拖动移动，拖动右下角缩放；右键可输入缩放比例。|Signature placed: drag to move, drag the bottom-right corner to resize, or right-click for a percentage.
移除水印…|Remove watermarks...
移除水印|Remove watermarks
图章批注|Stamp annotation
组合对象|Object group
图形|Graphic
请先勾选要移除的对象。|Check the objects to remove first.
显示全部页面对象|Show all page objects
预览移除后|Preview after removal
预览页码|Preview page
筛选文字|Filter text
勾选要移除的独立文字、图片或图章。\n组合对象会整体移除，请核对预览。\n扫描图像像素内的水印无法作为对象删除。|Check independent text, images or stamps to remove.\nGroups are removed as a whole; inspect the preview.\nWatermarks baked into scan pixels cannot be deleted as objects.
移除勾选对象|Remove checked objects
匹配页码范围|Match page range
匹配相同水印|Match same watermarks
选择 Mini PDF 水印|Select Mini PDF watermarks
清空选择|Clear selection
已选 {0} 个对象 / {1} 页|Selected {0} objects on {1} pages
选择|Select
选择文字 (V)；Esc 返回选择模式|Select text (V); Esc returns to selection mode
管理页面 (Ctrl+Shift+P)|Organize pages (Ctrl+Shift+P)
作为图像打印（兼容模式）|Print as image (compatibility mode)
无法访问打印设备。|Cannot access the printing device.
无法初始化原生打印。|Cannot initialize native printing.
无法准备打印页面。|Cannot prepare the page for printing.
管理页面|Organize pages
管理页面…|Organize pages...
应用页面调整|Apply page changes
复制页面|Duplicate pages
请至少保留一页。|Keep at least one page.
请至少保留一页，最多支持 10000 页。|Keep between 1 and 10,000 pages.
无法插入页面。|Cannot insert pages.
页面顺序无效。|Invalid page order.
高亮所选文字|Highlight selected text
选择文字后高亮；无文字选择时框选高亮|Highlight selected text; drag an area when no text is selected
黄色|Yellow
浅黄色|Light yellow
绿色|Green
薄荷绿|Mint
天蓝色|Sky blue
蓝色|Blue
紫色|Purple
粉色|Pink
珊瑚色|Coral
橙色|Orange
棕色|Brown
灰色|Gray
自定义高亮颜色…|Custom highlight color...
抓手|Hand
抓手 / 拖动页面|Hand / pan page
左键拖动页面 (H)；按住空格临时平移|Drag with left mouse (H); hold Space to pan temporarily
按住左键拖动页面 · 滚轮滚动 · Ctrl+滚轮缩放|Drag with left mouse to pan; Ctrl+wheel to zoom
打印|Print
打印机|Printer
打印机属性…|Printer properties...
页面设置…|Page setup...
份数|Copies
逐份打印|Collate
所有页面|All pages
当前页面|Current page
页码范围|Page range
奇数和偶数页|Odd and even pages
仅奇数页|Odd pages only
仅偶数页|Even pages only
逆序打印|Reverse order
大小|Size
多页合一|Multiple
小册子|Booklet
海报拼接|Poster
页面处理|Page handling
适合可打印区域|Fit printable area
实际大小|Actual size
缩小过大的页面|Shrink oversized pages
自定义比例|Custom scale
每张纸的页数|Pages per sheet
绘制页面边框|Print page borders
正反两面|Both sides
仅正面|Front sides only
仅背面|Back sides only
从右侧装订|Right binding
小册子通常使用横向、短边翻转；手动双面请按打印机进纸方向翻纸。|Booklets normally use landscape and short-edge duplex. For manual duplex, reload sheets as required by your printer.
重叠 (mm)|Overlap (mm)
裁切标记|Cut marks
自动纵向 / 横向|Auto portrait / landscape
纵向|Portrait
横向|Landscape
方向|Orientation
纸张|Paper
按照 PDF 页面大小选择纸张|Choose paper by PDF page size
附加页边距 (mm)|Extra margins (mm)
使用打印机默认双面设置|Use printer duplex defaults
单面|Single-sided
双面：长边翻转|Duplex: long-edge binding
双面：短边翻转|Duplex: short-edge binding
以灰度（黑白）打印|Print in grayscale
包含批注|Include annotations
没有可用的打印机。仍可查看排版预览。|No printer is available. Layout preview is still available.
纸面 {0} / {1} · 页码 {2}\n{3:0.#} × {4:0.#} mm|Side {0} / {1} · Pages {2}\n{3:0.#} × {4:0.#} mm
所选范围没有页面。|The selected range contains no pages.
页边距超出可打印区域。|Margins exceed the printable area.
打印设置无效。|Invalid print settings.
重叠宽度超出可打印区域。|Overlap exceeds the printable area.
打印任务超过 10000 张，请减小范围或比例。|This job exceeds 10,000 sheets. Reduce its range or scale.
所选打印机不支持自动双面打印。|The selected printer does not support automatic duplex.
无法打开打印机属性。|Cannot open printer properties.
打印未完成|Printing was not completed
签名与水印(&S)|&Signatures and watermarks
放置可见签名…|Place visible signature...
放置可见签名|Place visible signature
绘制签名|Draw signature
输入签名|Type signature
图片签名|Image signature
导入图片…|Import image...
清除|Clear
移除图片白底|Remove white image background
可见签名是页面图像；证书数字签名请使用证书菜单。|For a digital signature, use the certificate command.
放置签名|Place signature
请先绘制、输入或导入签名。|Draw, type or import a signature first.
添加水印…|Add watermark...
移除 Mini PDF 水印…|Remove Mini PDF watermarks...
移除 Mini PDF 水印|Remove Mini PDF watermarks
水印|Watermark
图片水印…|Image watermark...
使用文字|Use text
旋转角度|Rotation
不透明度 %|Opacity %
颜色…|Color...
图片宽度 %|Image width %
居中|Center
顶部|Top
底部|Bottom
平铺|Tile
置于页面内容下方|Behind page contents
替换现有 Mini PDF 水印|Replace existing Mini PDF watermarks
文字支持 {page} 和 {pages} 页码占位符。\n预览显示当前页面；应用后可撤销。|Text supports {page} and {pages} placeholders.\nPreview shows the current page. Applying can be undone.
应用|Apply
当前页面不在水印范围内。|The current page is outside the watermark range.
水印图片请缩小至 1200 万像素以内。|Reduce the watermark image to 12 megapixels or less.
无法创建页面预览。|Cannot create page preview.
请选择图片。|Choose an image.
无法标记水印对象。|Cannot tag watermark objects.
无法删除水印。|Cannot remove watermark.
请输入水印文字或选择图片。|Enter watermark text or choose an image.
水印设置无效。|Invalid watermark settings.
此文档不能编辑。|This document cannot be edited.
证书数字签名…|Digitally sign with certificate...
证书数字签名|Digitally sign with certificate
签名和证书…|Signatures and certificates...
签名和证书|Signatures and certificates
查看证书文件…|View certificate file...
载入 PFX / P12…|Load PFX / P12...
查看证书…|View certificate...
签署原因|Reason
地点|Location
使用 SHA-256 / CMS 签署并另存为副本。\n可先放置可见签名；证书签名本身不增加页面图案。\n不包含可信时间戳、长期验证或云端签名服务。|Sign with SHA-256 / CMS and save a separate copy.\nYou may place a visible signature first; certificate signing adds no page artwork.\nTrusted timestamps, long-term validation and cloud signing are not included.
签署并另存为…|Sign and save copy...
未找到含私钥的个人证书，可载入 PFX / P12 文件。|No personal certificate with a private key was found. Load a PFX / P12 file.
证书密码|Certificate password
输入 PFX / P12 密码（允许为空）：|Enter the PFX / P12 password (may be empty):
证书没有可用的私钥。|The certificate has no usable private key.
证书已过期或尚未生效。|The certificate has expired or is not valid yet.
该证书不允许数字签名。|The certificate does not permit digital signatures.
已有签名的文档以只读方式打开，暂不支持追加签名。|Signed documents open read-only. Adding further signatures is not supported yet.
请先将加密 PDF 提取为未加密副本，再进行证书签名。|Extract an unencrypted copy of this PDF before certificate signing.
证书签名目前支持 128 MiB 以内的 PDF。|Certificate signing currently supports PDFs up to 128 MiB.
XFA 表单暂不支持证书签名。|Certificate signing of XFA forms is not supported yet.
PDF 表单结构不支持签名。|This PDF form structure cannot be signed.
证书签名数据过大。|The certificate signature data is too large.
签名文件校验失败。|The signed file failed validation.
签名|Signature
签名验证目前支持 128 MiB 以内的 PDF。|Signature verification currently supports PDFs up to 128 MiB.
签名数据无效。|Invalid signature data.
签名格式无效。|Invalid signature format.
暂不支持验证这种签名格式。|Verification of this signature format is not supported yet.
签名字节范围无效。|Invalid signature byte range.
签名排除范围不安全。|The signature exclusion range is unsafe.
签名填充数据无效。|Invalid signature padding.
签名证书缺失。|The signing certificate is missing.
签名字节校验通过，覆盖当前文件。|Signature byte integrity verified; it covers the current file.
原签署版本的字节校验通过；文件含后续修改，未验证这些修改。|Original signed revision verified. Later changes exist and have not been validated.
Windows 本地证书链受信任。|The certificate chain is trusted locally by Windows.
Windows 本地证书链不受信任或不完整。|The local Windows certificate chain is untrusted or incomplete.
离线校验：未检查证书吊销、可信时间戳及文档修改权限。|Offline check: revocation, trusted timestamps and document modification permissions were not checked.
未通过验证：|Verification did not pass: 
PDF 结构不支持安全签名，请重新导出 PDF 后再试。|This PDF structure cannot be signed safely. Re-export the PDF and try again.
请使用其他文件名保存签名副本。|Choose a different filename for the signed copy.
证书签名未完成|Certificate signing was not completed
此文档没有证书数字签名。可见手写或图片签名不属于证书签名。|This document has no certificate digital signatures. Visible handwriting and signature images are not certificate signatures.
无法打开证书|Cannot open certificate
已签名的 PDF 以只读方式打开，以保留签名字节。|Signed PDFs open read-only to preserve their signed bytes.
合并文件|Combine files
添加文件…|Add files...
展开全部|Expand all
收起全部|Collapse all
旋转|Rotate
移除|Remove
合并为新文档|Combine into new document
{0} 个文件组 · {1} 页|{0} file groups · {1} pages
当前文档|Current document
PDF 和图片 (*.pdf;*.png;*.jpg;*.jpeg;*.bmp)\|*.pdf;*.png;*.jpg;*.jpeg;*.bmp|PDF and images (*.pdf;*.png;*.jpg;*.jpeg;*.bmp)\|*.pdf;*.png;*.jpg;*.jpeg;*.bmp
仅支持 PDF、PNG、JPEG 和 BMP 文件。|Only PDF, PNG, JPEG and BMP files are supported.
添加文件未完成|Adding a file was not completed
合并需要未签名且允许编辑的 PDF。|Combining requires unsigned PDFs that permit editing.
合并最多支持 10000 页。|Combining supports up to 10,000 pages.
请至少添加一页。|Add at least one page.
将 PDF 或图片拖到这里\n展开文件组后，可拖动页面调整顺序|Drop PDFs or images here\nExpand a file group to drag and reorder its pages
{0} 页|{0} pages
此页字体保存后无法保持文字完整，已取消修改；可尝试明确更换字体。|This page loses text after font serialization. The edit was canceled; try explicitly replacing the font.
PDF 转 Word|PDF to Word
PDF 转 Excel|PDF to Excel
PDF 转 Word…|Convert PDF to Word…
PDF 转 Excel…|Convert PDF to Excel…
表单工具|Form tools
表单工具…|Form tools…
页面范围|Page range
自动检测列|Detect columns
包含图片|Include images
预览第一页|Preview first page
按位置整理为可编辑单元格，每个 PDF 页一个工作表；复杂表格请核对。无文字层的扫描页不识别。|Extract positioned text into editable cells, one sheet per PDF page. Review complex tables. Scans without a text layer are not recognized.
导出可编辑文字和图片，采用流式排版；复杂版式可能变化。扫描页不会自动识别文字。|Export editable text and images with flowing layout. Complex layouts may change. Scans are not recognized automatically.
此页没有可提取文字；未启用 OCR。|No extractable text on this page. OCR is not included.
导出…|Export…
转换文档|Convert document
正在转换…|Converting…
转换进程启动超时。|The conversion worker did not start in time.
部分图片超过导出限制或无法读取：|Some images exceeded export limits or could not be read: 
转换未完成|Conversion incomplete
此页文字过多，无法转换。|This page has too much text to convert.
检测到过多列，请使用单列文本模式。|Too many columns were detected. Use single-column text mode.
单个单元格超过 Excel 的文字上限。|A cell exceeds Excel's text limit.
请选择 1 至 2000 页进行转换。|Select between 1 and 2,000 pages to convert.
页面范围无效。|Invalid page range.
转换内容过大，请分批导出。|The conversion is too large. Export fewer pages at a time.
导出文件校验失败。|Exported file validation failed.
文本框|Text field
多行文本|Multiline text
密码框|Password field
勾选框|Checkbox
单选组|Radio group
下拉框|Dropdown
可编辑下拉框|Editable dropdown
列表框|List box
多选列表|Multiple selection
签名字段|Signature field
按钮|Button
不支持的字段|Unsupported field
设计字段|Design fields
选择 / 填写|Select / fill
导入数据…|Import data…
导出数据…|Export data…
重置填写值|Reset values
字段名称|Field name
提示说明|Tooltip
填写值|Value
已勾选|Checked
默认值|Default value
当前单选项 / 勾选导出值|Selected option / checkbox export value
选项（每行 导出值\|显示文字）|Options (one export value\|label per line)
字数|Max length
左对齐|Align left
右对齐|Align right
只读|Read only
必填|Required
不导出数据|Exclude from export
更新字段|Update field
复制字段 / 新增单选项|Duplicate / add radio option
删除所选字段|Delete selected field
字段顺序上移|Move field up
字段顺序下移|Move field down
应用到文档|Apply to document
应用尚未提交的字段修改？|Apply pending field changes?
只读查看；签名、权限或加密限制了表单修改。|Read only: signature, permissions or encryption restrict form editing.
 个字段，未填必填项：| fields; required values missing: 
字号 0 表示自动；设计模式下可拖动和缩放。|Font size 0 means automatic. Use Design fields to move and resize.
将可填写字段恢复为默认值？|Restore writable fields to their defaults?
已导入字段：|Imported fields: 
关闭前将表单修改应用到当前文档？之后可用 Ctrl+S 保存。|Apply form changes to the current document before closing? Save the PDF afterward with Ctrl+S.
表单未打开|Cannot open form tools
表单页面树无效。|Invalid form page tree.
兼容预览不能处理表单。|Form tools are unavailable in compatibility preview.
XFA 动态表单暂不支持；请使用标准 PDF 表单。|Dynamic XFA forms are not supported. Use a standard PDF AcroForm.
表单字段树无效或过大。|The form field tree is invalid or too large.
直接内联字段暂不支持编辑。|Direct inline field dictionaries cannot be edited.
最多支持 10000 个表单字段。|At most 10,000 form fields are supported.
字段名称不能为空或重复，层级名称用点分隔。|Field names must be nonempty and unique. Use dots for hierarchical names.
字段文字过长。|Field text is too long.
字段文字超过最大长度：|Field exceeds its maximum length: 
签名、动作按钮及未知字段在这里仅供查看。|Signature fields, action buttons and unknown fields are read only here.
选项导出值必须唯一。|Option export values must be unique.
填写值不在字段选项内：|The value is not among the field options: 
单选组的选项值必须唯一。|Radio options must have distinct export values.
字段位置或大小无效。|Invalid field position or size.
字段名称不能同时作为其他字段的父组。|A terminal field name cannot also be another field's parent group.
无法生成表单外观。|Cannot generate the field appearance.
表单外观页数校验失败。|Field appearance page validation failed.
字段引用已变化，请重新打开表单工具。|Field references changed. Reopen form tools.
表单保存校验失败。|Saved form validation failed.
请选择 XFDF 表单数据文件。|Choose an XFDF form data file.
编辑表单结构前，请明确导出未加密副本。|Export an unencrypted copy explicitly before changing form structure.
此字段只接受一个填写值。|This field accepts only one value.
勾选或单选的选项值不能是 Off 或空值。|Checkbox and radio export values must be nonempty and cannot be Off.
无法合并页面。|Cannot combine pages.
预览页面|Preview page
在文档中编辑|Edit in document
原文编辑|Edit text
在独立窗口编辑所选文字|Edit selected text in a separate window
所选全部文字|All selected text
修改所选全部文字…|Edit all selected text…
删除所选全部文字|Delete all selected text
缩放所选全部文字…|Scale all selected text…
在文档中编辑文字（F2）|Edit text in document (F2)
应用 Ctrl+Enter|Apply Ctrl+Enter
取消 Esc|Cancel Esc
窗口编辑…|Edit in window…
应用文档内尚未确认的文字修改？|Apply the unconfirmed in-document text changes?
请先选择文字。|Select text first.
选区包含嵌套或无法映射的文字，暂不支持安全改写。|The selection includes nested or unmappable text that cannot be edited safely.
选区包含隐藏、剪裁、标记或非横排文字，请按单个对象编辑。|The selection includes hidden, clipped, marked or non-horizontal text. Edit individual objects instead.
文字映射不连续，请缩小选区。|The text mapping is discontinuous. Select a smaller range.
选区文字编码无法精确映射，请保留原文。|The selected text encoding cannot be mapped precisely. Keep the original.
请保留跨页选区的页面分隔标记。|Keep the page separator markers in a multi-page selection.
保留版式（推荐）|Preserve layout (recommended)
连续正文（便于编辑）|Continuous text (easy to edit)
简单公式可编辑（离线）|Editable simple equations (offline)
公式设置…|Equation settings…
公式设置|Equation settings
保留页面位置，普通正文可编辑，公式和图形保留高清外观。下方显示原文参考预览；扫描页不识别文字。|Keep page positions with editable body text and high-resolution equations and artwork. Preview shows the source PDF. No OCR for scans.
参数越宽松，越可能误判。复杂或不明确的公式始终保留原貌。|Looser thresholds can cause mistakes. Complex or ambiguous equations retain their original appearance.
脚标最大字号比例|Maximum script-to-body size ratio
最小基线偏移比例|Minimum baseline offset ratio
最大符号间距比例|Maximum symbol gap ratio
分式横线最大厚度（磅）|Maximum fraction bar thickness (pt)
恢复默认|Restore defaults
无法生成预览。|Cannot generate the preview.
此文件处于兼容预览，无法转换 Office 文件。|Office conversion is unavailable in compatibility preview.
使用普通 Word 段落合并正文，可连续编辑全文。可选离线转换简单公式；公式和图片位置可能变化。扫描页不识别文字。|Merge body text into normal Word paragraphs for continuous editing. Optionally infer simple equations offline. Equations and images may move. No OCR for scans.
";
            foreach(string line in pairs.Split('\n')){string value=line.TrimEnd('\r');int split=-1;for(int i=0;i<value.Length;i++)if(value[i]=='|'&&(i==0||value[i-1]!='\\')){split=i;break;}if(split<0)continue;strings[value.Substring(0,split).Replace("\\|","|").Replace("\\n","\n")]=value.Substring(split+1).Replace("\\|","|").Replace("\\n","\n");}
        }
    }
}
