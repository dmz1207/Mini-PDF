# Mini PDF 0.8.0 — Performance and size

[English](README.md) | [简体中文](README.zh-CN.md)

This build improves how Mini PDF uses PDFium. The pinned native PDFium DLL is byte-for-byte unchanged; this is not a new PDFium C++ build. Rendering settings and output resolution are retained.

## v0.9.0 compatibility follow-up

The measurements below belong to v0.8.0 and have not been remeasured as v0.9.0 speed claims. v0.9.0 limits the cooperative UI path to small text-only pages, preserves existing/imported font programs and serializes/reloads merged documents before later edits. That extra normalization can cost merge time and memory; preserving fonts can make some output PDFs larger. It fixes observed compatibility failures without replacing or reducing raster quality. Private import copies also preserve layer defaults and repair invalid Contents-array references. On-demand raster workers have a 768 MiB process commit limit, a 30-second native watchdog and a 45-second Windows compatibility watchdog. The fallback uses the installed Windows renderer, adds no bundled engine, and is restricted to read-only preview because the tested malformed JPX has color differences. These bounds do not isolate or cap every main-process operation. See [Compatibility](Compatibility.md).

## Measured results

Windows 10 Home Chinese x64, build 19045, eight logical CPUs, system .NET Framework. Compare v0.7.2 with v0.8.0 on the same machine and the same deterministic fixtures. The table is one sequential baseline/current run, with one warm-up and the median of seven iterations (five for serialization). Timings describe these fixtures, not all PDFs or cold disk startup.

| Operation | v0.7.2 | v0.8.0 |
| --- | ---: | ---: |
| Render one 794 × 1123 page | 35.977 ms | 39.815 ms |
| Render one 2048 × 2896 page | 129.764 ms | 122.911 ms |
| Render a 768 × 768 viewport tile | 7.678 ms | 6.523 ms |
| Serialize the test document | 2.373 ms | 2.129 ms |
| Merge and serialize 12 distinct pages | 48.759 ms | 4.524 ms |
| Merged output | 12,299,844 bytes | 1,156,117 bytes |
| Open a simple 1,000-page fixture | 2.081 ms | 2.400 ms |
| Read 1,000 cached page sizes ten times | 10.243 ms | 0.269 ms |
| Rebuild and paint an unchanged, cached reading view | 85.641 ms | 33.111 ms |

The 12-page fixture reuses one 1600 × 1200 image and fonts, plus text and vector strokes. Importing a source's pages together preserves resource sharing; the previous per-page importer duplicated those resources. This fixture's merged output is about 90.6% smaller. A scan with different images on every page will have a different result. Repeated copies of the same source page use separate batches so their indirect annotations stay independently editable.

The small-page and open timings above are slightly slower in this run. Other paired runs varied with CPU scheduling and warm caches; this release does not promise faster synchronous rendering for every page. The main gains are avoiding repeated work, keeping expensive raster work off the UI thread, and parallel throughput.

For eight independent 2048 × 2896 renders in the current version, the median of three warmed rounds was **1,024.552 ms sequential** and **579.263 ms with two workers**, about **43.5% less wall time**. These are eight render jobs of the same complex test page, with separate buffers, not an eight-page first-open measurement. IPC and worker startup are not free; no speedup is promised for a single simple page.

Full-page and tile pixel SHA-256 values match the old version exactly at all four rotations, eight comparisons. Separate worker/progressive comparisons and pypdf/MuPDF checks cover additional saved fixtures. Matching these outputs does not prove every PDF renders identically.

## What changed

- Render directly into owned GDI pixel buffers through FPDFBitmap_CreateEx. Remove the extra full-page bitmap/pixel copy.
- Ordinary first pages get a cooperative 8 ms progressive attempt. PDFium checks the pause callback between work units, so this is not a strict maximum stall time. Only completed output is displayed.
- Complex pages, visible tiles and thumbnails use up to two on-demand worker processes. Each has one PDFium thread. Visible requests precede prefetch; cancellation drops obsolete output, failed workers restart, idle processes stop after 30 seconds, and workers exit with the parent.
- Named read-only memory mappings share immutable PDF input. Mapped raster buffers share completed output. The workers use the same extracted executable and DLL, with no extra on-disk engine copy.
- Cache page dimensions, index visible rows and reuse dimension/rotation keyed LRU frames. Large pages render visible 768-pixel tiles. Canvas images target 64 MiB; thumbnail caches have separate limits. Those limits are not a total process-memory limit.
- Reuse immutable bytes for undo snapshots and a 64 KiB serialization callback buffer. Batch unique source pages and restore the exact requested order with one page-tree permutation.
- Forward later opens before unpacking another engine. Store the application icon once in the launcher and reuse it for the window.

Parallelism uses more private PDFium/CLR memory than one process. Inputs and image buffers are shared, but parser caches are not. Two workers are an intentional bound, not a claim that two is best on every computer. The v0.8.0 worker timeout was two minutes; v0.9.0 uses the tighter watchdogs described above. Unusually expensive pages can still report a render error. Search, editing, printing and serialization still use the UI-owned PDFium instance and are not all background operations.

## Compression measurements

Windows Cabinet LZMS supplies compression/decompression; no additional codec is bundled. On an intermediate 8,085,256-byte payload, seven warmed decode samples per case gave:

