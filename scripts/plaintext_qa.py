#!/usr/bin/env python3
"""Source and SQLite integration checks for the explicit UNENCRYPTED storage design.
These checks never replace dotnet build, C# tests, or Windows runtime tests.
"""
from pathlib import Path
import re, sqlite3, tempfile, sys
from lxml import etree
import yaml

root = Path(__file__).resolve().parents[1]
checks=[]
def check(name, truth):
    checks.append(bool(truth))
    print(('PASS' if truth else 'FAIL')+' | '+name)
def read(path): return (root/path).read_text(encoding='utf-8')
for f in [*root.rglob('*.csproj'),*root.rglob('*.xaml'),root/'WorkIntelligence.slnx']:
    try: etree.parse(str(f)); check('Valid XML: '+str(f.relative_to(root)),True)
    except Exception as e:check('Valid XML: '+str(f.relative_to(root))+' '+str(e),False)
try:
    with open(root/'.github/workflows/windows-build.yml',encoding='utf-8') as f: yaml.safe_load(f)
    check('Valid workflow YAML',True)
except Exception:check('Valid workflow YAML',False)
project=read('WorkIntelligence/WorkIntelligence.csproj')
tests=read('tests/Core/WorkIntelligence.Core.Tests.csproj')
src=read('WorkIntelligence/Services/KnowledgeStore.cs')
back=read('WorkIntelligence/Services/BackupService.cs')
schedule=read('WorkIntelligence/Services/ScheduledBackup.cs')
ui=read('WorkIntelligence/MainWindow.xaml')
app=read('WorkIntelligence/App.xaml.cs')
check('Standard bundled SQLite provider', all('Microsoft.Data.Sqlite"' in s and 'provider.sqlcipher' not in s for s in (project,tests)))
check('No SQLCipher initialization remains', 'SQLite3Provider_sqlcipher' not in app and 'sqlcipher.dll' not in project)
check('KnowledgeStore does not set database passwords', 'Password =' not in src and 'PRAGMA cipher_version' not in src)
check('Imported attachments are raw bytes', 'file.Write(input)' in src and '.bin' in src and '_vault.Encrypt' not in src)
check('Legacy encrypted data is refused without overwriting', 'LegacyEncryptedDataGuard.AssertNoLegacyData' in read('WorkIntelligence/Services/LocalPaths.cs'))
check('No password controls for opening or backups', all(x not in ui for x in ('UnlockPassword','BackupPassword','ScheduleVaultPassword')))
check('Versioned plaintext snapshots with SHA256 validation',all(x in back for x in ('snapshot', 'SHA256.HashData', 'VerifySnapshot', 'manifest.json', 'File.Move' if False else 'Directory.Move')))
check('Backups do not call restic', 'restic.exe' not in back and 'RESTIC_PASSWORD' not in back)
check('Scheduled backups store no vault or backup passwords',all(x not in schedule for x in ('ProtectedData.Protect','VaultPassword','BackupPassword')))
check('Backups never silently overwrite existing database','File.Move(file, destination, false)' in back)
check('Remote sync includes downloaded-byte verification','"--download"' in read('WorkIntelligence/Services/RemoteBackupService.cs'))
# Execute exactly the SQL extracted from source to validate FTS5, workspaces and schema migrations.
base=re.search(r'cmd.CommandText = """(\s*PRAGMA journal_mode=DELETE;.*?)""";',src,re.S)
m4=re.search(r'upgrade.CommandText = """(.*?)""";',src,re.S)
m5=re.search(r'migration.CommandText = """(.*?)""";',src,re.S)
m6=re.search(r'recallUpgrade.CommandText = """(.*?)""";',src,re.S)
m7=re.search(r'workUpgrade.CommandText = """(.*?)""";',src,re.S)
check('SQL schemas/migrations found in source',all((base,m4,m5,m6,m7)))
if all((base,m4,m5,m6,m7)):
  with tempfile.TemporaryDirectory() as d:
    path=Path(d)/'knowledge.db'
    db=sqlite3.connect(path)
    db.execute('PRAGMA foreign_keys=ON')
    for m in (base,m4,m5,m6,m7):db.executescript(m.group(1))
    check('Schema reaches V7',db.execute('PRAGMA user_version').fetchone()[0]==7)
    db.execute("INSERT INTO documents(name,original_path,sha256,version,imported_at,active) VALUES('source','/tmp/s','h',1,'now',1)")
    db.execute("INSERT INTO chunks(document_id,ordinal,content) VALUES(1,0,'Synthetic policy dispatch')")
    check('FTS5 searches imported document',db.execute('SELECT count(*) FROM chunks_fts WHERE chunks_fts MATCH ?',('"dispatch"',)).fetchone()[0]==1)
    db.execute("INSERT INTO chunk_vectors(chunk_id,model_key,dims,vector) VALUES(1,'test',16,?)",(bytes(64),))
    db.execute('DELETE FROM documents WHERE id=1')
    check('Deletion cascades to vector records',db.execute('SELECT count(*) FROM chunk_vectors').fetchone()[0]==0)
    db.close()
    check('Database header is openly readable',path.read_bytes().startswith(b'SQLite format 3\x00'))
print('PLAINTEXT SOURCE/SQLITE CHECKS:',sum(checks),'/',len(checks))
sys.exit(0 if all(checks) else 1)
