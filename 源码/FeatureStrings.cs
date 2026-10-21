using System.Collections.Generic;

namespace MiniPdf {
    internal static partial class UiText {
        static void AddFeatureTranslations(Dictionary<string,string> strings){
            // One entry per line keeps the optional feature vocabulary separate.
            const string pairs=@"选择|Select
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
无法合并页面。|Cannot combine pages.";
            foreach(string line in pairs.Split('\n')){string value=line.TrimEnd('\r');int split=-1;for(int i=0;i<value.Length;i++)if(value[i]=='|'&&(i==0||value[i-1]!='\\')){split=i;break;}if(split<0)continue;strings[value.Substring(0,split).Replace("\\|","|").Replace("\\n","\n")]=value.Substring(split+1).Replace("\\|","|").Replace("\\n","\n");}
        }
    }
}
