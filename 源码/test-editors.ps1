param([string]$Python='python',[ValidateSet('Text','OfficeForms','All')][string]$Suite='All',[switch]$CoreOnly,[switch]$UseExistingBuild)
$ErrorActionPreference='Stop'
$projectDir=Split-Path -Parent $PSScriptRoot
$work=Join-Path $projectDir 'work'
$env:TEMP=$work; $env:TMP=$work; $env:PYTHONDONTWRITEBYTECODE='1'
New-Item -ItemType Directory -Force -Path $work | Out-Null
if(-not $UseExistingBuild){ & (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory (Join-Path $work 'release') }
$application=Join-Path $work 'application'
$probe=Join-Path $work 'editor-probe'
New-Item -ItemType Directory -Force -Path $probe | Out-Null
Copy-Item -LiteralPath (Join-Path $application 'MiniPDF.Core.exe'),(Join-Path $application 'pdfium.dll') -Destination $probe -Force
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources=@('SelfTest.cs','ViewTests.cs','LanguageTests.cs','FrameTests.cs','FeatureTests.cs','SignatureTests.cs','MergeBoardTests.cs','ManyMergeTests.cs','PerformanceTests.cs','SelectionTests.cs','CompatibilityTests.cs','TextEditingTests.cs','OfficeFormTests.cs','TestProgram.cs') | ForEach-Object {Join-Path $PSScriptRoot $_}
$runner=Join-Path $probe 'MiniPdf.Tests.exe'
& $compiler /nologo /optimize+ /platform:x64 /target:winexe /codepage:65001 "/out:$runner" "/reference:$(Join-Path $probe 'MiniPDF.Core.exe')" /reference:System.dll /reference:System.Core.dll /reference:System.Security.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Xml.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll @sources
if($LASTEXITCODE -ne 0){throw 'Editor test runner compilation failed.'}

foreach($selectedSuite in $(if($Suite -eq 'All'){@('Text','OfficeForms')}else{@($Suite)})) {
    $textSuite=$selectedSuite -eq 'Text'
    $folder=if($textSuite){'text-edit-tests'}else{'office-forms-tests'}
    $fixtures=Join-Path $work $folder
    New-Item -ItemType Directory -Force -Path $fixtures | Out-Null
    $verifier=Join-Path $PSScriptRoot $(if($textSuite){'verify_text_edit.py'}else{'verify_office_forms.py'})
    & $Python $verifier fixtures $fixtures
    if($LASTEXITCODE -ne 0){throw "$selectedSuite fixture generation failed."}
    if($textSuite) {
        $publicFont=Join-Path $work 'compatibility/corpora/pdfjs/SimFang-variant.pdf'
        if(Test-Path -LiteralPath $publicFont){Copy-Item -LiteralPath $publicFont -Destination (Join-Path $fixtures 'font-source-SimFang-variant.pdf') -Force}
    }
    $mode=if($textSuite){if($CoreOnly){'--text-edit-core'}else{'--text-edit-only'}}else{if($CoreOnly){'--office-forms-core'}else{'--office-forms'}}
    $log=Join-Path $fixtures $(if($textSuite){'text-edit-test.txt'}else{'office-forms-test.txt'})
    $process=Start-Process -FilePath $runner -ArgumentList @(('"'+$fixtures+'"'),$mode) -WorkingDirectory $projectDir -WindowStyle Hidden -PassThru
    if(-not $process.WaitForExit(180000)){try{$process.Kill()}catch{};throw "$selectedSuite regression tests timed out."}
    Get-Content -LiteralPath $log -Encoding UTF8
    if($process.ExitCode -ne 0){throw "$selectedSuite regression tests failed."}
    & $Python $verifier verify $fixtures
    if($LASTEXITCODE -ne 0){throw "$selectedSuite independent verification failed."}
}
