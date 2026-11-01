# CI-only: build the open-source SQLCipher native library rather than using plaintext SQLite.
# Production distribution requires its own pinned, licensed, hash-verified bundle.
param(
    [Parameter(Mandatory=$true)][string]$TestOutputDirectory
)
$ErrorActionPreference = 'Stop'
$root = $env:VCPKG_INSTALLATION_ROOT
if ([string]::IsNullOrWhiteSpace($root)) { throw 'VCPKG_INSTALLATION_ROOT is not defined on the Windows runner.' }
$vcpkg = Join-Path $root 'vcpkg.exe'
if (!(Test-Path -LiteralPath $vcpkg)) { throw 'vcpkg executable was not found on the runner.' }
$revision = & git -C $root rev-parse HEAD
Write-Host "Windows test SQLCipher vcpkg revision: $revision"
$installRoot = Join-Path $env:RUNNER_TEMP 'wi-vcpkg-native'
& $vcpkg install 'sqlcipher[fts5]:x64-windows' --triplet x64-windows --x-install-root $installRoot
if ($LASTEXITCODE -ne 0) { throw "vcpkg SQLCipher build failed with exit code $LASTEXITCODE" }
$nativeDir = Join-Path $installRoot 'x64-windows/bin'
$sqlcipher = Join-Path $nativeDir 'sqlcipher.dll'
if (!(Test-Path -LiteralPath $sqlcipher)) { throw "sqlcipher.dll missing from $nativeDir; do not run encrypted-storage tests without it." }
New-Item -ItemType Directory -Path $TestOutputDirectory -Force | Out-Null
foreach ($dll in (Get-ChildItem -LiteralPath $nativeDir -Filter '*.dll' -File)) {
    Copy-Item -LiteralPath $dll.FullName -Destination $TestOutputDirectory -Force
    $hash = (Get-FileHash -LiteralPath $dll.FullName -Algorithm SHA256).Hash
    Write-Host "Installed native dependency $($dll.Name) SHA256=$hash"
}
if (!(Test-Path -LiteralPath (Join-Path $TestOutputDirectory 'sqlcipher.dll'))) { throw 'SQLCipher runtime copy failed.' }
