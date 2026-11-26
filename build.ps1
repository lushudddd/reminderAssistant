$ErrorActionPreference = 'Stop'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$out = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$app = Join-Path $out 'Tixing.exe'
$setup = Join-Path $out 'TixingSetup.exe'
& $csc /nologo /target:winexe /codepage:65001 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/out:$app" (Join-Path $PSScriptRoot 'Reminder.cs')
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
$uninstaller = Join-Path $out 'Uninstaller.exe'
& $csc /nologo /target:winexe /define:UNINSTALLER /codepage:65001 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:Microsoft.CSharp.dll "/out:$uninstaller" (Join-Path $PSScriptRoot 'Setup.cs')
if ($LASTEXITCODE -ne 0) { throw 'Uninstaller build failed.' }
& $csc /nologo /target:winexe /codepage:65001 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:Microsoft.CSharp.dll "/resource:$app,Tixing.exe" "/resource:$uninstaller,Uninstaller.exe" "/out:$setup" (Join-Path $PSScriptRoot 'Setup.cs')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
Get-Item -LiteralPath $setup
