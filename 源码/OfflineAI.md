# Offline OCR and translation

[English](#offline-ocr-and-translation) | [简体中文](#中文)

Install [Mini-AI](https://github.com/dmz1207/Mini-AI/releases) separately. Extract its `MiniAI` folder beside `Mini PDF.exe`, or choose **Tools → Load MiniAI package** once. The reader discovers and starts the local service when needed. Without a complete installed pack, selection and screen translation commands are disabled with an installation hint.

- Drag-select text and release the mouse to show a compact rounded translation bubble near the selection. The default is Chinese/English auto direction. Choose another supported target in its dropdown or copy the result.
- Move away to dismiss. Hold Ctrl to keep the bubble visible, or click its **Ctrl** control to pin/unpin. Starting another selection, clearing it or switching documents cancels the old request.
- Press **Alt+S** or choose **Capture & translate**. The desktop darkens; drag a region and release to OCR and translate that region. Esc/right-click cancels capture. Recognized text is selectable and correctable; select a sentence to translate it, or use Ctrl+Enter after editing. If another app owns Alt+S, the menu/button remains usable.
- Use **OCR / Ctrl+Shift+O** for a current/all/custom page range. Review recognized text before adding an undoable invisible searchable layer. Save to retain it. Exact original scan pixels are preserved by this layer insertion.
- Use **Translate / Ctrl+Alt+T** for a larger independent review window. MiniAI.exe also provides a standalone text/image/screen workspace without the reader. Other Windows applications can use the same authenticated local API; browser extensions require an allowed origin and token.
- **PDF to Word → OCR scanned pages first (MiniAI)** recognizes pages without visible body text, then uses the chosen export mode. Confident horizontal prose on light paper becomes editable. Complex formulas, artwork, low-confidence or uncertain regions retain the original raster. Scan fonts/layout are approximate; review the result. Export does not rewrite the source PDF or undo history.

The reader executable remains about 3 MiB. PP-OCRv6 medium detection/recognition alone is 138,749,438 bytes; Hy-MT2 1.25-bit weights are 461,860,800 bytes, plus runtimes. Models are not hidden in the small reader download.

Inference runs outside the PDF UI and raster workers. AVX2/generic CPU kernels, bounded CPU threads, a persistent translation process, common-prefix KV reuse and a 32-result memory cache reduce repeated work. Superseded requests are canceled. Cached requests and fresh generation have very different latency; cold start and dense full-page medium OCR can take substantially longer than small screen snippets. GPU acceleration is not bundled or verified in this release.

Local checks cover 33 reader/Word integration assertions, 40 service assertions and five independent workspace assertions. Two OCR Word exports were rendered by installed Word and visually inspected. The public PDF corpus checks PDF opening/rendering/save/import, not OCR or translation accuracy. See [test record](测试记录.md) and [Mini-AI verification](https://github.com/dmz1207/Mini-AI/blob/main/Verification.md).

## 中文

外挂包单独下载，解压后的 `MiniAI` 文件夹放在阅读器旁边，或通过工具菜单加载一次。未安装时入口置灰提示。拖选文字松开就近翻译，移开收起，Ctrl 临时或点击固定；支持切换语言和复制。Alt+S 框选屏幕识别翻译，识别文字可修正、选句再译，Ctrl+Enter 重译。快捷键被其他软件占用时，仍可点击菜单或按钮。

Word 导出勾选“扫描页先 OCR（MiniAI）”，高置信度普通正文可编辑，不确定区域和复杂公式保留图片。扫描版式近似重建，需要核对，原 PDF 与撤销历史不变。独立打开 MiniAI.exe 也能使用；其他软件可共用本机 API。当前经过验证的是多核 CPU 与 AVX2，未捆绑或验证 GPU；密集整页识别及首次加载仍需要等待，不能保证所有机器实时完成。
