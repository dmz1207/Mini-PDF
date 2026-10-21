param([string]$CorePath,[string]$Name='current',[string]$PythonPath='python',[switch]$Parallel)
$ErrorActionPreference='Stop'
$projectDir=Split-Path -Parent $PSScriptRoot
$benchRoot=Join-Path $projectDir 'work\benchmarks'
if ($Name -notmatch '^[a-zA-Z0-9_.-]+$') { throw 'Invalid benchmark name.' }
if (-not $CorePath) { $CorePath=Join-Path $projectDir 'work\application\MiniPDF.Core.exe' }
$CorePath=[IO.Path]::GetFullPath($CorePath)
$runDir=Join-Path $benchRoot $Name
$inputs=Join-Path $benchRoot 'inputs'
New-Item -ItemType Directory -Path $runDir,$inputs -Force | Out-Null
$env:TEMP=$benchRoot
$env:TMP=$benchRoot
$env:PYTHONDONTWRITEBYTECODE='1'
& $PythonPath (Join-Path $PSScriptRoot 'benchmark_fixture.py') $inputs
if ($LASTEXITCODE -ne 0) { throw 'Benchmark fixtures require reportlab and Pillow.' }
Copy-Item -LiteralPath $CorePath -Destination (Join-Path $runDir 'MiniPDF.Core.exe') -Force
Copy-Item -LiteralPath (Join-Path (Split-Path -Parent $CorePath) 'pdfium.dll') -Destination $runDir -Force
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$runner=Join-Path $runDir 'MiniPdf.Tests.exe'
& $compiler /nologo /platform:x64 /optimize+ /reference:System.Drawing.dll /reference:System.Windows.Forms.dll ('/reference:'+(Join-Path $runDir 'MiniPDF.Core.exe')) ('/out:'+$runner) (Join-Path $PSScriptRoot 'Benchmark.cs')
if ($LASTEXITCODE -ne 0) { throw 'Benchmark compilation failed.' }
& $runner $inputs | Tee-Object -FilePath (Join-Path $runDir 'results.txt')
if ($LASTEXITCODE -ne 0) { throw 'Benchmark failed.' }
Get-FileHash -LiteralPath (Join-Path $runDir 'pdfium.dll') -Algorithm SHA256

if ($Parallel) {
    & $compiler /nologo /platform:x64 /optimize+ /reference:System.Drawing.dll /reference:System.Windows.Forms.dll ('/reference:'+(Join-Path $runDir 'MiniPDF.Core.exe')) ('/out:'+$runner) (Join-Path $PSScriptRoot 'ParallelBenchmark.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Parallel benchmark needs the current application core.' }
    & $runner $inputs | Tee-Object -FilePath (Join-Path $runDir 'parallel-results.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Parallel benchmark failed.' }
}
