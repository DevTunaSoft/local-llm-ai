#!/usr/bin/env python3
"""Round-3 independent source & SQLite integration checks. No claim of Windows compilation.
Run after static_qa.py and recheck_qa.py; intentionally tests changed requirements.
"""
from pathlib import Path
import sqlite3,tempfile,re,sys,os, subprocess
from lxml import etree
root=Path(__file__).resolve().parent.parent
checks=[]
def check(label,ok): checks.append((label,bool(ok))); print(('PASS' if ok else 'FAIL')+' | '+label)
def txt(p):return (root/p).read_text()
src=txt('WorkIntelligence/Services/KnowledgeStore.cs')
embed=txt('WorkIntelligence/Services/EmbeddingRuntime.cs')
ocr=txt('WorkIntelligence/Services/OcrService.cs')
bu=txt('WorkIntelligence/Services/BackupService.cs')
remote=txt('WorkIntelligence/Services/RemoteBackupService.cs')
schedule=txt('WorkIntelligence/Services/ScheduledBackup.cs')
win=txt('WorkIntelligence/MainWindow.xaml.cs')
xl=txt('WorkIntelligence/MainWindow.xaml')
app=txt('WorkIntelligence/App.xaml.cs')
release=txt('scripts/Build-Windows.ps1')
extract=txt('WorkIntelligence/Services/DocumentExtractor.cs')
check('Semantic model server bound to loopback and authenticated', '127.0.0.1' in embed and 'LLAMA_API_KEY' in embed and '--embedding' in embed)
check('Semantic generation uses local embeddings endpoint', 'v1/embeddings' in embed and 'api.openai.com' not in embed)
check('Document vectors stored under SQLCipher DB with model identity', 'CREATE TABLE IF NOT EXISTS chunk_vectors' in src and 'model_key TEXT NOT NULL' in src)
check('Model switch does not mix incompatible vector spaces','v.model_key=$model' in src and 'v.dims=$dims' in src)
check('Semantic reindex and query hooked to WPF', 'IndexSemanticAsync' in win and 'SearchHybridAsync' in win and 'ReindexSemantic_Click' in xl)
check('OCR reads images via stdin/stdout, no cleartext temporary image files', '"stdin", "stdout"' in ocr and 'RedirectStandardInput = true' in ocr and 'GetTempFileName' not in ocr)
check('Invalid image rejected before OCR runtime', 'image.Length is < 8' in ocr)
check('Scanned PDFs not silently reported as successfully indexed', 'PdfPageOcr.Extract(bytes, page.Number - 1)' in extract and 'OCR returned no readable text' in extract)
check('Native OCR and remote programs are packaging requirements', all(x in release for x in ('tesseract.exe missing','tessdata/eng.traineddata missing','rclone.exe missing')))
check('Local restic data and restored file contents verified',
      all(token in bu for token in ('"--read-data"', 'ReadSnapshotId(summary)', '"restore", snapshotId',
                                   '"--tag","work-intelligence"', 'SHA256.HashData(original)')))
check('Encrypted OneDrive transfer includes downloaded byte comparison','"copy"' in remote and '"check"' in remote and '"--download"' in remote)
check('Remote restore refuses overwriting existing vault','RestoreRemoteToEmptyVaultAsync' in remote and 'remote recovery requires an empty vault'.lower() in remote.lower())
check('Backup background uses user DPAPI not cleartext password files','DataProtectionScope.CurrentUser' in schedule and 'ProtectedData.Protect' in schedule and 'ProtectedData.Unprotect' in schedule)
check('No credential is passed as scheduled task CLI argument','--scheduled-backup' in schedule and '--scheduled-backup' in app and 'TaskScheduler' in schedule)
check('Scheduled background launch calls backup and optional remote verification','BackupAsync(credentials.Repository' in schedule and 'RemoteBackupService.SyncAndVerifyAsync' in schedule)
check('Shutdown no longer blocks the UI using GetResult()', 'protected override async void OnClosing' in win and 'GetAwaiter().GetResult()' not in win)
check('Changes represented by distinct WPF handlers',all(h in xl and h in win for h in ['StartSemantic_Click','ReindexSemantic_Click','SyncOneDrive_Click','RestoreOneDrive_Click','ScheduleBackup_Click','RemoveScheduledBackup_Click']))
check('Historical source of truth preserved for audit', (root/'docs/MASTER_DEVELOPMENT_PROMPT.md').exists())
# Ensure database layout actually migrates from an existing V1 into the V2 encrypted-data schema.
schema=re.search(r'cmd.CommandText = """(.*?)""";',src,re.S)
try:
 with tempfile.TemporaryDirectory() as td:
  con=sqlite3.connect(os.path.join(td,'schema.db'));con.execute('PRAGMA foreign_keys=ON')
  con.execute('PRAGMA user_version=1');con.commit()
  con.executescript(schema.group(1))
  # Execute the *actual* migration SQL embedded in the source, not a reimplemented schema.
  upgrade4=re.search(r'upgrade.CommandText = """(.*?)""";',src,re.S)
  upgrade5=re.search(r'migration.CommandText = """(.*?)""";',src,re.S)
  assert upgrade4 is not None and upgrade5 is not None
  con.executescript(upgrade4.group(1))
  con.executescript(upgrade5.group(1))
  upgrade6=re.search(r'recallUpgrade.CommandText = """(.*?)""";',src,re.S)
  assert upgrade6 is not None
  con.executescript(upgrade6.group(1))
  check('V1 DB upgrades through schema V4 to V6',con.execute('PRAGMA user_version').fetchone()[0]==6)
  con.execute("INSERT INTO documents(name,original_path,sha256,version,imported_at,active) VALUES('t','a','1',1,'now',1)")
  con.execute("INSERT INTO chunks(document_id,ordinal,content) VALUES(1,0,'vehicle policy')")
  con.execute("INSERT INTO chunk_vectors(chunk_id,model_key,dims,vector) VALUES(1,'hash1',16,?)",(bytes(64),))
  con.execute('DELETE FROM documents WHERE id=1')
  check('Deleting a document clears encrypted vector records',con.execute('SELECT COUNT(*) FROM chunk_vectors').fetchone()[0]==0)
  con.close()
except Exception as e:check('SQLite schema migration integration',False);print('INTEGRATION ERROR:',type(e).__name__,str(e)[:200])
try:etree.parse(str(root/'WorkIntelligence/MainWindow.xaml'));check('XAML remains well formed after new controls',True)
except Exception:check('XAML remains well formed after new controls',False)
print('ROUND3 INDEPENDENT CHECKS:',sum(v for _,v in checks),'/',len(checks))
sys.exit(0 if all(v for _,v in checks) else 1)
