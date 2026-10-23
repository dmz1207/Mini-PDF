# CI save-preservation investigation — 2026-10-22

[English](README.md) | [简体中文](README.zh-CN.md)

This investigation and the binary hashes below refer to v0.9.0. See [the current test record](测试记录.md) for v0.9.1 verification.

## What failed

The first public-corpus workflow completed its build and probes, then failed the regression gate on PDF.js `issue14438.pdf`: a saved/reopened rendering differed from the first source rendering. The workflow has one job, so GitHub reported “All jobs failed” for that one failed gate. It did not report that all 4,211 PDFs failed.

The diagnostic run also reopened the unchanged original. Its pixels changed to exactly the same hash as the saved copy, while text, character count, annotation count and dimensions stayed equal. This establishes a process-state-dependent rendering difference and makes the first comparison insufficient to attribute damage to saving. The precise native cache/font mechanism was not established.

## Strict independent confirmation

`CorpusProbe.cs` now retains the initial difference and same-process replay evidence. On a difference, it launches two fresh, hidden PDFium processes: one for the original, one for the saved copy. A page is classified as replay/cache variance only if both fresh snapshots equal each other **and equal the original first snapshot**. Pixel SHA-256, text SHA-256, character/annotation counts, raster dimensions and page dimensions retain their existing comparisons. Unconfirmed differences, child failures and timeouts continue to fail the preservation check. There is no filename exemption or pixel-error allowance.

Diagnostic reports, hashes and PNGs are retained as a GitHub Actions artifact for seven days. The workflow uses pinned Node 24 actions. Test data and these tools are excluded from the app.

## Local checks

- `issue14438.pdf`: 20 complete probe repeats pass save, merge and annotation checks. A separate structural comparison finds no decoded PDF-object/content difference between source and saved copy.
- `issue2391-2.pdf`: unchanged same-process reopens reproduce the pixel difference; fresh original and saved snapshots match the first source exactly. Merge and annotation checks still pass.
- A Unicode input path and paths containing spaces pass the child-process argument check.
- A deliberately changed PDF has different fresh pixel and text hashes; it is correctly distinguished from the original.
- All 13 Python sources parse successfully.

Machine-readable evidence and the completed CI result are in [ci-verification.json](ci-verification.json). The historical bulk CSV/summary and the 1,111 release assertions remain separate records.

## Completed GitHub run

[Run #3](https://github.com/dmz1207/Mini-PDF/actions/runs/39735564148) completed successfully at 2026-10-22 06:49:28 UTC, commit `14657beeafc850fc16379b19c71346a4119e8f6b`, on Windows Server 2025 build 26100. It checked **4,211 paths / 4,206 distinct contents** with **zero newly detected regressions**. The current classification is **4,134 applicable unsigned passes**, 15 signed preservation passes and **62 known exception paths**, including three native merge differences. Raw status `ok` is 4,137, including those three merge differences; 10 permission-limited cases skip editing. There are no remaining save/reopen differences after the strict fresh-process confirmation.

For `issue14438.pdf`, the first source pixel hash and both independent fresh hashes are `0E09824163AE2475137DEF88BA64F4ACA609F94CB9ABF7A384BF438F21889033`. The same-process original/saved replays both have `6ED3D9AF11318E1DD8AD1CE7316CE091C14EEADDA3D25F0F34C18D8945F28D15`. The full page/text/annotation evidence is preserved in JSON. This distinguishes the save-preservation result from a native replay behavior.

No action warnings were emitted. Earlier failed runs remain visible as history. The reviewed baseline is unchanged.

## Scope

This changes the development probe and workflow, with **no application or PDFium binary change**. The EXE remains v0.9.0.0, 3,023,360 bytes, SHA-256 `F098B91B2117E9B49C83020C8AC00B8F74E25867E57B0A950C63DFA8D522B083`. PDFium process-state-dependent replay is still an engine behavior; no C++ repair is claimed. Exact raster checks use a maximum dimension of 600 pixels. Known malformed/password/page-tree/time-limit/native-render cases remain recorded in [Compatibility](Compatibility.md). A green regression gate means no newly detected regression against the reviewed baseline.

## 中文说明

截图里失败的是一个回归比较步骤：`issue14438.pdf` 保存后与首次渲染出现像素差异，工作流只有一个任务，因此邮件显示全部任务失败。它不代表 4,211 份 PDF 都失败。

进一步诊断发现，未修改的原文件在同一个 PDFium 进程里重开，也变成与保存件相同的像素哈希。文字、批注数量和页面尺寸没有变化，不能仅据此判定保存损坏。具体底层缓存或字体机制尚未定位。

现在对这种差异用两个全新进程分别渲染原件和保存件；只有两者像素及页面、文字信息完全一致，且都与原件首次结果一致，才确认为进程状态相关的重放差异。保留最初差异和诊断证据，失败、超时或无法确认仍然报错，没有加文件白名单或放宽像素比较。

本地验证包括 20 次重复、可稳定复现差异的第二个样本、中文/空格路径，以及故意修改正文的反向检查。此次只修正测试归因和工作流，EXE 与 PDFium 没有变；原始公开库结果和已知异常仍保留。最新完整 CI 结果见下方及 JSON。

最新完整运行于北京时间 2026-10-22 14:49:28 通过：[查看 GitHub 结果](https://github.com/dmz1207/Mini-PDF/actions/runs/39735564148)。共检查 4,211 个路径，新增回归为 0；当前 4,134 份通过适用的无签名检查、15 份通过签名保存检查，仍有 62 个已知异常路径。`issue14438.pdf` 的两个独立进程结果与原件首次结果完全一致。旧的失败运行保留为历史记录。
