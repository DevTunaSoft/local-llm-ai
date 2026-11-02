# Real Windows 11 CPU-only model acceptance. Run on the target laptop.
# No packaged EXE, downloads, telemetry, or cloud inference.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$RuntimeDirectory,
    [Parameter(Mandatory=$true)][string]$GptOss20BGguf,
    [Parameter(Mandatory=$true)][string]$Qwen27BGguf,
    [string]$ReportDirectory = (Join-Path $PSScriptRoot '..\acceptance-reports')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$runtime = (Resolve-Path -LiteralPath $RuntimeDirectory).Path
if (-not (Test-Path -LiteralPath (Join-Path $runtime 'llama-server.exe') -PathType Leaf)) { throw 'Trusted native llama-server.exe is missing.' }
if (-not (Test-Path -LiteralPath (Join-Path $runtime 'SHA256SUMS.txt') -PathType Leaf)) { throw 'Hash manifest for native runtime is missing.' }
$models = @(
    @{ Name='GPT-OSS 20B'; Path=$GptOss20BGguf },
    @{ Name='Qwen 27B'; Path=$Qwen27BGguf }
)
$report = [ordered]@{
    timestamp_utc = [DateTimeOffset]::UtcNow.ToString('O')
    computer = $env:COMPUTERNAME
    physical_ram_gib = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 2)
    os = (Get-CimInstance Win32_OperatingSystem).Caption
    tests = @()
}
New-Item -ItemType Directory -Path $ReportDirectory -Force | Out-Null
Push-Location $root
try {
    foreach ($item in $models) {
        $model = (Resolve-Path -LiteralPath $item.Path).Path
        $start = [DateTimeOffset]::UtcNow
        $result = [ordered]@{
            model = $item.Name
            model_file = [IO.Path]::GetFileName($model)
            model_bytes = (Get-Item -LiteralPath $model).Length
            model_sha256 = (Get-FileHash -LiteralPath $model -Algorithm SHA256).Hash
            started_at_utc = $start.ToString('O')
            outcome = 'failed'
        }
        $env:WI_LIVE_TEST_GGUF = $model
        $env:WI_LIVE_TEST_GGUF_SHA256 = $result.model_sha256
        $env:WI_DEV_RUNTIME_DIR = $runtime
        $env:WI_REQUIRE_LIVE_INFERENCE = '1'
        try {
            dotnet test tests/Core/WorkIntelligence.Core.Tests.csproj -c Release --filter 'FullyQualifiedName~RealLocalGgufProducesStreamingCompletion' --logger 'console;verbosity=normal'
            if ($LASTEXITCODE -ne 0) { throw "Real model inference failed with code $LASTEXITCODE." }
            $result.outcome = 'passed'
        } catch {
            $result.error_type = $_.Exception.GetType().Name
            $result.error_summary = $_.Exception.Message
        } finally {
            $result.elapsed_seconds = [math]::Round(([DateTimeOffset]::UtcNow - $start).TotalSeconds, 2)
            $report.tests += $result
            Remove-Item Env:WI_LIVE_TEST_GGUF,Env:WI_LIVE_TEST_GGUF_SHA256,Env:WI_DEV_RUNTIME_DIR,Env:WI_REQUIRE_LIVE_INFERENCE -ErrorAction SilentlyContinue
        }
    }
} finally { Pop-Location }
$out = Join-Path $ReportDirectory ('production-model-acceptance-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '.json')
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $out -Encoding utf8
Write-Host "Local CPU model acceptance report: $out"
if (@($report.tests | Where-Object { $_.outcome -ne 'passed' }).Count) { throw 'At least one real production-size model failed. See local report.' }
Write-Host 'Both CPU production-size models generated output and successfully restarted. This does not certify chat templates or tokens per second.'
