# Compila src\*.cs (menos Program.cs) + tests\*.cs num console e roda. csc não aceita \\wsl$, por isso copia antes.
$ErrorActionPreference = 'Stop'
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$dir = Join-Path $env:TEMP 'devmon-build'
if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
New-Item -ItemType Directory $dir | Out-Null
Get-ChildItem "$PSScriptRoot\src\*.cs", "$PSScriptRoot\tests\*.cs" | Where-Object Name -ne 'Program.cs' | Copy-Item -Destination $dir
Push-Location $dir
try {
  & $csc /nologo /codepage:65001 /target:exe /out:tests.exe *.cs
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
  & .\tests.exe
  exit $LASTEXITCODE
} finally { Pop-Location }
