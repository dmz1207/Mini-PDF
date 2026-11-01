param([Parameter(Mandatory=$true)][string]$Paper,[Parameter(Mandatory=$true)][string]$MiniAI,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$base=Split-Path -Parent $PSScriptRoot
if(-not $OutputDirectory){$OutputDirectory=Join-Path $base 'work\paper-translation-tests'}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$core=Join-Path $base 'work\application\MiniPDF.Core.exe'
Copy-Item -LiteralPath $core,(Join-Path $base 'work\application\pdfium.dll') -Destination $OutputDirectory -Force
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$runner=Join-Path $OutputDirectory 'MiniPdf.Tests.exe'
& $compiler /nologo /platform:x64 /optimize+ /target:winexe /codepage:65001 /main:MiniPdf.TranslationPaperTests ('/out:'+$runner) ('/reference:'+$core) /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll (Join-Path $PSScriptRoot 'TranslationPaperTests.cs')
if($LASTEXITCODE -ne 0){throw 'Paper test compilation failed.'}
$process=Start-Process -FilePath $runner -ArgumentList ('"'+$Paper+'" "'+$MiniAI+'" "'+$OutputDirectory+'"') -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit(600000)){throw 'Paper tests timed out.'}
Get-Content -LiteralPath (Join-Path $OutputDirectory 'paper-translation-checks.txt')
if($process.ExitCode -ne 0){throw 'Paper translation checks failed.'}
