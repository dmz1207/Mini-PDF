# Contributing

[English](README.md) | [简体中文](README.zh-CN.md)

Report a bug with the Mini PDF version, Windows build, display scaling, exact steps and expected behavior. Include a public PDF link or a reduced sample when possible. Remove private information before sharing. A screenshot alone cannot establish its original PDF structure.

Keep changes focused, use native Windows APIs, and retain vector/text content and original files. Reproduce a reported failure before changing rendering or save behavior. Add a small regression fixture or pinned public-case test when useful. Compare saved files with an independent parser/renderer; a successful open alone is insufficient.

Build with 源码/build.ps1. Full desktop verification and public-corpus commands are documented in [Compatibility](Compatibility.md). Python dependencies are development-only. All local downloads, build files and test outputs belong in work; never commit test certificates, user PDFs, corpus downloads or unpacked runtimes.

The checked-in workflow runs bounded native preservation checks on Windows after relevant pushes/pull requests. compare_corpus.py rejects new failures against reviewed, content-identical baseline entries. It does not replace desktop interaction, printer/device testing, or review of known exceptions. A timeout can depend on machine load; investigate before changing the baseline.

## 中文

反馈请附版本、Windows 版本、显示缩放、操作步骤，以及可公开的原 PDF 或精简样本。不要提交含隐私的文件。修复优先保留原文、文字和矢量，使用独立工具验证保存结果；仅能打开不代表正确。全部临时文件放 work，测试证书、下载库、运行缓存不要提交。完整复测方法及已知问题见兼容性报告。
