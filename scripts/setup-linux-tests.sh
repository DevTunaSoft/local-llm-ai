#!/usr/bin/env bash
# Development dependencies only; does not create or publish a Windows executable.
set -euo pipefail
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
export WI_TOOLS_ROOT="${WI_TOOLS_ROOT:-$(dirname -- "$repo_root")/.tools}"
export DOTNET_ROOT="$WI_TOOLS_ROOT/dotnet"
export DOTNET_CLI_HOME="$WI_TOOLS_ROOT/dotnet-home"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
for command_name in python3; do
    command -v "$command_name" >/dev/null || { echo "Missing development prerequisite: $command_name" >&2; exit 1; }
done
python3 - <<'PY'
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tarfile
import tempfile
import urllib.request

root = Path(os.environ['WI_TOOLS_ROOT']).resolve()
root.mkdir(parents=True, exist_ok=True)

def download(url, destination, expected, algorithm='sha256'):
    digest = hashlib.new(algorithm)
    with urllib.request.urlopen(url, timeout=90) as response, destination.open('wb') as output:
        while block := response.read(1024 * 1024):
            digest.update(block)
            output.write(block)
    if digest.hexdigest() != expected:
        destination.unlink()
        raise RuntimeError('Official dependency checksum mismatch: ' + url)

with tempfile.TemporaryDirectory(prefix='wi-dependencies-') as temporary:
    temp = Path(temporary)
    dotnet = root / 'dotnet' / 'dotnet'
    if not dotnet.exists() or subprocess.check_output([str(dotnet), '--version'], text=True).strip() != '10.0.403':
        archive = temp / 'sdk.tar.gz'
        download('https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.403/dotnet-sdk-10.0.403-linux-x64.tar.gz', archive,
                 'b7b3d8461cf98b07d2552ce9199702fa3ad010ac2671a5f8cd5136f7534e50f6855ba7bbb02b11494563f1d04156276a31cda1103b900e5eecd0d9473dc7b9c5', 'sha512')
        dotnet.parent.mkdir(parents=True, exist_ok=True)
        with tarfile.open(archive) as source:
            source.extractall(dotnet.parent, filter='data')

print('Pinned .NET Linux SDK installed and verified. No SQLCipher/restic native test dependencies required.')
PY
if ! test -x "$WI_TOOLS_ROOT/qa-venv/bin/python"; then
    python3 -m venv "$WI_TOOLS_ROOT/qa-venv"
fi
"$WI_TOOLS_ROOT/qa-venv/bin/python" -m pip install --disable-pip-version-check -r "$repo_root/scripts/qa-requirements.txt"
"$DOTNET_ROOT/dotnet" restore "$repo_root/WorkIntelligence.slnx" -p:UseAppHost=false -p:SelfContained=false -p:PublishSingleFile=false
"$DOTNET_ROOT/dotnet" restore "$repo_root/tests/Core/WorkIntelligence.Core.Tests.csproj"
