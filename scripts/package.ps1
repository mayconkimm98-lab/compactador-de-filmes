param([string]$OutputDirectory = 'artifacts', [switch]$RunTests)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$output = [IO.Path]::GetFullPath((Join-Path $repo $OutputDirectory))
$stage = Join-Path $output ('build-' + [Guid]::NewGuid().ToString('N'))
$app = Join-Path $stage 'CompactadorFilmes'
New-Item -ItemType Directory -Path $app -Force | Out-Null
dotnet publish (Join-Path $repo 'Compactador/Compactador.csproj') -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o $app
if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação.' }
Copy-Item -LiteralPath (Join-Path $repo 'README.md'), (Join-Path $repo 'LEIA-ME.md'), (Join-Path $repo 'THIRD-PARTY-NOTICES.md') -Destination $app
if ($RunTests) {
    $testDirectory = Join-Path $stage 'testes'
    $process = Start-Process -FilePath (Join-Path $app 'Compactador.exe') -ArgumentList @('--self-test', ('"' + $testDirectory + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Autoteste falhou; consulte $testDirectory\relatorio.json" }
    $report = Get-Content -LiteralPath (Join-Path $testDirectory 'relatorio.json') -Raw | ConvertFrom-Json
    if (-not $report.passed) { throw 'Autoteste não passou.' }
}
$zip = Join-Path $output 'CompactadorFilmes-0.2.0-win-x64.zip'
Compress-Archive -LiteralPath $app -DestinationPath $zip -Force
$hash = Get-FileHash -LiteralPath $zip -Algorithm SHA256
($hash.Hash + '  ' + [IO.Path]::GetFileName($zip)) | Set-Content -LiteralPath ($zip + '.sha256') -Encoding ascii
Write-Output "Pacote: $zip"
Write-Output 'Este pacote contém o aplicativo e o .NET; FFmpeg/FFprobe são obtidos separadamente conforme o README.'
