# Invoke on Windows using PowerShell 7+ and .NET 10 SDK.
# Place vetted Windows llama.cpp, OCR, and OneDrive binaries in vendor/runtime with all required DLLs.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$vendor = Join-Path $root 'vendor/runtime'
$resources = Join-Path $root 'WorkIntelligence/Resources'
$archive = Join-Path $resources 'runtime.zip'
$hashfile = Join-Path $resources 'runtime.sha256'
if (!(Test-Path (Join-Path $vendor 'llama-server.exe'))) { throw 'Release blocked: vendor/runtime/llama-server.exe missing' }
if (!(Test-Path (Join-Path $vendor 'rclone.exe'))) { throw 'Release blocked: vendor/runtime/rclone.exe missing' }
if (!(Test-Path (Join-Path $vendor 'pdfium.dll'))) { throw 'Release blocked: vetted PDFium native renderer absent' }
if (!(Test-Path (Join-Path $vendor 'tesseract.exe'))) { throw 'Release blocked: vendor/runtime/tesseract.exe missing' }
if (!(Test-Path (Join-Path $vendor 'tessdata/eng.traineddata'))) { throw 'Release blocked: vendor/runtime/tessdata/eng.traineddata missing' }
$manifestPath = Join-Path $root 'vendor/SHA256SUMS.txt'
if (!(Test-Path $manifestPath)) { throw 'Release blocked: pinned vendor SHA256SUMS.txt not provided' }
$manifest = @{}
foreach ($line in Get-Content $manifestPath) {
    if ($line -match '^([A-Fa-f0-9]{64})\s+(.+)$') { $manifest[$Matches[2].Trim()] = $Matches[1].ToUpperInvariant() }
}
$verifiedFiles = @{}
foreach ($file in Get-ChildItem $vendor -Recurse -File) {
    $relative = [IO.Path]::GetRelativePath($vendor, $file.FullName).Replace('\','/')
    if (!$manifest.ContainsKey($relative)) { throw "Unverified native component: $relative" }
    if ($verifiedFiles.ContainsKey($relative)) { throw "Duplicate native component: $relative" }
    $verifiedFiles[$relative] = $true
    $actual = (Get-FileHash $file.FullName -Algorithm SHA256).Hash
    if ($actual -ne $manifest[$relative]) { throw "Native binary hash mismatch: $relative" }
}
foreach ($declared in $manifest.Keys) {
    if (!$verifiedFiles.ContainsKey($declared)) { throw "Manifest references missing native component: $declared" }
}

New-Item -ItemType Directory -Force -Path $resources | Out-Null
if (Test-Path $archive) { Remove-Item $archive -Force }
Compress-Archive -Path (Join-Path $vendor '*') -DestinationPath $archive -CompressionLevel Optimal
(Get-FileHash $archive -Algorithm SHA256).Hash | Set-Content $hashfile -NoNewline
Push-Location $root
try {
    dotnet restore WorkIntelligence.slnx
    if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed' }
    # Core tests link production services and exercise plaintext storage,
    # backup integrity, and memory recall. Do not bypass them.
    $env:PATH = "$vendor;$env:PATH"
    dotnet test tests/WorkIntelligence.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Windows WPF tests failed. Release blocked.' }
    dotnet test tests/Core/WorkIntelligence.Core.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Windows storage-service tests failed. Release blocked.' }
    dotnet publish WorkIntelligence/WorkIntelligence.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o (Join-Path $root 'publish')
    if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed' }
    $exe = Join-Path $root 'publish/WorkIntelligence.exe'
    if (!(Test-Path $exe)) { throw 'Expected executable not found' }
    $others = @(Get-ChildItem (Join-Path $root 'publish') -File | Where-Object { $_.Name -ne 'WorkIntelligence.exe' })
    if ($others.Count -ne 0) { throw "Single-file requirement failed: unexpected output files: $($others.Name -join ', ')" }
    # A publish command is not evidence of a signed, accepted production release.
    # Do not mistake an unsigned local candidate for an organization-approved binary.
    $signature = Get-AuthenticodeSignature -FilePath $exe
    Write-Host "Built source/package candidate: $exe"
    Write-Host "Publisher signature status: $($signature.Status)"
    if ($signature.Status -ne 'Valid') {
        throw 'PRODUCTION RELEASE BLOCKED: executable is not signed by a trusted publisher.'
    }
    Write-Host 'SIGNED PACKAGE CANDIDATE ONLY. Real-model, desktop, security, and cross-PC restore acceptance tests remain mandatory.'
} finally { Pop-Location }
