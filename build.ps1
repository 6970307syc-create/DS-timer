$ErrorActionPreference = "Stop"

$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$outDir = Join-Path $PSScriptRoot "release"
$outExe = Join-Path $outDir "DS_Timer.exe"

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

& $csc `
  /nologo `
  /target:winexe `
  /platform:x64 `
  /optimize+ `
  /out:$outExe `
  /reference:System.dll `
  /reference:System.Core.dll `
  /reference:System.Drawing.dll `
  /reference:System.Windows.Forms.dll `
  /reference:"$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF\System.Speech.dll" `
  /resource:"D:\-7s28Q5-50f2XcZ38\down.wav,DSTimer.Audio.down.wav" `
  /resource:"D:\-7s28Q5-50f2XcZ38\megabeam.wav,DSTimer.Audio.megabeam.wav" `
  "$PSScriptRoot\DS_Timer.cs"

Write-Host "Built $outExe"
