param([string]$SingleExePath)
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path $projectDir 'work\portable-test'
New-Item -ItemType Directory -Force -Path $testRoot | Out-Null
$env:TEMP = $testRoot
$env:TMP = $testRoot
if (-not $SingleExePath) { $SingleExePath = Join-Path $projectDir 'Mini PDF.exe' }
$isolatedExe = Join-Path $testRoot 'Mini PDF.exe'
Copy-Item -LiteralPath $SingleExePath -Destination $isolatedExe -Force
$sourcePdf = Join-Path $projectDir 'work\tests\baseline.pdf'
$spacedPdf = Join-Path $testRoot '中文 测试文件 with spaces.pdf'
Copy-Item -LiteralPath $sourcePdf -Destination $spacedPdf -Force
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class MiniPdfWindowProbe {
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr window, StringBuilder text, int length);
}
'@
$checks = New-Object Collections.Generic.List[string]
$running = New-Object Collections.Generic.List[object]
function Start-CheckedWindow([string]$document, [string]$expectedTitle) {
    $launchArgs = @{FilePath=$isolatedExe;PassThru=$true;WindowStyle='Hidden'}
    if ($document) { $launchArgs.ArgumentList = '"' + $document + '"' }
    $wrapper = Start-Process @launchArgs
    $child = $null
    $deadline = (Get-Date).AddSeconds(12)
    while ((Get-Date) -lt $deadline) {
        $candidate = Get-CimInstance Win32_Process -Filter ("ParentProcessId = " + $wrapper.Id) | Where-Object Name -eq 'MiniPDF.Core.exe' | Select-Object -First 1
        if ($candidate) {
            $child = [Diagnostics.Process]::GetProcessById([int]$candidate.ProcessId)
            $child.Refresh()
            if ($child.MainWindowHandle -ne [IntPtr]::Zero) { break }
        }
        Start-Sleep -Milliseconds 80
    }
    if (-not $child -or $child.MainWindowHandle -eq [IntPtr]::Zero) { throw '单文件程序未显示主窗口。' }
    $class = New-Object Text.StringBuilder(256)
    [void][MiniPdfWindowProbe]::GetClassName($child.MainWindowHandle,$class,256)
    if (-not $class.ToString().StartsWith('WindowsForms10.')) { throw ('启动显示了异常对话框：' + $child.MainWindowTitle) }
    $deadline = (Get-Date).AddSeconds(8)
    while (-not $child.MainWindowTitle.Contains($expectedTitle) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 80; $child.Refresh() }
    if (-not $child.MainWindowTitle.Contains($expectedTitle)) { throw ('主窗口标题错误：' + $child.MainWindowTitle) }
    $record = [PSCustomObject]@{Wrapper=$wrapper;Child=$child;Runtime=(Join-Path $testRoot ('.MiniPDF-runtime-' + $wrapper.Id + '-*'))}
    $running.Add($record)
    return $record
}
function Close-CheckedWindow($record) {
    [void]$record.Child.CloseMainWindow()
    if (-not $record.Wrapper.WaitForExit(10000)) { throw '单文件程序退出超时。' }
    if ($record.Wrapper.ExitCode -ne 0) { throw ('单文件程序退出码错误：' + $record.Wrapper.ExitCode) }
}
try {
    if (Test-Path -LiteralPath (Join-Path $testRoot 'pdfium.dll')) { throw '独立测试目录不应含有额外 DLL。' }
    $first = Start-CheckedWindow '' 'Mini PDF'
    $checks.Add('Single EXE starts normally without adjacent DLL or runtime installation')
    $second = Start-CheckedWindow $spacedPdf '中文 测试文件 with spaces.pdf'
    $checks.Add('Chinese path containing spaces is passed correctly to the viewer')
    $first.Child.Refresh()
    if ($first.Child.HasExited) { throw '启动第二个实例破坏了第一个实例。' }
    $checks.Add('Concurrent instances keep each other alive')
    Close-CheckedWindow $second
    $secondRuntimes = @(Get-ChildItem -LiteralPath $testRoot -Force -Directory | Where-Object Name -like ('.MiniPDF-runtime-' + $second.Wrapper.Id + '-*'))
    if ($secondRuntimes.Count -ne 0) { throw '正常退出后没有清理运行目录。' }
    $checks.Add('Normal exit cleans extracted runtime files')
    $firstRuntime = Get-ChildItem -LiteralPath $testRoot -Force -Directory | Where-Object Name -like ('.MiniPDF-runtime-' + $first.Wrapper.Id + '-*') | Select-Object -First 1
    $keepFile = Join-Path $firstRuntime.FullName '用户自己创建的文件.txt'
    [IO.File]::WriteAllText($keepFile,'Preserve user data during cleanup')
    Close-CheckedWindow $first
    if (-not (Test-Path -LiteralPath $keepFile)) { throw '运行目录清理误删了用户文件。' }
    $checks.Add('Cleanup preserves user-created files even inside runtime directory')
    [IO.File]::Delete($keepFile)
    $third = Start-CheckedWindow '' 'Mini PDF'
    Close-CheckedWindow $third
    $remaining = @(Get-ChildItem -LiteralPath $testRoot -Force -Directory | Where-Object Name -like '.MiniPDF-runtime-*')
    if ($remaining.Count -ne 0) { throw '再次启动没有清理遗留运行目录。' }
    $checks.Add('Next launch removes stale runtime files safely')
    $checks | Set-Content -LiteralPath (Join-Path $testRoot 'launcher-checks.txt') -Encoding UTF8
    'Launcher checks: ' + $checks.Count + ' passed'
} finally {
    foreach ($record in $running) {
        try { if (-not $record.Child.HasExited) { $record.Child.Kill() }; [void]$record.Wrapper.WaitForExit(6000) } catch { }
    }
}
