# GitHub Actions test-only dependency: do not distribute this binary as a release asset.
# The ZIP SHA-256 is pinned, checked before extraction, and the executable's platform/version is checked.
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
    throw 'RUNNER_TEMP is required for the isolated CI restic dependency.'
}
if ([string]::IsNullOrWhiteSpace($env:GITHUB_ENV)) {
    throw 'GITHUB_ENV is required to expose WI_TEST_RESTIC to later CI steps.'
}

$version = '0.18.0'
$expectedZipSha256 = 'c90cfcd577fe3d60d2529021e76bd5637bdcd19d7fa84840a40fcbbf995902de'
$assetName = "restic_${version}_windows_amd64.zip"
$assetUrl = "https://github.com/restic/restic/releases/download/v$version/$assetName"
$work = Join-Path $env:RUNNER_TEMP "wi-restic-$version"
$archive = Join-Path $env:RUNNER_TEMP $assetName

# Never re-use unverified bytes from a previous execution.
if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
New-Item -ItemType Directory -Path $work -Force | Out-Null
Invoke-WebRequest -Uri $assetUrl -OutFile $archive -MaximumRedirection 5
$actualZipSha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
if (-not $actualZipSha256.Equals($expectedZipSha256, [StringComparison]::OrdinalIgnoreCase)) {
    Remove-Item -LiteralPath $archive -Force
    throw "Pinned restic ZIP SHA256 mismatch. Expected $expectedZipSha256; got $actualZipSha256. Do not run backup tests."
}

Expand-Archive -LiteralPath $archive -DestinationPath $work -Force
$resticCandidates = @(Get-ChildItem -LiteralPath $work -Recurse -File -Filter '*.exe' |
    Where-Object { $_.Name -eq "restic_${version}_windows_amd64.exe" })
if ($resticCandidates.Count -ne 1) {
    throw "Expected exactly one official restic Windows x64 executable; found $($resticCandidates.Count)."
}
$restic = Join-Path $work 'restic.exe'
Copy-Item -LiteralPath $resticCandidates[0].FullName -Destination $restic -Force

$versionOutput = (& $restic version 2>&1) -join ' '
if ($LASTEXITCODE -ne 0 -or $versionOutput -notmatch '^restic 0\.18\.0 compiled with .* on windows/amd64') {
    throw "Downloaded restic executable failed the expected Windows x64 version check: $versionOutput"
}
"WI_TEST_RESTIC=$restic" | Out-File -FilePath $env:GITHUB_ENV -Encoding utf8 -Append
Write-Host "Pinned restic $version available to backup integration tests; verified ZIP SHA256=$actualZipSha256"
