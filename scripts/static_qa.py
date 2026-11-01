#!/usr/bin/env python3
"""Source-structure checks only. Not a substitute for dotnet build, Windows runtime, or security testing."""
from pathlib import Path
import re
import sys
from lxml import etree
import yaml

ROOT=Path(__file__).resolve().parent.parent
checks=[]
def check(name,ok,detail=''):
    checks.append((name, bool(ok),detail))

def txt(path):return (ROOT/path).read_text(encoding='utf-8')

for path in sorted(ROOT.rglob('*.xaml')):
    try: etree.parse(str(path)); ok=True
    except Exception as exc: ok=False; err=str(exc)
    check('Valid XAML XML: '+str(path.relative_to(ROOT)),ok, '' if ok else err)
for path in sorted(ROOT.rglob('*.csproj')):
    try: etree.parse(str(path)); ok=True
    except Exception as exc: ok=False; err=str(exc)
    check('Valid .csproj XML: '+str(path.relative_to(ROOT)),ok,'' if ok else err)
try: etree.parse(str(ROOT/'WorkIntelligence.slnx')); ok=True
except Exception as exc: ok=False;err=str(exc)
check('Valid .slnx XML',ok, '' if ok else err)

xaml=txt('WorkIntelligence/MainWindow.xaml')
code=txt('WorkIntelligence/MainWindow.xaml.cs')
events=re.findall(r'(?:Click|SelectionChanged)="([A-Za-z_][A-Za-z0-9_]*)"', xaml)
for method in sorted(set(events)):
    check('XAML event has implementation: '+method, bool(re.search(r'\b'+method+r'\s*\(',code)))
check('No duplicate x:Name attributes',len(re.findall(r'x:Name="([^"]+)"',xaml))==len(set(re.findall(r'x:Name="([^"]+)"',xaml))))

project=txt('WorkIntelligence/WorkIntelligence.csproj')
check('Self-contained win-x64 target','<RuntimeIdentifier>win-x64</RuntimeIdentifier>' in project and '<SelfContained>true</SelfContained>' in project)
check('WPF enabled','<UseWPF>true</UseWPF>' in project)
check('Single EXE packaging configured','<PublishSingleFile>true</PublishSingleFile>' in project)
check('Native library extraction configured','<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>' in project)
check('SQLCipher provider used, not default plaintext SQLite', 'Microsoft.Data.Sqlite.Core' in project and 'SQLitePCLRaw.provider.sqlcipher' in project and 'SQLitePCLRaw.bundle_e_sqlcipher' not in project and not re.search(r'<PackageReference Include="Microsoft.Data.Sqlite"',project))

vault=txt('WorkIntelligence/Services/Vault.cs')
store=txt('WorkIntelligence/Services/KnowledgeStore.cs')
runtime=txt('WorkIntelligence/Services/ModelRuntime.cs')
model_download=txt('WorkIntelligence/Services/ModelDownloader.cs')
backup=txt('WorkIntelligence/Services/BackupService.cs')
release=txt('scripts/Build-Windows.ps1')
check('Explicit encrypted database provider validation','PRAGMA cipher_version' in store)
check('Vault has authenticated encryption','AesGcm' in vault and 'CryptographicOperations.FixedTimeEquals' in vault)
check('No default public AI endpoint in source',all('api.openai.com' not in p.read_text(encoding='utf-8').lower() for p in (ROOT/'WorkIntelligence').rglob('*.cs')))
check('AI process explicitly binds loopback', '"--host", "127.0.0.1"' in runtime)
check('LLM API key uses documented environment variable','LLAMA_API_KEY' in runtime and 'LLAMA_ARG_API_KEY' not in runtime)
check('Local model health check','GetAsync("health"' in runtime)
check('Model file digest before load','SHA256.HashDataAsync' in runtime)
check('Model download requires checksum','expectedSha256' in model_download and 'SHA256.HashDataAsync' in model_download)
check('Model redirects restricted to HTTPS','AllowAutoRedirect=false' in model_download and 'current.Scheme != Uri.UriSchemeHttps' in model_download)
check('Model GGUF signature validated','"GGUF"u8' in store and '"GGUF"u8' in model_download)
check('Backup repository locally encrypted','RESTIC_PASSWORD' in backup and '"backup", payload' in backup)
check('Non-overwrite recovery gate','Restore requires an empty vault' in backup)
check('Release refuses missing native tools', 'llama-server.exe missing' in release and 'restic.exe missing' in release)
check('Release checks vendor checksums','SHA256SUMS.txt' in release and 'Get-FileHash' in release)
check('User information redacted from engine logs','content redacted' in runtime)
try: yaml.safe_load(txt('.github/workflows/windows-build.yml')); ok=True
except Exception as exc: ok=False; err=str(exc)
check('CI workflow YAML parses',ok,'' if ok else err)
check('Tests present with substantive assertions',txt('tests/Tests.cs').count('[Fact]')>=6)

for name, ok, detail in checks:
    print(f'{"PASS" if ok else "FAIL"} | {name}'+(f' | {detail}' if detail else ''))
print(f'SOURCE STRUCTURE CHECKS: {sum(ok for _,ok,_ in checks)}/{len(checks)} passed')
sys.exit(0 if all(ok for _,ok,_ in checks) else 1)
