# Offline OCR and translation / 离线 OCR 与翻译

**Mini PDF 0.12.5 — Capture OCR:** with MiniAI-Portable.exe beside the reader,
the toolbar, Tools menu and context menu expose **Capture OCR**. These new actions
are hidden when no package is detected. **Alt+Shift+O** selects a screen region:
automatic mode keeps ordinary text as text and returns formulas as editable
LaTeX. Mixed captures retain both and offer each formula separately. Use Text
only / Equation only to override detection. Copy LaTeX or MathType-compatible
MathML, edit results before copying, and save explicitly when needed. Instant
captures do not write result files or change the PDF. The existing MiniAI 1.0.1
package provides the models; no additional model download is required. Equation
recognition and automatic classification still require review for unusual input.

**0.12.5 截取识别：**检测到同目录或已加载的 MiniAI 功能包，才显示工具栏、
工具菜单和右键中的“截取识别”；没有包时隐藏这些新选项。**Alt+Shift+O**
框选后自动判断：正文保留为文字，公式转为 LaTeX，混合区域同时保留并可逐个
选择公式。可切换“仅文字／仅公式”，修改后复制 LaTeX 或 MathType 使用的
MathML。日期、编号和常见数值优先保留文字。即时截取不自动保存，不修改 PDF。
使用现有 MiniAI 1.0.1 即可，不增加模型；不常见的符号或排布仍需核对。

Download the complete **MiniAI-Portable.exe** from [Mini-AI Releases](https://github.com/dmz1207/Mini-AI/releases/latest). Place it beside Mini PDF.exe or select it through **Tools → Load MiniAI package**. First use prepares its folder automatically. The last AI client exit cleans verified runtime/model files. Normal results stay in the sibling MiniAI Results folder; instant translations are not saved. Next use extracts again. A small EXE from a source archive does not contain models. Extracted MiniAI.exe/folder loading remains supported.

Mini PDF **0.12.4** and Mini-AI **1.0.0** support streaming selection bubbles and translation-panel output. Opening the reader with a detected pack starts the service/prewarming in the background. Select PDF text and release the mouse to translate it. Pending bubbles remain visible. Completed results have an 8–20 second reading grace; hover, press Ctrl to pin, copy selected result text, expand Source to translate a smaller part, or press Esc to dismiss. Drag the top area to move the bubble; all edges and corners resize it. **Alt+S** selects a screen region for OCR + translation. The translation panel uses selected input text when a selection exists.

The current pack uses **Hy-MT2-1.8B 1.25-bit with a compatible llama.cpp runtime**, with 38 target options. CPU is the default. Threads follow physical cores; AVX2 is used only where supported, with a baseline fallback. Background preparation, prompt-prefix reuse and memory caching reduce repeated work. Optional Vulkan support and hardware limits are described in [Mini-AI Models](https://github.com/dmz1207/Mini-AI/blob/main/Models.md). Source-build dependency ZIPs are not needed for normal use.

**Fast** uses greedy decoding; **High quality** uses a different sampling configuration. Both stream results, share translation weights and keep separate caches. Quality does not guarantee better wording for every sentence. Image/screenshot OCR uses tiny in fast mode and medium in quality; PDF text-layer and Word OCR default to **PP-OCRv6 medium**. The image window can overlay translated text, switch original/translated, expose selectable OCR text, copy and save PNG. Formula OCR retains its current model and allows original-image review, manual TeX correction and MathML export. Complex equations, calligraphic symbols and subscripts need review.

The OCR panel accepts dropped photos, multiple image files, a PDF or a text file. Open or paste a clipboard image, capture a screen region, or OCR the current/all/custom PDF pages. **Alt+Shift+O** captures for OCR within the panel; **Alt+S** captures for translation. Results can be corrected, copied or sent to translation. Only results from the current PDF can be added as an invisible searchable layer. OCR during Word export retains uncertain figures as images. Source PDF appearance is preserved when an invisible layer is added.

No document contents are uploaded. The separate service binds to localhost and requires a random token. The reader remains usable without the optional pack; AI actions are disabled when it is absent. A failed inference request does not modify the source PDF.

下载完整 **MiniAI-Portable.exe** 放在阅读器旁，或从工具菜单选择它；普通用户不需要源码构建依赖 ZIP。首次自动准备；最后一个 AI 客户端退出后清理已校验的运行文件。普通结果保留在同级 MiniAI Results，即时翻译不自动保存，下次重新解包。0.12.4 配合 1.0.0 支持流式输出、后台准备和“选中多少译多少”。气泡等待期间保持显示，完成后保留阅读时间；悬停、Ctrl 固定、选择复制、展开原文再划选、Esc 关闭。顶部可拖动，四边和四角均可调整大小。

当前使用 **Hy-MT2 1.25-bit + 兼容 llama.cpp**，提供 38 种目标语言。默认 CPU 按物理核心数运行，支持时启用 AVX2，否则使用基础指令运行库；不强制 AVX-512。识别、翻译、缓存均在本机完成。可选 Vulkan 加速的要求与限制见模型说明。

极速／高精度两档共用翻译模型，分开缓存且都支持流式输出，高精度不保证每句必然更准确。译图支持原图切换、可选择的 OCR 文字、复制及保存。极速照片／截图使用 tiny，高精度和 PDF 默认使用 medium。OCR 面板可拖入照片、多张图片、PDF 或文本，也能打开图片、粘贴图片、截图识别或识别本页／全部／指定页。面板内 Alt+Shift+O 截图识别，Alt+S 截图翻译；外部照片结果不能误加到当前 PDF 的文字层。公式 OCR 暂不换模型，可结合原图与手动 TeX 修改核对复杂符号及上下标。
