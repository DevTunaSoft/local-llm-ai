# Compile and test on Windows without producing or publishing an apphost EXE.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Push-Location (Join-Path $PSScriptRoot '..')
try {
    $properties = @('-p:UseAppHost=false', '-p:SelfContained=false', '-p:PublishSingleFile=false')
    dotnet restore WorkIntelligence.slnx @properties
    if ($LASTEXITCODE -ne 0) { throw 'Source restore failed' }
    dotnet build WorkIntelligence.slnx -c Release --no-restore @properties
    if ($LASTEXITCODE -ne 0) { throw 'Source compilation failed' }
    dotnet test tests/WorkIntelligence.Tests.csproj -c Release --no-build --no-restore @properties --logger 'console;verbosity=normal'
    if ($LASTEXITCODE -ne 0) { throw 'Windows unit tests failed' }
    # Portable production-service tests cover SQLite, plaintext snapshots, memory,
    # corrections and workspace isolation; no SQLCipher DLL or restic is required.
    dotnet restore tests/Core/WorkIntelligence.Core.Tests.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Service test restore failed' }
    dotnet test tests/Core/WorkIntelligence.Core.Tests.csproj -c Release --no-restore --logger 'console;verbosity=normal'
    if ($LASTEXITCODE -ne 0) { throw 'Windows production-service regression tests failed' }
} finally { Pop-Location }
