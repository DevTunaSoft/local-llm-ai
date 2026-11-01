#!/usr/bin/env bash
# Development dependencies only; does not create or publish a Windows executable.
set -euo pipefail
repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
export WI_TOOLS_ROOT="${WI_TOOLS_ROOT:-$(dirname -- "$repo_root")/.tools}"
export DOTNET_ROOT="$WI_TOOLS_ROOT/dotnet"
export DOTNET_CLI_HOME="$WI_TOOLS_ROOT/dotnet-home"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
for command_name in python3 gcc make tclsh; do
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
import bz2

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

    commit = '6404ee168a2ae53fd029f6e6775fb70a44f11535'  # official SQLCipher v4.12.0
    library = root / 'sqlcipher' / 'libsqlcipher.so'
    marker = library.parent / 'test-build.json'
    saved = json.loads(marker.read_text()) if marker.exists() else {}
    if not library.exists() or saved.get('source') != commit or saved.get('sha256') != hashlib.sha256(library.read_bytes()).hexdigest():
        archive = temp / 'sqlcipher.tar.gz'
        download('https://codeload.github.com/sqlcipher/sqlcipher/tar.gz/' + commit, archive,
                 'f320649b402fd345fe30166a7962411f383042814acf76d8946194a4642409c8')
        with tarfile.open(archive) as source:
            source.extractall(temp, filter='data')
        source_dir = temp / ('sqlcipher-' + commit)
        flags = '-O2 -fPIC -DSQLITE_HAS_CODEC -DSQLITE_EXTRA_INIT=sqlcipher_extra_init -DSQLITE_EXTRA_SHUTDOWN=sqlcipher_extra_shutdown'
        subprocess.run(['./configure', '--with-tempstore=yes', '--enable-fts5', '--disable-tcl',
                        'CFLAGS=' + flags, 'LDFLAGS=-lcrypto'], cwd=source_dir, check=True)
        subprocess.run(['make', '-j2', 'sqlite3.c'], cwd=source_dir, check=True)
        library.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run(['gcc', '-O2', '-fPIC', '-shared', '-D_GNU_SOURCE', '-include', 'stdint.h',
                        '-DSQLITE_HAS_CODEC', '-DSQLITE_TEMP_STORE=2', '-DSQLITE_THREADSAFE=1', '-DSQLITE_ENABLE_FTS5',
                        '-DSQLITE_EXTRA_INIT=sqlcipher_extra_init', '-DSQLITE_EXTRA_SHUTDOWN=sqlcipher_extra_shutdown',
                        'sqlite3.c', '-o', str(library), '-lcrypto', '-lpthread', '-ldl', '-lm'], cwd=source_dir, check=True)
        marker.write_text(json.dumps({'source': commit, 'sha256': hashlib.sha256(library.read_bytes()).hexdigest()}))

    restic = root / 'restic' / 'restic'
    expected_binary = 'a77b8f59af4da23256edf1f768bc363c224b259082da8f6cc7fc7913535626d3'
    if not restic.exists() or hashlib.sha256(restic.read_bytes()).hexdigest() != expected_binary:
        archive = temp / 'restic.bz2'
        download('https://github.com/restic/restic/releases/download/v0.18.0/restic_0.18.0_linux_amd64.bz2', archive,
                 '98f6dd8bf5b59058d04bfd8dab58e196cc2a680666ccee90275a3b722374438e')
        binary = bz2.decompress(archive.read_bytes())
        if hashlib.sha256(binary).hexdigest() != expected_binary:
            raise RuntimeError('Decompressed restic binary checksum mismatch')
        restic.parent.mkdir(parents=True, exist_ok=True)
        restic.write_bytes(binary)
        restic.chmod(0o755)
print('Pinned Linux test tools installed and verified. These are not Windows release assets.')
PY
if ! test -x "$WI_TOOLS_ROOT/qa-venv/bin/python"; then
    python3 -m venv "$WI_TOOLS_ROOT/qa-venv"
fi
"$WI_TOOLS_ROOT/qa-venv/bin/python" -m pip install --disable-pip-version-check -r "$repo_root/scripts/qa-requirements.txt"
"$DOTNET_ROOT/dotnet" restore "$repo_root/WorkIntelligence.slnx" -p:UseAppHost=false -p:SelfContained=false -p:PublishSingleFile=false
"$DOTNET_ROOT/dotnet" restore "$repo_root/tests/Core/WorkIntelligence.Core.Tests.csproj"
