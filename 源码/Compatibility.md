# Mini PDF 0.9.0 — Public PDF compatibility tests

[English](README.md) | [简体中文](README.zh-CN.md)

Recorded 2026-10-22 on Windows 10 Home Chinese x64, build 19045. This is an application smoke and preservation test using pinned public PDFs, with independent diagnosis of exceptions. It does not execute the complete upstream PDFium, PDF.js or veraPDF conformance harnesses.

## Sources

| Corpus | Pinned snapshot | PDF paths |
| --- | --- | ---: |
| PDFium | [a84323421e94f484faca52dd9d027934eba42ab8](https://github.com/chromium/pdfium/tree/a84323421e94f484faca52dd9d027934eba42ab8/testing/resources) | 301 |
| PDF.js | [d213fafd7b43212b93788c910d5cdc9e30d0dd8c](https://github.com/mozilla/pdf.js/tree/d213fafd7b43212b93788c910d5cdc9e30d0dd8c/test/pdfs) | 1,004 |
| veraPDF | [bb75f4f0073d9350dfd058c0162a367e6fadf25e](https://github.com/veraPDF/veraPDF-corpus/tree/bb75f4f0073d9350dfd058c0162a367e6fadf25e) | 2,906 |

There are **4,211 paths / 4,206 distinct byte contents**. Duplicate contents run once and map back to their original paths. Every extracted resource was verified against its pinned Git blob hash. All three corpus archives downloaded directly; an auxiliary PDFium-header archive used the system proxy after a direct timeout. No proxy credentials are recorded.

Only PDFium testing/resources, PDF.js test/pdfs and the veraPDF corpus were extracted. Codeload downloads contain complete repository snapshots without Git history; PDF resources total about 281 MiB. **460 external PDF.js .pdf.link targets were not downloaded/tested; 508 PDFium .in templates were not generated/executed.** Upstream files retain their own licenses. They are excluded from app/source archives and deleted from the delivery folder after testing.

## Scope and limits

- Use actual Mini PDF document APIs and the unchanged pinned PDFium 158.0.8111.0 x64 DLL. Exact bulk probe/core/native SHA-256 values are in [the summary](compatibility-summary.json).
- Open with empty passwords or passwords explicitly listed in the PDF.js manifest. Enumerate outlines and visit all pages, subject to a 10,000-page ceiling and per-file budget.
- Render at maximum dimension **600 pixels**, preserving aspect ratio. Record dimensions, character count, extracted-text hash (at most 1,000,000 characters/page), annotation count and pixel hash.
- Save/reopen and compare all page states. For editable unsigned inputs, merge first/last pages into a new PDF, compare, add a note and verify persistence. One-page inputs are deliberately duplicated. Signed and permission-limited inputs skip editing.
- Each distinct input runs in a separate Windows process, with **45 seconds for the whole file**, **768 MiB committed memory** and **four concurrent processes**. These are test-runner limits, not total application memory guarantees.
- Independently inspect the **63 exception paths** using MuPDF, with a 30-second reference budget. Compare source/output through the same renderer. Different renderers need not have identical antialiasing.

This is not an all-feature test for every PDF, a high-resolution visual oracle, a physical-print fidelity proof or an estimate of real-world failure frequency. The corpus deliberately contains invalid, encrypted, empty, fuzzed and resource-exhaustion inputs. The bulk probe exercises direct native document rendering; the reader's restricted Windows fallback is tested separately.

## Final bulk results

| Outcome | PDF paths |
| --- | ---: |
| All applicable unsigned checks pass | **4,133** |
| Signed preservation checks pass; editing skipped | **15** |
| Native merge comparison differences | 3 |
| Native save/reopen comparison difference | 1 |
| Open errors, including security/format cases | 37 |
| Page-specific errors | 7 |
| Other open/page-tree/no-displayable-page exceptions | 11 |
| Whole-file time limit | 3 |
| Native rendering process failure | 1 |
| Total | **4,211** |

Of the 4,133 applicable passes, **10 permission-limited inputs skip editing** and **4,123 actually pass merge and annotation checks**. The 15 signed files have preservation-only status. Raw status ok has 4,136 rows, including the three merge differences, which remain excluded from applicable passes. Completed records include **5,128 successfully rendered source pages**; timed-out processes can render extra pages without returning a completed record.

Evidence: [per-path CSV](compatibility-results.csv), [summary/provenance](compatibility-summary.json), [exceptions and independent diagnosis](compatibility-exceptions.json). The immutable bulk candidate has core SHA-256 e35ba630181ed7227bdb769f30bb6cd88028d06f642d34307bbd8a5ebf46b26e and probe SHA-256 e745f946f6117e5beb2375bd04d38f3cd980486d1ff27b5f6fd63d729d3af898. Later GUI-only compatibility restrictions and guide edits do not alter the document API paths measured by this candidate; the final desktop suite tests those restrictions.

The initial v0.8.0 run had **143 merge differences and five native failures**. The final run has **three differences and one native failure**. No formerly passing case in the intermediate full run became a failure in the final full run. Worker counts differ between baseline/final, so elapsed time is not a controlled speed comparison.

## Repairs verified on public samples

- Preserve existing/imported font programs and normalize merged font state. Four public font cases no longer crash after merge/insert/duplicate followed by text editing and saving.
- Rebuild optional-content group default/view/print state after native page import, while retaining vector/text page content. Keep independent groups distinct even when their names match. Six public layer samples, two same-name fixtures and mixed sixteen-page output pass native and independent preservation checks through merge, insert, duplicate, extract and organization/undo.
- Preserve the viewer-selected configuration separately where required. This repairs issue11144_reduced, issue12007_reduced, issue18823, issue269_1, issue269_2 and visibility_expressions. Arbitrary named alternate configurations are not all retained; there is no full layer-control UI.
- On a private serialized import copy, discard missing/non-stream entries from malformed Contents arrays, retaining every valid stream. PDFium bug_455199 now preserves source text and rendered content in native and independent checks. Original files stay unchanged.
- Reject layered imports into an encrypted target and roll back, rather than stripping its security. Encrypted sources can merge into a new unencrypted PDF. Reorder/delete of original pages remains supported.

The metadata helper only reads canonical, freshly serialized private copies using their xref offsets; it is not a replacement general PDF parser. It applies bounded nesting/group traversal. The native DLL itself was not rebuilt or patched.

## Remaining cases

- **Native-only merge differences:** bug_664284, viewer_ref and ThuluthFeatures still differ in PDFium state comparisons. Independent source/output text and first/last-page pixels match. This is evidence against obvious output loss, not grounds to relabel the native checks as passes.
- **Native-only save difference:** issue2391-2 differs in native pixel replay while independent text/pixels match; corruption is not established.
- **Malformed JPX:** jpx_numcomps still crashes the direct native probe. The reader isolates that raster attempt and retries using Windows' installed PDF renderer. Full-page and visible-region rendering at four rotations and subsequent normal-page recovery pass. However, grayscale/component panels differ from the independent reference: whole-page mean RGB difference is **34.20/255**. Therefore this is a visibly marked **read-only compatibility preview**, with editing, native image export and printing unavailable. It is not a color-accurate repair. [Difference evidence](windows-render-difference.json).
- **Extreme work:** two tiling-pattern-tiny-steps inputs exceed both native and reference budgets. The 10,000-page Isartor input exceeds the whole-file sweep budget, which does not mean opening failed. bug1978317 (32,768 annotations), previously timed out, passes in the final bounded bulk run and a separate focused rerun.
- **Open/page cases:** independent classification records 11 password requirements, 21 reference failures, seven no-displayable-page cases, two XFA-only cases and 14 native open/page differences. A reference failure alone does not prove invalid input. XFA, V8/JavaScript and OCR recognition are not bundled.

Native raster workers have a 30-second watchdog, Windows fallback workers 45 seconds, and owned raster processes a 768 MiB commit limit. Other parser, search, save, edit and print calls are not all isolated or asynchronous. Windows 11 has not been tested on a separate machine; physical printer jobs were not sent.

## OCR, selection and sidebar

The public [PDF.js issue5747.pdf](https://github.com/mozilla/pdf.js/blob/d213fafd7b43212b93788c910d5cdc9e30d0dd8c/test/pdfs/issue5747.pdf) contains a full-page scan with invisible text; actual mouse drag/copy passes. Six generated Chinese samples cover text above/below the scan, transparency, nested Forms and rotated pages. Thumbnail/label layout is checked at 100%, 125%, 150% and 200% scaling.

Cross-page continuous/facing drags, reverse copying, active-page changes during scrolling, held edge scrolling, saved multi-page highlights and single-step undo pass. Independent pypdf/MuPDF checks verify markup, original text and unaffected pages. The screenshot's original file was unavailable, so this is public/generated coverage rather than an exact reproduction. Image-only scans need external OCR; broken encodings or misaligned recognition layers can still affect selection.

## Reproduce

On x64 Windows, install development dependencies from requirements-dev.txt and provide OpenSSL for independent CMS tests. The app itself needs none of these packages. From the repository root:

~~~powershell
powershell -ExecutionPolicy Bypass -File .\源码\test-corpus.ps1 -Python python -Name run1 -Workers 4
python .\源码\verify_pdf.py fixtures .\work\tests
python .\源码\verify_features.py fixtures .\work\tests
python .\源码\verify_fixes.py fixtures .\work\tests
python .\源码\selection_fixtures.py .\work\tests .\work\compatibility
powershell -ExecutionPolicy Bypass -File .\源码\build.ps1 -Test
python .\源码\verify_pdf.py verify .\work\tests
python .\源码\verify_features.py verify .\work\tests
python .\源码\verify_fixes.py verify .\work\tests
python .\源码\verify_merge.py verify .\work\tests
python .\源码\verify_selection.py .\work\tests
python .\源码\verify_compatibility.py .\work\tests
powershell -ExecutionPolicy Bypass -File .\源码\test-launcher.ps1
~~~

The corpus script uses direct-first download routing, verifies files, builds the app/probe and exports reports under work/compatibility. Use a new run name when binaries change; stale cache reuse is rejected by fingerprints. Desktop tests explicitly skip absent public fixtures, so a smaller count is not equivalent to the full release suite. The final release record lists **1,111 assertions**, separate from the corpus paths. Remove work only after all application/test processes exit. The source ZIP keeps reports/tools, not test PDFs, binaries, caches or keys.

## 中文说明

本轮共 4,211 个公开 PDF 路径，去重 4,206 份。三套库均直连下载并核对 Git 哈希；辅助头文件包直连超时后用了系统代理。下载、构建、缓存和测试均放在项目目录，交付时删除测试库，源代码保留复测工具和报告。

4,133 份通过所有适用检查，其中 10 份受权限限制跳过编辑，4,123 份实际通过合并和添加批注；另 15 份通过签名只读保存。剩余 63 个异常路径按密码、页树、XFA、超时和渲染差异分类。合并比较差异从 143 降至 3，原生进程失败从 5 降至 1。公开图层默认状态丢失、异常 Contents 引用导致正文丢失、四种字体崩溃已修复并复测。

异常 JPX 原生解码仍失败，Windows 内置渲染可作只读预览，但整页颜色与参考图仍有明显偏差。因此会提示并禁用该文档编辑、图片导出和打印。3 份原生合并比较、1 份原生保存比较仍保留待处理；独立渲染相同也没有算作原生通过。向加密目标导入带图层页面会取消回滚，任意命名的备用图层配置并非全部保留。

跨页选择、复制、高亮、自动滚动及侧栏页码分离通过实测。公开双层扫描文件和六种中文样本验证通过；没有截图原文件，不声称复现了那份文档。完整发布测试为 1,111 项，范围见测试记录。公开库测试只有应用层指定操作，600 像素渲染、45 秒单文件预算；外链及模板没有执行，不等于上游全套测试通过或所有环境无缺陷。
