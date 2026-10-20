# Mini PDF

面向 Windows 10 / Windows 11 **x64** 的极简 PDF 阅读与编辑工具。
系统 Windows Forms 控件 + Windows 已有的 .NET Framework 4.x + PDFium。
不使用 Electron、WebView 或浏览器。发行版为一个 EXE，运行时临时释放引擎，退出清理。
资源采用 Windows 自带的 LZMS 压缩，8 MiB 分块；无需另带解压库。

## 功能

- 单页、双页、书籍、连续滚动、多页网格；网格随缩放自动调整列数。
- 工具栏精确百分比缩放，4%–400%，支持小数；Ctrl+滚轮缩放。
- 阅读模式、全屏、幻灯片；Esc 退出，恢复进入前的界面。
- 工具栏“侧栏”、文档左侧折叠箭头、F12（兼容 F9）切换侧边栏；收起后仍可一键打开。
- 文字选择、复制、搜索、目录、缩略图、当前页批注列表和标准链接。
- 修改原文字对象，添加中英文文字，移动、缩放、删除原有文字/图片/图形。
- 高亮、下划线、删除线、手写、矩形、便签；可编辑便签、删除批注。
- 页面旋转、插入、复制、删除、移动、提取、合并，导出 PNG / TXT，按范围打印。
- 撤销/重做、保存前重新读取验证、同目录临时文件原子替换、中文字体子集化。
- 正确密码打开加密 PDF；遵循修改和打印权限。保存保留加密。
- 按光标位置生成原生右键菜单，阅读模式仍可使用。

## 使用

双击根目录的 `Mini PDF.exe`。不需要安装环境，也不需要激活。
请将便携版放在可以写入的目录。本项目默认放在 `F:\软件diy` 内。
许可证和完整使用说明已嵌入 EXE，可在“帮助”菜单查看。

程序旁会短暂出现 `.MiniPDF-runtime-*` 目录。TEMP/TMP 也指向该目录，正常退出自动删除。
异常结束后，下次启动会清理遗留目录；不会删除另一运行实例正在使用的目录。
如果用户在运行目录内另建了文档，清理会保留这些文件。

## 构建

在 Windows PowerShell 运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\源码\build.ps1
```

不需要安装 SDK：使用 Windows 自带的 C# 编译器。
第一次构建会在 `work/` 下载 PDFium 158.0.8111.0 x64（无 V8、无 XFA），校验固定 SHA-256。
所有中间文件在 `work/`，最终在根目录生成 `Mini PDF.exe`。
编译开启优化，关闭调试符号；测试程序与压缩工具单独构建，不嵌入正式 EXE。
源码保留可读性；删除空格不会缩小编译后的代码。

## 回归测试

独立测试程序（引用正式应用程序集）：

```powershell
powershell -ExecutionPolicy Bypass -File .\源码\build.ps1 -Test
```

包含真实 Windows 窗口、鼠标消息、真实文字对话框、右键菜单开关、布局、缩放、阅读模式、PDF 编辑和异常路径。
打印通过 PreviewPrintController 验证，不发送实体打印任务。

完整独立互操作测试另需开发用 Python 包 `pypdf reportlab pymupdf`，这些不随软件分发，也不影响软件使用：

```powershell
python .\源码\verify_pdf.py fixtures .\work\tests
powershell -ExecutionPolicy Bypass -File .\源码\build.ps1 -Test
python .\源码\verify_pdf.py verify .\work\tests
powershell -ExecutionPolicy Bypass -File .\源码\test-launcher.ps1
```

## 支持边界

当前为单文档应用。扫描图片中的文字需要 OCR，目前不提供。
原文字按 PDF 文字块编辑；替换字体可能改变字宽，不是 Word 式段落重排。
组合对象内部文字和已转为曲线的文字不能逐字修改。
暂不提供交互表单填写、数字签名、永久信息抹除、批注回复、测量、多文档标签。
对极大页面的高倍缩放会限制内部渲染图像分辨率，页面显示尺寸和输入百分比仍保持准确。
实测环境是当前 Windows 10 x64；Windows 11 使用相同系统 API，尚无独立 Windows 11 实机测试。

## 来源与许可

应用和启动器源码：MIT，见 `LICENSE`。
引擎和第三方组件：见 `third-party-licenses/`，发行版内置这些说明。
压缩接口参见 [Microsoft Compression API](https://learn.microsoft.com/en-us/windows/win32/cmpapi/using-the-compression-api)。

- 直接使用 [PDFium](https://pdfium.googlesource.com/pdfium/)，[固定二进制版本](https://github.com/bblanchon/pdfium-binaries/releases/tag/chromium%2F8111)。
- 参考 [Sumatra 的视图文档](https://www.sumatrapdfreader.org/docs/Viewing-modes)、[DisplayModel](https://github.com/sumatrapdfreader/sumatrapdf/blob/master/src/shared/DisplayModel.cpp)、[菜单](https://github.com/sumatrapdfreader/sumatrapdf/blob/master/src/Menu.cpp)、[选择](https://github.com/sumatrapdfreader/sumatrapdf/blob/master/src/shared/Selection.cpp)及 Canvas 的行为和边界；应用代码独立实现，未移植 GPL 代码。
- 功能参照 [PDF Looker Pro](https://github.com/dongfangxy/pdf-looker-pro)，未复制其应用代码。

没有广告、遥测、账号、在线服务或默认文件关联修改。
