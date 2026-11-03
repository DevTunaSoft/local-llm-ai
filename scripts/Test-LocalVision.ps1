# Requires actual authorized model/projector files and a synthetic image containing 427.
# This launches source test DLLs only; no application EXE/installer is packaged.
[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$RuntimeDirectory,
  [Parameter(Mandatory=$true)][string]$ModelPath,
  [Parameter(Mandatory=$true)][string]$ModelSha256,
  [Parameter(Mandatory=$true)][string]$ProjectorPath,
  [Parameter(Mandatory=$true)][string]$ProjectorSha256,
  [Parameter(Mandatory=$true)][string]$SyntheticImage
)
$ErrorActionPreference='Stop'
$values=@{
  WI_DEV_RUNTIME_DIR=(Resolve-Path -LiteralPath $RuntimeDirectory).Path
  WI_LIVE_VISION_MODEL=(Resolve-Path -LiteralPath $ModelPath).Path
  WI_LIVE_VISION_MODEL_SHA256=$ModelSha256
  WI_LIVE_VISION_PROJECTOR=(Resolve-Path -LiteralPath $ProjectorPath).Path
  WI_LIVE_VISION_PROJECTOR_SHA256=$ProjectorSha256
  WI_LIVE_VISION_IMAGE=(Resolve-Path -LiteralPath $SyntheticImage).Path
  WI_REQUIRE_LIVE_VISION='1'
}
$previous=@{}
try {
  foreach($name in $values.Keys){$previous[$name]=[Environment]::GetEnvironmentVariable($name);[Environment]::SetEnvironmentVariable($name,$values[$name])}
  dotnet test (Join-Path $PSScriptRoot '../tests/Core/WorkIntelligence.Core.Tests.csproj') -c Release --filter 'FullyQualifiedName~RealCpuVisionModelReadsSyntheticImage' --logger 'console;verbosity=normal'
  if($LASTEXITCODE -ne 0){throw 'Real CPU vision acceptance failed; no multimodal compatibility is certified.'}
} finally {foreach($name in $previous.Keys){[Environment]::SetEnvironmentVariable($name,$previous[$name])}}
