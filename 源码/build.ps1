param([switch]$Test,[string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
$buildWork = Join-Path $projectDir 'work'
$appDir = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { $projectDir }
$stagingDir = Join-Path $buildWork 'application'
New-Item -ItemType Directory -Force -Path $buildWork,$appDir,$stagingDir | Out-Null
$env:TEMP = $buildWork
$env:TMP = $buildWork
$env:PYTHONDONTWRITEBYTECODE = '1'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '需要 64 位 Windows 10/11 自带的 .NET Framework 4.x。' }
$nativeDll = Join-Path $stagingDir 'pdfium.dll'
if (-not (Test-Path -LiteralPath $nativeDll)) {
    $archive = Join-Path $buildWork 'pdfium-win-x64.tgz'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    if (-not (Test-Path -LiteralPath $archive)) {
        Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/bblanchon/pdfium-binaries/releases/download/chromium%2F8111/pdfium-win-x64.tgz' -OutFile $archive
    }
    $expectedHash = '3452D11031050EC65E57F180215B57B9E6D465E3FBEDAFA8A77AD90885C7D23E'
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expectedHash) { throw 'PDFium 下载校验失败。' }
    $unpack = Join-Path $buildWork 'pdfium'
    New-Item -ItemType Directory -Force -Path $unpack | Out-Null
    & tar -xzf $archive -C $unpack
    if ($LASTEXITCODE -ne 0) { throw 'PDFium 解包失败。' }
    Copy-Item -LiteralPath (Join-Path $unpack 'bin\pdfium.dll') -Destination $nativeDll
}
$licenseDir = Join-Path $stagingDir '许可证'
$expectedNativeHash = 'A318C39C137482DC4687C2973B7EC1C27FC1C20253CF2DC31928F922EAB89E8F'
if ((Get-FileHash -LiteralPath $nativeDll -Algorithm SHA256).Hash -ne $expectedNativeHash) { throw 'PDFium DLL 校验失败。' }
New-Item -ItemType Directory -Force -Path $licenseDir | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot 'third-party-licenses\*') -Destination $licenseDir -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination (Join-Path $licenseDir 'MiniPDF-MIT.txt') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '使用说明.txt') -Destination $stagingDir -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UserGuide.en.txt') -Destination $stagingDir -Force
$sources = @('AssemblyInfo.cs','Dialogs.cs','MainWindow.cs','DocumentTabs.cs','WindowFrame.cs','FlatUi.cs','SingleInstance.cs','UiText.cs','UiStrings.cs','Native.cs','NativeViewport.cs','PdfCanvas.cs','PdfDocument.cs','Program.cs' | ForEach-Object { Join-Path $PSScriptRoot $_ })
$common = @('/nologo','/platform:x64','/optimize+','/debug-','/codepage:65001',('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')),'/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll')
$iconPath = Join-Path $PSScriptRoot 'app.ico'
if (Test-Path -LiteralPath $iconPath) { $common += '/win32icon:' + $iconPath }
$corePath = Join-Path $stagingDir 'MiniPDF.Core.exe'
$coreArguments = $common + @('/target:winexe',('/out:' + $corePath))
& $compiler @coreArguments @sources
if ($LASTEXITCODE -ne 0) { throw '应用编译失败。' }
$rawPayload = Join-Path $buildWork 'application.payload.raw'
$payloadFile = [IO.File]::Create($rawPayload)
$writer = New-Object IO.BinaryWriter($payloadFile,[Text.Encoding]::UTF8)
try {
    $packageFiles = @('MiniPDF.Core.exe','pdfium.dll','使用说明.txt','UserGuide.en.txt','许可证\MiniPDF-MIT.txt')
    foreach ($license in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'third-party-licenses') -File) { $packageFiles += '许可证\' + $license.Name }
    $files = @($packageFiles | Sort-Object -Unique | ForEach-Object { Get-Item -LiteralPath (Join-Path $stagingDir $_) })
    $writer.Write([int]0x53554A31)
    $writer.Write([int]$files.Count)
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($stagingDir.Length + 1)
        $bytes = [IO.File]::ReadAllBytes($file.FullName)
        $writer.Write([string]$relative)
        $writer.Write([int]$bytes.Length)
        $writer.Write([byte[]]$bytes)
    }
} finally { $writer.Dispose() }
$packer = Join-Path $buildWork 'MiniPdf.Pack.exe'
$packArguments = $common + @('/target:exe',('/out:' + $packer))
& $compiler @packArguments (Join-Path $PSScriptRoot 'PackProgram.cs') (Join-Path $PSScriptRoot 'PayloadCompression.cs')
if ($LASTEXITCODE -ne 0) { throw '资源压缩工具编译失败。' }
$payload = Join-Path $buildWork 'application.payload.lzms'
& $packer $rawPayload $payload
if ($LASTEXITCODE -ne 0) { throw '资源压缩或解压校验失败。' }
$singleExe = Join-Path $appDir 'Mini PDF.exe'
$launcherArguments = $common + @('/target:winexe',('/out:' + $singleExe),('/resource:' + $payload + ',MiniPDF.Payload'))
& $compiler @launcherArguments (Join-Path $PSScriptRoot 'Launcher.cs') (Join-Path $PSScriptRoot 'PayloadCompression.cs') (Join-Path $PSScriptRoot 'AssemblyInfo.cs')
if ($LASTEXITCODE -ne 0) { throw '单文件封装失败。' }
if ($Test) {
    # The regression runner references the same production assembly, but is never packaged.
    $testRunner = Join-Path $stagingDir 'MiniPdf.Tests.exe'
    $testArguments = $common + @('/target:winexe',('/out:' + $testRunner),('/reference:' + $corePath))
    & $compiler @testArguments (Join-Path $PSScriptRoot 'SelfTest.cs') (Join-Path $PSScriptRoot 'ViewTests.cs') (Join-Path $PSScriptRoot 'LanguageTests.cs') (Join-Path $PSScriptRoot 'FrameTests.cs') (Join-Path $PSScriptRoot 'TestProgram.cs')
    if ($LASTEXITCODE -ne 0) { throw '回归测试工具编译失败。' }
    $testFolder = Join-Path $buildWork 'tests'
    New-Item -ItemType Directory -Force -Path $testFolder | Out-Null
    $testProcess = Start-Process -FilePath $testRunner -ArgumentList @(('"' + $testFolder + '"')) -WorkingDirectory $projectDir -WindowStyle Hidden -PassThru -Wait
    Get-Content -LiteralPath (Join-Path $testFolder 'self-test.txt') -Encoding UTF8
    if ($testProcess.ExitCode -ne 0) { throw ('自检失败，进程退出码：' + $testProcess.ExitCode) }
}
Get-Item -LiteralPath $singleExe | Select-Object Name,Length
