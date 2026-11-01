# Offline OCR and translation / 离线 OCR 与翻译

Download the complete **MiniAI-Portable.exe** from [Mini-AI Releases](https://github.com/dmz1207/Mini-AI/releases/latest). Place it beside Mini PDF.exe or select it through **Tools → Load MiniAI package**. First use prepares its folder automatically. The last AI client exit cleans verified runtime/model files. Normal results stay in the sibling MiniAI Results folder; instant translations are not saved. Next use extracts again. A small EXE from a source archive does not contain models. Extracted MiniAI.exe/folder loading remains supported.

Mini PDF **0.12.3** and Mini-AI **0.2.0** support streaming selection bubbles and translation-panel output. Opening the reader with a detected pack starts the service/prewarming in the background. Select PDF text and release the mouse to translate it. Pending bubbles remain visible. Completed results have an 8–20 second reading grace; hover, press Ctrl to pin, copy selected result text, expand Source to translate a smaller part, or press Esc to dismiss. **Alt+S** selects a screen region for OCR + translation. The translation panel uses selected input text when a selection exists.

The current fast pack uses **CTranslate2 + OPUS-MT INT8** for English/Simplified Chinese/Traditional Chinese. CPU threads follow physical cores; supported AVX2/AVX-512 is selected automatically. Compatible NVIDIA CUDA 12/cuBLAS can be used automatically when installed; the default pack includes a complete CPU runtime, and GPU failure falls back to CPU. No AMD/Intel GPU backend or local real-GPU speed claim. See [Mini-AI Models](https://github.com/dmz1207/Mini-AI/blob/main/Models.md). The small model can need technical terminology corrections; earlier multilingual releases remain downloadable.

**Fast** uses beam 1 and token streaming; **High quality** uses beam 4 and sentence streaming. Both share translation weights and keep separate caches. Image/screenshot OCR uses tiny in fast mode and medium in quality; PDF text-layer and Word OCR default to **PP-OCRv6 medium**. The image window can overlay translated text, switch original/translated, expose selectable OCR text, copy and save PNG. Photos, scanned pages, clipboard images and screen regions can be recognized, edited and translated. Add a searchable invisible PDF layer or use OCR during Word export. OCR-to-Word retains uncertain figures as images; scans, complex mathematics and handwriting are not guaranteed to become accurate editable objects. Source PDF appearance is preserved when an invisible OCR layer is added.

No document contents are uploaded. The separate service binds to localhost and requires a random token. The reader remains usable without the optional pack; AI actions are disabled when it is absent. A failed inference request does not modify the source PDF.

下载完整 **MiniAI-Portable.exe** 放在阅读器旁，或从工具菜单选择它；首次自动准备；最后一个 AI 客户端退出后清理运行文件。普通结果保留在同级 MiniAI Results，即时翻译不自动保存，下次重新解包。0.12.3 配合 0.2.0 增加流式输出、后台预热和“选中多少译多少”。气泡等待期间保持显示，完成后保留阅读时间；鼠标悬停、Ctrl 固定、选择复制、展开原文再划选、Esc 关闭。Alt+S 截图识别并翻译；照片可在独立 Mini-AI 窗口打开、拖入或粘贴。

新默认翻译模型体积更小、速度更快，支持中英和繁体中文，专业术语仍可能需要修订；旧多语种包保留下载。线程按物理核心数设置，自动选择支持的 AVX2／AVX-512；兼容 NVIDIA CUDA 环境会尝试加速，失败回退 CPU。默认包不含体积较大的可选 CUDA 库，不宣称本机已验证独显性能。识别、翻译、缓存均在本机完成。

极速／高精度两档共用翻译模型，分别使用 Beam 1／4；极速逐步输出，高精度按句输出，不保证每句必然更准确。译图支持原图切换、可选择的 OCR 文字、复制及保存。极速截图使用 tiny，高精度使用 medium；PDF 文字层和转 Word 默认继续使用 medium。