| LZMS block | Compressed payload | Median decode |
| --- | ---: | ---: |
| 1 MiB | 3,093,174 bytes | 104.390 ms |
| 2 MiB | 3,057,974 bytes | 102.768 ms |
| 4 MiB | 3,029,790 bytes | 100.423 ms |
| 8 MiB | 2,998,136 bytes | 102.336 ms |
| 16 MiB | 2,998,136 bytes | 102.588 ms |
| 32 MiB | 2,998,136 bytes | 100.454 ms |
| 64 MiB | 2,998,136 bytes | 106.050 ms |

All cases passed a SHA-256 round trip. Three alternative entry orders were 398–928 bytes larger at 8 MiB. The complete payload already fits in the 8 MiB block, so a larger block brings no measured size gain here. Removing the duplicated core icon then saved about 11 KiB of compressed data. These probe results are intermediate payload sizes, not the final EXE size; the final byte count and SHA-256 are in [the test record](测试记录.md).

Source whitespace is preserved for readability. Test runners, benchmark dependencies, certificates, native headers, debug symbols and build tools are excluded from the application. The packed download, extracted files and live RAM are different measurements.

## Native engine status and remaining scope

The shipped PDFium 158.0.8111.0 x64 DLL is 7,586,816 bytes and already omits V8/XFA. Its SHA-256 is A318C39C137482DC4687C2973B7EC1C27FC1C20253CF2DC31928F922EAB89E8F. Mini PDF has not patched its C++ sources or rebuilt it in this release. The working machine has the Framework C# compiler, but no PDFium C++/Clang/GN/Windows SDK toolchain.

Further size reduction remains possible in principle through a reproducible custom build, export/component trimming and compiler/linker optimization. These are candidates, not measured savings. Removing font, image or color support could break real documents; any native change needs a representative rendering/printing/editing corpus and comparison with the pinned baseline. Neither an absolute minimum EXE size nor maximum possible performance has been established.

## Reproduce

From a complete checkout, install development-only reportlab, Pillow, pypdf, pymupdf and cryptography as needed. Keep environment/cache paths inside the project work directory.

~~~powershell
powershell -ExecutionPolicy Bypass -File .\源码\build.ps1
powershell -ExecutionPolicy Bypass -File .\源码\benchmark.ps1 -Name current -Parallel
~~~

For an old/current comparison, extract the old portable payload into a separate work directory and pass its MiniPDF.Core.exe path to benchmark.ps1 -CorePath. The adjacent pdfium.dll must match. Use -Parallel only for the new core. Run GUI benchmarks and UI tests sequentially. The script writes fixtures, copies and reports beneath work/benchmarks; it does not modify the input core. The reported managed_bytes values are observed GC heap deltas, not total allocations or peak process memory.

## Primary implementation references

- [PDFium public API and threading rule](https://pdfium.googlesource.com/pdfium/+/refs/heads/main/public/fpdfview.h): calls are not thread-safe. Process isolation preserves independent instances.
- [PDFium progressive rendering API](https://pdfium.googlesource.com/pdfium/+/refs/heads/main/public/fpdf_progressive.h).
- [Page import implementation](https://pdfium.googlesource.com/pdfium/+/refs/heads/main/fpdfsdk/fpdf_ppo.cpp) and [resource remapping](https://pdfium.googlesource.com/pdfium/+/refs/heads/main/core/fpdfapi/edit/cpdf_pageexporter.cpp).
- [PDFium build instructions](https://pdfium.googlesource.com/pdfium/+/refs/heads/main/README.md) and [prebuilt configuration](https://github.com/bblanchon/pdfium-binaries/blob/master/steps/05-configure.sh).
- [Windows LZMS block configuration](https://learn.microsoft.com/en-us/windows/win32/api/compressapi/ne-compressapi-compress_information_class).

## 中文说明

本版优化了 Mini PDF 调用 PDFium 的方式，仍使用同一份校验过的原生 DLL，没有声称已修改或重新编译 PDFium 的 C++ 内核。保持渲染参数、分辨率和功能范围；四个旋转方向的整页及分块共八组像素哈希与旧版一致。

在同一台八逻辑核心的 Windows 10 电脑上，复杂测试页八次渲染的耗时中位数从串行 1024.552 ms 降至两个后台工作进程并行 579.263 ms，约减少 43.5%。不代表所有 PDF 都快同样比例。普通小页面和简单文档打开在这一轮略慢，表格保留了这些结果。主要收益来自并行、复用缓存及避免重复处理。

共享图片和字体的十二页样本，合并及序列化从 48.759 ms 降至 4.524 ms，输出从 12,299,844 字节降至 1,156,117 字节。来源相同的不同页面批量导入以复用资源；同一页的多个副本分开导入，防止便签等间接批注被联动修改。每页图片都不同的扫描件不能套用该缩小比例。

画布缓存目标 64 MiB，缩略图另有缓存，两个后台进程也有各自的解析和运行时内存，因此单 EXE 很小不等于运行内存只有几兆。普通首屏采用可暂停的渐进渲染，复杂页面、分块和缩略图后台处理；编辑、搜索、保存及打印并未全部转为后台。渐进渲染的 8 ms 是协作式预算，不是严格阻塞上限。

压缩块实测 8/16/32/64 MiB 产物大小完全相同，换三种打包顺序反而略大；保留 8 MiB，并去掉约 11 KiB 的重复图标压缩数据。源码保留可读排版，删除空格不能缩小编译后的程序。最终大小与校验值以测试记录为准。

体积尚未证明达到绝对极限。进一步明显缩小需要定制 PDFium 编译、裁剪和实测；当前环境没有相应 C++ 工具链。本版没有凭空承诺内核裁剪的收益，也没有删除字体、颜色或图像支持来换取体积。复现脚本、测量口径和原始接口说明列在上文。
