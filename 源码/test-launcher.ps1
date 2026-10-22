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
$sourcePdf = Join-Path $testRoot 'baseline.pdf'
Copy-Item -LiteralPath (Join-Path $projectDir 'work\tests\external.pdf') -Destination $sourcePdf -Force
$spacedPdf = Join-Path $testRoot '中文 测试文件 with spaces.pdf'
Copy-Item -LiteralPath $sourcePdf -Destination $spacedPdf -Force
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using System.Threading;
public sealed class MiniPdfRuntimeWatch : IDisposable {
    readonly FileSystemWatcher watcher;
    int count;
    public int Created { get { return Volatile.Read(ref count); } }
    public MiniPdfRuntimeWatch(string directory){watcher=new FileSystemWatcher(directory,".MiniPDF-runtime-*"){NotifyFilter=NotifyFilters.DirectoryName};watcher.Created+=delegate{Interlocked.Increment(ref count);};watcher.EnableRaisingEvents=true;}
    public void Dispose(){watcher.Dispose();}
}
public static class MiniPdfWindowProbe {
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr window, StringBuilder text, int length);
    public delegate bool ChildCallback(IntPtr window, IntPtr value);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr window, ChildCallback callback, IntPtr value);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int left,top,right,bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr window, int message, IntPtr w, IntPtr l);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetStyle(IntPtr window,int index);
    [StructLayout(LayoutKind.Sequential)] public struct Point {public int x,y;}
    [DllImport("user32.dll")]public static extern bool ClientToScreen(IntPtr window,ref Point point);
    public static bool SingleTitleRow(IntPtr window){Rect bounds;GetWindowRect(window,out bounds);var point=new Point();ClientToScreen(window,ref point);return ((long)GetStyle(window,-16)&0xc00000)==0&&point.y-bounds.top<=2;}
    public static int Tabs(IntPtr parent) { int count=0; EnumChildWindows(parent,delegate(IntPtr child,IntPtr unused){var text=new StringBuilder(256);GetClassName(child,text,256);Rect rect;GetWindowRect(child,out rect);if(text.ToString().Contains("SysTabControl32")&&rect.bottom-rect.top<90)count=(int)SendMessage(child,0x1304,IntPtr.Zero,IntPtr.Zero);return true;},IntPtr.Zero);return count; }
}
'@
$checks = New-Object Collections.Generic.List[string]
$running = New-Object Collections.Generic.List[object]
function Start-CheckedWindow([string]$document, [string]$expectedTitle, [bool]$separate=$true, [string]$extraDocument='') {
    $launchArgs = @{FilePath=$isolatedExe;PassThru=$true;WindowStyle='Hidden';WorkingDirectory=$testRoot}
    $arguments = if ($separate) { '--new-window ' } else { '' }
    if ($document) { $arguments += '"' + $document + '"' }
    if ($extraDocument) { $arguments += ' "' + $extraDocument + '"' }
    if ($arguments.Trim()) { $launchArgs.ArgumentList = $arguments }
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
function Forward-Checked([string]$arguments) {
    $launchArgs = @{FilePath=$isolatedExe;PassThru=$true;WindowStyle='Hidden';WorkingDirectory=$testRoot}
    if ($arguments) { $launchArgs.ArgumentList=$arguments }
    $forwarded = Start-Process @launchArgs
    if (-not $forwarded.WaitForExit(12000)) { throw '后续打开文件未转入已有窗口。' }
    if ($forwarded.ExitCode -ne 0) { throw ('文件转交失败：' + $forwarded.ExitCode) }
}
function Wait-TabCount($record,[int]$count,[string]$title='') {
    $deadline=(Get-Date).AddSeconds(8)
    do {
        $record.Child.Refresh()
        if ([MiniPdfWindowProbe]::Tabs($record.Child.MainWindowHandle) -eq $count -and (-not $title -or $record.Child.MainWindowTitle.Contains($title))) { return }
        Start-Sleep -Milliseconds 80
    } while ((Get-Date) -lt $deadline)
    throw ('标签数量或标题错误，预期 ' + $count + ' 个标签：' + $record.Child.MainWindowTitle)
}
try {
    if (Test-Path -LiteralPath (Join-Path $testRoot 'pdfium.dll')) { throw '独立测试目录不应含有额外 DLL。' }
    $first = Start-CheckedWindow '' 'Mini PDF'
    $checks.Add('Single EXE starts normally without adjacent DLL or runtime installation')
    Add-Type -AssemblyName System.Drawing
    $iconHandle = [MiniPdfWindowProbe]::SendMessage($first.Child.MainWindowHandle,0x7f,[IntPtr]1,[IntPtr]::Zero)
    if ($iconHandle -eq [IntPtr]::Zero) { throw '单文件窗口缺少图标。' }
    # Match the application manifest: system-DPI-aware extraction can select 48px while the shell host selects 32px.
    $previousDpi = [MiniPdfWindowProbe]::SetThreadDpiAwarenessContext([IntPtr](-2))
    try { $expectedIcon = [Drawing.Icon]::ExtractAssociatedIcon($isolatedExe) }
    finally { if ($previousDpi -ne [IntPtr]::Zero) { [void][MiniPdfWindowProbe]::SetThreadDpiAwarenessContext($previousDpi) } }
    $actualIcon = [Drawing.Icon]::FromHandle($iconHandle).Clone()
    $expectedBitmap = $expectedIcon.ToBitmap()
    $actualBitmap = $actualIcon.ToBitmap()
    try {
        if ($expectedBitmap.Size -ne $actualBitmap.Size) { throw ('单文件窗口图标尺寸错误：预期 ' + $expectedBitmap.Size + '，实际 ' + $actualBitmap.Size) }
        for ($y=0; $y -lt $actualBitmap.Height; $y++) {
            for ($x=0; $x -lt $actualBitmap.Width; $x++) {
                if ($actualBitmap.GetPixel($x,$y).ToArgb() -ne $expectedBitmap.GetPixel($x,$y).ToArgb()) { throw '窗口图标没有正确沿用启动器图标。' }
            }
        }
    } finally { $expectedBitmap.Dispose(); $actualBitmap.Dispose(); $expectedIcon.Dispose(); $actualIcon.Dispose() }
    $checks.Add('Window icon exactly matches the launcher while the core omits its duplicate icon resource')

    if (-not [MiniPdfWindowProbe]::SingleTitleRow($first.Child.MainWindowHandle)) { throw '最终单 EXE 在非最大化窗口仍保留系统标题栏，出现双层窗口按钮。' }
    $checks.Add('Actual single-EXE normal window has no native caption style or extra title-row reservation')
    [void][MiniPdfWindowProbe]::SendMessage($first.Child.MainWindowHandle,0x112,[IntPtr]0xf030,[IntPtr]::Zero)
    [void][MiniPdfWindowProbe]::SendMessage($first.Child.MainWindowHandle,0x112,[IntPtr]0xf120,[IntPtr]::Zero)
    if (-not [MiniPdfWindowProbe]::SingleTitleRow($first.Child.MainWindowHandle)) { throw '最终 EXE 最大化再还原后出现双层标题栏。' }
    $checks.Add('Actual single-EXE maximize and restore leave exactly one title row in normal window mode')
    $second = Start-CheckedWindow $spacedPdf '中文 测试文件 with spaces.pdf'
    $checks.Add('Chinese path containing spaces is passed correctly to the viewer')
    $first.Child.Refresh()
    if ($first.Child.HasExited) { throw '启动第二个实例破坏了第一个实例。' }
    $checks.Add('Concurrent instances keep each other alive')
    Close-CheckedWindow $second
    $secondRuntimes = @(Get-ChildItem -LiteralPath $testRoot -Force -Directory | Where-Object Name -like ('.MiniPDF-runtime-' + $second.Wrapper.Id + '-*'))
    if ($secondRuntimes.Count -ne 0) { throw '正常退出后没有清理运行目录。' }
    $children = @(Get-CimInstance Win32_Process -Filter ("ParentProcessId = " + $second.Child.Id) | Where-Object Name -eq 'MiniPDF.Core.exe')
    if ($children.Count -ne 0) { throw '退出后仍有渲染工作进程。' }
    $checks.Add('Normal exit stops workers from a multipage PDF before removing the extracted engine')
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
    $primary = Start-CheckedWindow $sourcePdf 'baseline.pdf' $false
    $runtimeWatch = [MiniPdfRuntimeWatch]::new($testRoot)
    Forward-Checked ('"' + $spacedPdf + '"')
    Wait-TabCount $primary 2 '中文 测试文件 with spaces.pdf'
    $checks.Add('Subsequent double-click forwards Chinese filename to existing window as a new tab')
    Forward-Checked ('"' + $sourcePdf + '"')
    Wait-TabCount $primary 2 'baseline.pdf'
    $checks.Add('Opening an existing document activates its tab without duplication')
    Forward-Checked ''
    Wait-TabCount $primary 2
    $checks.Add('Starting without a filename focuses existing window without a new document')
    Start-Sleep -Milliseconds 150
    if ($runtimeWatch.Created -ne 0) { throw '后续打开不应重新建立内核解包目录。' }
    $runtimeWatch.Dispose()
    $checks.Add('Warm opens forward before extraction without creating any temporary engine directory')
    $extra = Start-CheckedWindow '中文 测试文件 with spaces.pdf' 'baseline.pdf' $true $sourcePdf
    Wait-TabCount $extra 2 'baseline.pdf'
    $primary.Child.Refresh()
    if ($primary.Child.HasExited) { throw '新建窗口破坏了原窗口。' }
    $checks.Add('Explicit new window opens multiple arguments, including a relative Unicode filename')
    Close-CheckedWindow $extra
    $runtimeBefore = @(Get-ChildItem -LiteralPath $testRoot -Force -Directory | Where-Object Name -like '.MiniPDF-runtime-*')
    if ($runtimeBefore.Count -ne 1) { throw '文件转交进程遗留了解包目录。' }
    $checks.Add('Forwarding processes exit and clean their temporary engine copies')
    $primary.Child.Kill()
    if (-not $primary.Wrapper.WaitForExit(10000)) { throw '异常退出后的启动器未退出。' }
    $restarted = Start-CheckedWindow $sourcePdf 'baseline.pdf' $false
    Close-CheckedWindow $restarted
    $checks.Add('Single-instance ownership and file opening recover after an unexpected process exit')
    $remaining = @(Get-ChildItem -LiteralPath $testRoot -Force -Directory | Where-Object Name -like '.MiniPDF-runtime-*')
    if ($remaining.Count -ne 0) { throw '单实例测试退出后遗留运行目录。' }
    $checks | Set-Content -LiteralPath (Join-Path $testRoot 'launcher-checks.txt') -Encoding UTF8
    'Launcher checks: ' + $checks.Count + ' passed'
} finally {
    foreach ($record in $running) {
        try { if (-not $record.Child.HasExited) { $record.Child.Kill() }; [void]$record.Wrapper.WaitForExit(6000) } catch { }
    }
}
