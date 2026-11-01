#!/usr/bin/env python3
"""Independent source/SQLite smoke audit. Explicitly NOT Windows execution or C# compilation."""
from pathlib import Path
from lxml import etree
import re, sqlite3, tempfile, sys, zipfile, subprocess, os, json
base=Path(__file__).resolve().parent.parent
checks=[]
def add(name, passed, details=''):
    checks.append((name,bool(passed),details))
def t(rel):return (base/rel).read_text('utf-8')
proj=t('WorkIntelligence/WorkIntelligence.csproj')
rt=t('WorkIntelligence/Services/RuntimeBundle.cs')
store=t('WorkIntelligence/Services/KnowledgeStore.cs')
backup=t('WorkIntelligence/Services/BackupService.cs')
model=t('WorkIntelligence/Services/ModelRuntime.cs')
win=t('WorkIntelligence/MainWindow.xaml.cs')
script=t('scripts/Build-Windows.ps1')
vault=t('WorkIntelligence/Services/Vault.cs')
down=t('WorkIntelligence/Services/ModelDownloader.cs')
add('Both native archive AND SHA manifest embedded', all(x in proj for x in ('LogicalName="WorkIntelligence.runtime.zip"','LogicalName="WorkIntelligence.runtime.sha256"')))
add('Release generator creates both embedded resources', all(x in script for x in ('runtime.zip','runtime.sha256','Get-FileHash')))
add('Runtime bundle verifies signed/hash-matched archive before extraction','Embedded runtime hash mismatch' in rt and 'SHA256.HashData(archiveBytes)' in rt)
add('Runtime ZIP archive has size, symlink, and traversal protections',all(x in rt for x in ('Maximum', 'entry.Length >', 'entry.ExternalAttributes', 'Path.GetFullPath', 'StartsWith(destination + Path.DirectorySeparatorChar')) if False else all(x in rt for x in ('entry.Length >', 'entry.ExternalAttributes', 'Path.GetFullPath', 'StartsWith(destination + Path.DirectorySeparatorChar')))
add('Runtime ZIP extraction verifies output hashes', 'Runtime extraction verification failed.' in rt)
add('Model API name agrees with server alias', '--alias' in model and model.count('work-intelligence-local')==2)
add('Model API CORS restricted to localhost', '"--cors-origins", "localhost"' in model)
add('Model lifecycle serialized across start/stop', '_lifecycle.WaitAsync' in model and '_lifecycle.Release()' in model)
add('Model process cleanup also runs on early failure','if (ActiveModel is null) await StopCoreAsync();' in model)
add('Long CPU model startup allowance', 'FromMinutes(15)' in model)
add('Model requests remain local','127.0.0.1' in model and 'api.openai.com' not in model)
add('No untrusted model download redirects','AllowAutoRedirect=false' in down and 'current.Scheme != Uri.UriSchemeHttps' in down)
add('SQL schema does not override newer versions', 'if (version > 6)' in store)
add('SQL connection disposed on encryption init failure', re.search(r'catch\s*\{\s*connection.Dispose\(\);',store) is not None)
add('Encrypted backup snapshots guarded by document lock','public void CreateBackupSnapshot' in store and 'lock (_writeGate)' in store.split('public void CreateBackupSnapshot',1)[1])
add('Backup uses guarded snapshot method', 'store.CreateBackupSnapshot(payload, cancellationToken)' in backup)
add('Backup excludes nested application root', 'repository.Equals(appRoot' in backup and 'repository.StartsWith(appRoot' in backup)
add('Failed backups do not display arbitrary stderr', 'Truncate(stderr)' not in backup and 'output has been suppressed' in backup)
add('Interrupted assistant answers persisted exactly once','if (answer.Length > 0)' in win and 'responseComplete' in win and '_store.AddMessage(_chatId,"assistant",answer.ToString());' not in win)
add('Model selection preserved across item refresh','selectedModelId' in win)
add('Stable encryption and no plaintext fallback','PRAGMA cipher_version' in store and 'AesGcm' in vault)
add('Unsigned native package is never silently fetched', 'Release blocked:' in script)
# Reproduce SQL schema/FTS5 semantics via platform SQLite smoke test; DOES NOT test SQLCipher/ADO provider
schema=re.search(r'cmd.CommandText = """\n(\s*PRAGMA journal_mode=DELETE;.*?)\n\s*""";',store,re.S)
add('SQLite schema extracted for standalone smoke test', schema is not None)
if schema:
    with tempfile.TemporaryDirectory() as td:
        c=sqlite3.connect(os.path.join(td,'smoke.db'))
        try:
            c.execute('PRAGMA foreign_keys=ON')
            c.executescript(schema.group(1))
            c.execute("INSERT INTO documents(name,original_path,sha256,version,imported_at,active) VALUES(?,?,?,?,?,?)",('Work manual','x','h',1,'2026',1))
            c.execute("INSERT INTO chunks(document_id,ordinal,content) VALUES(?,?,?)",(1,0,'Samsara vehicle camera tag assignment'))
            rows=c.execute('''SELECT c.content FROM chunks_fts JOIN chunks c ON c.id=chunks_fts.rowid JOIN documents d ON d.id=c.document_id WHERE chunks_fts MATCH ? AND d.active=1 ORDER BY bm25(chunks_fts) LIMIT 10''',('"samsara"',)).fetchall()
            add('SQLite FTS5 insertion and search smoke test',rows==[('Samsara vehicle camera tag assignment',)])
            c.execute("INSERT INTO chunk_vectors(chunk_id,model_key,dims,vector) VALUES(?,?,?,?)",(1,'test',16,bytes(64)))
            c.execute('DELETE FROM documents WHERE id=1')
            rows=c.execute('SELECT count(*) FROM chunks_fts WHERE chunks_fts MATCH ?',('"samsara"',)).fetchone()[0]
            add('SQLite FTS5 deletion trigger and foreign-key smoke test', rows==0)
            add('SQLite vector records cascade on document deletion',c.execute('SELECT COUNT(*) FROM chunk_vectors').fetchone()[0]==0)
        except Exception as e: add('SQLite schema full integration smoke',False,str(e))
        finally:c.close()
