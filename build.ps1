# Roda os testes e gera dist\Instalar-Monitor-de-Bateria.exe. csc não aceita \\wsl$, por isso compila em %TEMP%.
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot\test.ps1"
if ($LASTEXITCODE -ne 0) { Write-Host 'Testes vermelhos: build abortado'; exit 1 }
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$dir = Join-Path $env:TEMP 'devmon-app'
if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
New-Item -ItemType Directory $dir | Out-Null
Copy-Item "$PSScriptRoot\src\*.cs" $dir
$exe = 'Instalar-Monitor-de-Bateria.exe'
Push-Location $dir
try {
  & $csc /nologo /codepage:65001 /target:winexe /optimize+ "/out:$exe" *.cs
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} finally { Pop-Location }
New-Item -ItemType Directory "$PSScriptRoot\dist" -Force | Out-Null
Copy-Item (Join-Path $dir $exe) "$PSScriptRoot\dist\$exe" -Force
Write-Host "dist\$exe"
