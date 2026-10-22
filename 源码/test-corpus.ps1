param([string]$Python = 'python', [ValidateRange(1,8)][int]$Workers = 4,
      [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Name = 'compatibility')
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $PSScriptRoot
$work = Join-Path $projectDir 'work'
$corpus = Join-Path $work 'compatibility'
New-Item -ItemType Directory -Force -Path $work,$corpus | Out-Null
$env:TEMP = $work
$env:TMP = $work
$env:PYTHONDONTWRITEBYTECODE = '1'
& $Python (Join-Path $PSScriptRoot 'corpus_download.py') --workdir $corpus
if ($LASTEXITCODE -ne 0) { throw 'Corpus download/hash verification failed.' }
$coreDir = Join-Path $work 'application'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory (Join-Path $work 'release')
if (-not $?) { throw 'Application build failed.' }
$probe = Join-Path $corpus ('probe-' + $Name)
New-Item -ItemType Directory -Force -Path $probe | Out-Null
Copy-Item -LiteralPath (Join-Path $coreDir 'MiniPDF.Core.exe'),(Join-Path $coreDir 'pdfium.dll') -Destination $probe -Force
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$probeExe = Join-Path $probe 'MiniPdf.Tests.exe'
& $compiler /nologo /optimize+ /platform:x64 /target:exe /codepage:65001 "/out:$probeExe" "/reference:$(Join-Path $probe 'MiniPDF.Core.exe')" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'CorpusProbe.cs')
if ($LASTEXITCODE -ne 0) { throw 'Corpus probe build failed.' }
& $Python (Join-Path $PSScriptRoot 'corpus_test.py') --workdir $corpus --probe $probeExe --name $Name --workers $Workers
if ($LASTEXITCODE -ne 0) { throw 'Corpus runner failed.' }
& $Python (Join-Path $PSScriptRoot 'corpus_report.py') --workdir $corpus --name $Name --references --output (Join-Path $corpus 'reports')
if ($LASTEXITCODE -ne 0) { throw 'Compatibility report failed.' }
