# Mini PDF

[English](README.md) | [简体中文](README.zh-CN.md)

轻量、原生界面的 Windows PDF 阅读与编辑工具。

面向 **Windows 10 / Windows 11 x64**，使用系统控件和 PDFium 引擎。当前 **v0.5.0 预览版**为一个约 **2.81 MiB** 的 EXE，双击即可运行，支持多种阅读视图、原文字块修改、对象编辑、批注和页面管理。

[**下载 Mini PDF.exe**](https://github.com/dmz1207/Mini-PDF/raw/refs/heads/main/Mini%20PDF.exe) · [完整源码](源码) · [验证记录](源码/测试记录.md) · [反馈问题](https://github.com/dmz1207/Mini-PDF/issues)

## v0.5 更新

- 多文档标签位于阅读工具栏上方，左侧是加大的菜单按钮。
- 按住标签文字区域左右拖动调整顺序；标签过多时，在两端停留可继续拖到隐藏位置。
- 拖动保留当前文档、页码、缩放、滚动位置、编辑工具、未保存修改和撤销记录。关闭按钮和中键关闭仍可用。
- 顶部文字和常用图标放大一档，继续使用紧凑原生控件。
- GitHub 首页默认英文，完整中文说明位于第二个语言入口。

## 特点

单文档默认显示一行紧凑主工具栏；多文档时在上方增加标签行，菜单按钮与标签同排。全部菜单收进左上角按钮（F10），编辑工具按需展开，侧栏和状态栏默认隐藏。缩小控件、页间距和外围留白，优先提供文档显示空间。

- **原生多标签**：正文只显示当前选中文档；每个标签保留页码、缩放、位置和撤销记录，再次双击 PDF 默认进入已有窗口。
- **小体积、单文件**：EXE 为 2,951,168 字节，约 2.81 MiB。资源采用 Windows 自带的 LZMS 压缩，无需额外解压库。
- **系统原生界面**：C# / Windows Forms，使用系统已有的 .NET Framework 4.x；没有 Electron、WebView 或浏览器运行时。
- **离线使用**：没有账号、广告、遥测或在线服务，不修改默认 PDF 文件关联。
- **阅读与编辑结合**：简洁阅读界面，同时提供文字块、图片、图形、批注和页面处理。
- **源码开放**：应用和启动器源码采用 MIT 许可证，完整保留 PDFium 及其依赖的许可说明。

## 界面与帮助语言

软件默认英文，菜单 > Language / 语言 中可即时切换 English 与简体中文，英文在前、中文在后。每个新独立窗口默认英文；当前窗口的选择不生成额外配置文件。切换保留文档、修改、位置和用户输入。

帮助菜单中英文说明在前、中文说明在后，两份说明均已嵌入 EXE，F1 打开英文说明。系统文件和打印对话框跟随 Windows 显示语言。

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
| 文档标签 | 原生选项卡、拖动排序、边缘滚动、关闭按钮、中键关闭、多选 / 拖入多文件、重复打开自动定位；未保存时提示 |
| 阅读视图 | 单页、双页、书籍、连续滚动、自适应多页网格 |
| 缩放 | 1%–6400% 精确百分比，支持小数；适合页面、适合宽度、Ctrl+滚轮缩放 |
| 专注阅读 | 保留标题栏和关闭按钮的阅读模式、F11 全屏、幻灯片；Esc 或右键退出 |
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
| 切换标签 / 反向切换 / 关闭当前标签 | Ctrl+Tab / Ctrl+Shift+Tab / Ctrl+W 或 Ctrl+F4 |
| 新建独立窗口 | Ctrl+Shift+N |
| 撤销 / 重做 | Ctrl+Z / Ctrl+Y |
| 查找 / 下一个结果 | Ctrl+F / Enter 或 F3 |
| 适合页面 / 适合宽度 / 实际大小 | Ctrl+1 / Ctrl+2 / Ctrl+0 |
| 自适应单页 / 自适应双页 / 书籍 / 多页网格 | Ctrl+6 / Ctrl+7 / Ctrl+8 / Ctrl+9 |
| 阅读模式 / 全屏 / 幻灯片 | Ctrl+H / F11 / Ctrl+L |
| 退出阅读模式、全屏或幻灯片 | Esc，或使用右键菜单 |
| 显示或隐藏侧边栏 | F12，兼容 F9 |
| 翻页 / 删除所选对象 | PageUp、PageDown / Delete |

缩放框可以直接输入 `87%`、`123.5%` 等数值，按 Enter 确认。多页网格中缩小页面可增加列数，双击某一页返回单页阅读。中键拖动可以平移文档。

## 多文档与页面布局

打开多个 PDF 时，点击哪个标签就只显示哪个文档。按住标签文字区域左右拖动可排序；将拖动中的标签停留在行的两端可滚动到隐藏标签。松开鼠标完成，Esc 停止拖动。只有打开两份以上文档时才显示标签行，单份文档保留紧凑界面。

`Ctrl+6` 让当前文档按单页整页自适应，`Ctrl+7` 按双页宽度自适应；工具栏提供相应按钮。双页和多页总览都是当前 PDF 内部的页面布局，不会拼接不同 PDF。

工具栏保留独立的“适宽”按钮，菜单中的“适合宽度”和 `Ctrl+2` 快捷键始终可用。

适宽时去除左右外围灰带，侧栏折叠按钮不占用整条宽度；页间距为 4 像素，Windows 原生滚动条约 10 像素并随 DPI 缩放。适宽按当前页或当前双页的实际尺寸计算，支持混合纸张与奇数末页。

手动输入的 `100%` 保持真实缩放比例；需要填满窗口时使用自适应或 `Ctrl+2` 适宽。

## 双层 PDF

支持扫描图像与 OCR 文字层叠加的 PDF：读取、选择、复制、搜索；可修改顶层隐藏 / 透明文字并保留隐藏属性和原扫描图像。图像位于文字上方时，也会优先选取对应 OCR 文字，避免误选整张扫描图。

修改识别层只改变搜索与复制得到的文字，扫描图像上的字样保持原样。组合对象内的 OCR 文字支持读取和搜索，暂不支持逐字编辑。

## 当前限制

这是持续完善中的预览版：

- 暂无 OCR。扫描页面里的文字属于图片内容，不能直接逐字修改。
- 原文字按 PDF 文字块编辑；替换字体可能改变字宽，暂不提供完整段落重排或原字体全部排版属性保留。
- 组合对象内部文字、已经转为曲线的文字不能逐字编辑。
- 暂无交互表单填写、数字签名、永久信息抹除、批注回复、测量或印章。
- 原有数字签名可能在编辑后失效；程序暂不提供签名验证。
- 高倍缩放按原分辨率分块渲染可见区域，显示尺寸与输入百分比保持准确，图像缓存目标上限为 64 MiB。
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
├─ README.md                    默认英文说明
├─ README.zh-CN.md              中文说明
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

在 Windows 10 家庭中文版 x64（10.0.19045）上，v0.5.0 已通过 **210 项程序回归、33 项独立 PDF 校验、12 项单文件启动器检查**，合计 **255 项**。

另覆盖标签左右拖动、边缘滚动、取消与鼠标捕获中断、菜单移动、较大字体和图标、标签状态、真实未保存提示、混合纸张适宽、窄滚动条、后续启动文件转交、相对路径与多参数打开。覆盖真实窗口、鼠标选择和编辑、文字对话框、右键菜单、阅读布局、精确缩放、侧栏、阅读模式恢复、密码和权限、页面处理、保存失败保护、撤销/重做、200 页文档以及打印预览。另用 ReportLab、pypdf 和 MuPDF 独立检查保存后的文字、图片、批注、页树及既有表单和目录。

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
313FDFD5CC05AAC312499E9DC947B7712F602BBAB14E6965F05EFDCC9E97884B
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
