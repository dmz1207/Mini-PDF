# Mini PDF

轻量、原生界面的 Windows PDF 阅读与编辑工具。

面向 **Windows 10 / Windows 11 x64**，使用系统控件和 PDFium 引擎。当前 **v0.2.0 预览版**为一个约 **2.79 MiB** 的 EXE，双击即可运行，支持多种阅读视图、原文字块修改、对象编辑、批注和页面管理。

[**下载 Mini PDF.exe**](https://github.com/dmz1207/Mini-PDF/raw/refs/heads/main/Mini%20PDF.exe) · [完整源码](源码) · [验证记录](源码/测试记录.md) · [反馈问题](https://github.com/dmz1207/Mini-PDF/issues)

## 特点

- **小体积、单文件**：EXE 为 2,929,664 字节，约 2.79 MiB。资源采用 Windows 自带的 LZMS 压缩，无需额外解压库。
- **系统原生界面**：C# / Windows Forms，使用系统已有的 .NET Framework 4.x；没有 Electron、WebView 或浏览器运行时。
- **离线使用**：没有账号、广告、遥测或在线服务，不修改默认 PDF 文件关联。
- **阅读与编辑结合**：简洁阅读界面，同时提供文字块、图片、图形、批注和页面处理。
- **源码开放**：应用和启动器源码采用 MIT 许可证，完整保留 PDFium 及其依赖的许可说明。

## 下载和使用

1. 下载上面的 `Mini PDF.exe`，保存到可写入的普通文件夹。
2. 双击启动，点击“打开”，或者把 PDF 拖入窗口。
3. 点击“对象”，选择页面文字、图片或图形；双击文字块可修改内容。
4. 按 `Ctrl+S` 保存，或按 `Ctrl+Shift+S` 另存为。

适用于 **64 位 Windows 10 / 11**。使用 Windows 自带的 .NET Framework 4.x，无需安装 Node、Python、Electron 或现代 .NET SDK。当前已在 Windows 10 x64 实测，Windows 11 尚未独立实机验证。

启动时，引擎、使用说明和许可文件临时释放到 EXE 旁的 `.MiniPDF-runtime-*` 目录，程序 TEMP/TMP 同样指向这里。正常退出后自动清理，异常结束后的遗留目录在下次启动时清理。并行实例互不干扰，运行目录内额外创建的用户文件会保留。

## 功能

| 类别 | 已实现功能 |
| --- | --- |
| 阅读视图 | 单页、双页、书籍、连续滚动、自适应多页网格 |
| 缩放 | 4%–400% 精确百分比，支持小数；适合页面、适合宽度、Ctrl+滚轮缩放 |
| 专注阅读 | 隐藏菜单和工具栏的阅读模式、全屏、幻灯片；Esc 或右键退出 |
| 导航 | 缩略图、目录、当前页批注、搜索、文字选择和复制、PDF 内部及网页链接 |
| 侧边栏 | 工具栏按钮、页面左侧折叠箭头、F12 开关；收起后可一键重新打开 |
| 文字和对象 | 修改原文字块，添加中英文及多行文字；移动、缩放、删除文字、图片和图形；插入图片 |
| 批注 | 高亮、下划线、删除线、手写、便签；便签内容编辑、批注删除；矩形绘制 |
| 页面处理 | 旋转、插入空白页、复制、删除、移动、提取、合并 |
| 输出 | 保存、另存为、当前页 PNG、全文 TXT、当前页及页码范围打印 |
| 历史和保存 | 撤销/重做、保存数据重开校验、原子替换、中文字体子集化 |
| 密码文档 | 正确密码打开，遵循编辑及打印权限；保存保留加密 |

原文字修改会移除原文字对象并写入替换内容。高亮、下划线、删除线、手写和便签保存为标准 PDF 批注；新增文字、图片和矩形保存为页面内容。

## 常用操作

| 操作 | 快捷键 |
| --- | --- |
| 打开 / 保存 / 另存为 | Ctrl+O / Ctrl+S / Ctrl+Shift+S |
| 撤销 / 重做 | Ctrl+Z / Ctrl+Y |
| 查找 / 下一个结果 | Ctrl+F / Enter 或 F3 |
| 适合页面 / 适合宽度 / 实际大小 | Ctrl+1 / Ctrl+2 / Ctrl+0 |
| 单页 / 双页 / 书籍 / 多页网格 | Ctrl+6 / Ctrl+7 / Ctrl+8 / Ctrl+9 |
| 阅读模式 / 全屏 / 幻灯片 | Ctrl+H / F11 / Ctrl+L |
| 退出阅读模式、全屏或幻灯片 | Esc，或使用右键菜单 |
| 显示或隐藏侧边栏 | F12，兼容 F9 |
| 翻页 / 删除所选对象 | PageUp、PageDown / Delete |

缩放框可以直接输入 `87%`、`123.5%` 等数值，按 Enter 确认。多页网格中缩小页面可增加列数，双击某一页返回单页阅读。中键拖动可以平移文档。

## 当前限制

这是持续完善中的预览版：

- 暂无 OCR。扫描页面里的文字属于图片内容，不能直接逐字修改。
- 原文字按 PDF 文字块编辑；替换字体可能改变字宽，暂不提供完整段落重排或原字体全部排版属性保留。
- 组合对象内部文字、已经转为曲线的文字不能逐字编辑。
- 暂无交互表单填写、数字签名、永久信息抹除、批注回复、测量、印章或多文档标签。
- 原有数字签名可能在编辑后失效；程序暂不提供签名验证。
- 极大页面高倍缩放时会限制内部渲染分辨率，显示尺寸和输入百分比仍保持准确。
- 撤销最多 12 步，历史目标上限 64 MiB；至少保留最近一步，大文档可能超过该目标。

## 从源码构建

下载或克隆整个仓库，在仓库根目录使用 Windows PowerShell 运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\源码\build.ps1
```

无需安装开发 SDK。脚本使用 Windows 自带的 C# 编译器，首次构建下载固定版本的 PDFium 并校验 SHA-256。下载、中间文件和测试产物保存在仓库下的 `work/`，最终生成根目录的 `Mini PDF.exe`。

应用采用优化编译，不附带调试符号。压缩工具和回归程序单独构建，不嵌入正式 EXE；源码保留可读性。

```text
Mini-PDF/
├─ Mini PDF.exe                 便携版程序
├─ README.md                    项目介绍
├─ LICENSE                      应用源码 MIT 许可证
├─ CHANGELOG.md                 版本记录
└─ 源码/
   ├─ *.cs                     应用、启动器及测试源码
   ├─ build.ps1                构建和程序回归测试
   ├─ test-launcher.ps1         单文件启动与清理检查
   ├─ verify_pdf.py             独立 PDF 校验
   ├─ 使用说明.txt
   ├─ 测试记录.md
   └─ third-party-licenses/     PDFium 及依赖许可
```

## 验证情况

在 Windows 10 家庭中文版 x64（10.0.19045）上，v0.2.0 已通过 **99 项程序回归、22 项独立 PDF 校验、6 项单文件启动器检查**，合计 **127 项**。

覆盖真实窗口、鼠标选择和编辑、文字对话框、右键菜单、阅读布局、精确缩放、侧栏、阅读模式恢复、密码和权限、页面处理、保存失败保护、撤销/重做、200 页文档以及打印预览。另用 ReportLab、pypdf 和 MuPDF 独立检查保存后的文字、图片、批注、页树及既有表单和目录。

这些结果代表已覆盖流程通过，不构成所有 PDF、打印机及系统环境均无缺陷的保证。**Windows 11 尚未实机验证。**详细记录见 [测试记录](源码/测试记录.md)。

运行程序回归：

```powershell
powershell -ExecutionPolicy Bypass -File .\源码\build.ps1 -Test
```

完整互操作检查需要开发环境中的 `pypdf`、`reportlab`、`pymupdf` 及支持 AES 的 `cryptography`。它们仅用于测试，不随软件分发：

```powershell
python .\源码\verify_pdf.py fixtures .\work\tests
powershell -ExecutionPolicy Bypass -File .\源码\build.ps1 -Test
python .\源码\verify_pdf.py verify .\work\tests
powershell -ExecutionPolicy Bypass -File .\源码\test-launcher.ps1
```

当前 EXE 的 SHA-256：

```text
1F4AFD4632A89F1F01C91F2F7CB4F828B730DAF12631BF8C88DFAB125CE09CBD
```

## 技术与来源

- 界面：C# 5 / Windows Forms / 系统 .NET Framework 4.x。
- PDF 引擎：[PDFium](https://pdfium.googlesource.com/pdfium/)，采用 [158.0.8111.0 x64 固定构建](https://github.com/bblanchon/pdfium-binaries/releases/tag/chromium%2F8111)，不含 V8 和 XFA。
- 压缩：[Windows Compression API](https://learn.microsoft.com/en-us/windows/win32/cmpapi/using-the-compression-api)，LZMS，8 MiB 分块。
- 阅读行为参考 [SumatraPDF 视图文档](https://www.sumatrapdfreader.org/docs/Viewing-modes)、[DisplayModel](https://github.com/sumatrapdfreader/sumatrapdf/blob/master/src/shared/DisplayModel.cpp)、[菜单](https://github.com/sumatrapdfreader/sumatrapdf/blob/master/src/Menu.cpp)和[选择](https://github.com/sumatrapdfreader/sumatrapdf/blob/master/src/shared/Selection.cpp)等成熟实现。
- 功能参考 [PDF Looker Pro](https://github.com/dongfangxy/pdf-looker-pro)。

应用代码独立实现，未移植 SumatraPDF 的 GPL 应用代码或 PDF Looker Pro 的应用代码。

## 开源许可与反馈

应用和启动器源码采用 [MIT 许可证](LICENSE)。PDFium 及第三方依赖遵循 [各自的许可说明](源码/third-party-licenses)。许可说明已嵌入 EXE，在“帮助 → 开源许可”中可查看，无需激活。

反馈时请提供 Windows 版本、Mini PDF 版本、复现步骤和实际结果。如果需要示例 PDF，请使用可公开的文件。
