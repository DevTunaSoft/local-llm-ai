# Invoke from the repository root on Windows after installing a trusted llama.cpp
# llama-server.exe and obtaining a local GGUF model. No network inference is used.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ModelPath,
    [Parameter(Mandatory=$true)][string]$RuntimeDirectory
)
$ErrorActionPreference = 'Stop'
$model = (Resolve-Path -LiteralPath $ModelPath -ErrorAction Stop).Path
$runtime = (Resolve-Path -LiteralPath $RuntimeDirectory -ErrorAction Stop).Path
$exe = Join-Path $runtime 'llama-server.exe'
$manifest = Join-Path $runtime 'SHA256SUMS.txt'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Missing native llama-server.exe: $exe" }
if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { throw "Missing SHA256SUMS.txt: $manifest" }
$hash = (Get-FileHash -LiteralPath $model -Algorithm SHA256).Hash
$env:WI_LIVE_TEST_GGUF = $model
$env:WI_LIVE_TEST_GGUF_SHA256 = $hash
$env:WI_DEV_RUNTIME_DIR = $runtime
$env:WI_REQUIRE_LIVE_INFERENCE = "1"
try {
    Write-Host "Running actual offline llama.cpp inference test against $model"
    & dotnet test tests/Core/WorkIntelligence.Core.Tests.csproj -c Release --filter 'FullyQualifiedName~RealLocalGgufProducesStreamingCompletion' --logger 'console;verbosity=normal'
    if ($LASTEXITCODE -ne 0) { throw "Live inference integration test failed (exit code $LASTEXITCODE)." }
    Write-Host 'Live GGUF inference test passed. This does not certify the entire Windows UI.'
}
finally {
    Remove-Item Env:WI_LIVE_TEST_GGUF -ErrorAction SilentlyContinue
    Remove-Item Env:WI_LIVE_TEST_GGUF_SHA256 -ErrorAction SilentlyContinue
    Remove-Item Env:WI_DEV_RUNTIME_DIR -ErrorAction SilentlyContinue
    Remove-Item Env:WI_REQUIRE_LIVE_INFERENCE -ErrorAction SilentlyContinue
}