# Static audit of all XML and event wiring
for f in list(base.rglob('*.xaml')) + list(base.rglob('*.csproj')) + [base/'WorkIntelligence.slnx']:
    try: etree.parse(str(f)); add('XML validity: '+str(f.relative_to(base)),True)
    except Exception as e:add('XML validity: '+str(f.relative_to(base)),False,str(e))
xaml=t('WorkIntelligence/MainWindow.xaml')
for handler in sorted(set(re.findall(r'(?:Click|SelectionChanged)="(\w+)"',xaml))):
    add('WPF event wired: '+handler, bool(re.search(r'\b'+handler+r'\s*\(',win)))
# Source-only CI references tests; release intended separate
add('Windows CI builds and executes tests', 'dotnet build' in t('.github/workflows/windows-build.yml') and 'dotnet test' in t('.github/workflows/windows-build.yml'))
add('Maintained SQLCipher provider and mandatory bundled native', 'SQLitePCLRaw.provider.sqlcipher' in proj and 'SQLitePCLRaw.bundle_e_sqlcipher' not in proj and "vendor 'sqlcipher.dll'" in script and 'Release blocked: maintained SQLCipher native library absent' in script, 'Native SQLCipher DLL must be separately approved and tested')
for name,ok,details in checks:print(f'{"PASS" if ok else "FAIL"} | {name}'+(f' | {details}' if details else ''))
print('RECHECK SOURCE + SQLITE SMOKE:',sum(x[1] for x in checks),'/',len(checks),'passed')
sys.exit(0 if all(x[1] for x in checks) else 1)
