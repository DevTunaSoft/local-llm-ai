#!/usr/bin/env python3
"""Reproduces actual SQL statements from C# against SQLite, with synthetic fixtures.
Does NOT substitute for SQLCipher, C# compilation, .NET runtime or Windows UI testing.
"""
from pathlib import Path
import re, sqlite3, tempfile, sys, subprocess, hashlib
root=Path(__file__).resolve().parent.parent
source=(root/'WorkIntelligence/Services/KnowledgeStore.cs').read_text()
work=(root/'WorkIntelligence/Services/KnowledgeStore.Work.cs').read_text()
ui=(root/'WorkIntelligence/MainWindow.xaml.cs').read_text()
checks=[]
def check(name, condition):
    checks.append(bool(condition));print(('PASS' if condition else 'FAIL')+' | '+name)
def extract(text,pattern):
    match=re.search(pattern,text,re.S)
    if not match: raise AssertionError('Missing source SQL: '+pattern)
    return match.group(1)
base=extract(source,r'cmd.CommandText = """(\s*PRAGMA journal_mode=DELETE;.*?)""";')
m4=extract(source,r'upgrade.CommandText = """(.*?)""";')
m5=extract(source,r'migration.CommandText = """(.*?)""";')
m6=extract(source,r'recallUpgrade.CommandText = """(.*?)""";')
m7=extract(source,r'workUpgrade.CommandText = """(.*?)""";')
with tempfile.TemporaryDirectory() as folder:
    c=sqlite3.connect(str(Path(folder)/'schema.db'))
    c.execute('PRAGMA foreign_keys=ON')
    c.executescript(base)
    c.execute("INSERT INTO documents(name,original_path,sha256,version,imported_at,active) VALUES('Legacy','legacy.txt','a',1,'now',1)")
    c.execute("INSERT INTO memories(content,source,created_at,approved) VALUES('Legacy procedure', 'fixture','now',1)")
    c.execute("INSERT INTO chats(title,created_at) VALUES('Legacy Chat','now')")
    c.execute("INSERT INTO memory_proposals(content,source,created_at) VALUES('Legacy proposal','fixture','now')")
    c.commit()
    # This conversation exists BEFORE the upgrade and must be backfilled into FTS.
    c.execute("INSERT INTO messages(chat_id,role,content,created_at) VALUES(1,'user','Legacy operation Specter','now')")
    c.commit()
    c.executescript(m4)
    check('V4 migration reaches correct version',c.execute('PRAGMA user_version').fetchone()[0]==4)
    c.executescript(m5)
    check('V5 migration reaches correct version',c.execute('PRAGMA user_version').fetchone()[0]==5)
    c.executescript(m6)
    check('V6 migration reaches correct version',c.execute('PRAGMA user_version').fetchone()[0]==6)
    c.executescript(m7)
    check('V7 migration reaches correct version',c.execute('PRAGMA user_version').fetchone()[0]==7)
    check('Legacy memories retained in General',c.execute('SELECT content,workspace_id FROM memories').fetchone()==('Legacy procedure',1))
    check('Legacy chats retained in General',c.execute('SELECT title,workspace_id FROM chats').fetchone()==('Legacy Chat',1))
    c.execute("INSERT INTO chats(title,created_at,workspace_id) VALUES('Other chat','now',1)")
    current_chat=c.execute('SELECT last_insert_rowid()').fetchone()[0]
    recall=extract(source,r'public List<ConversationRecallRecord> RecallEarlierConversations\(.*?cmd.CommandText = """(.*?)""";')
    rows=c.execute(recall,{'q':'"specter"','w':1,'exclude':current_chat,'n':4}).fetchall()
    check('Pre-existing conversation messages backfilled into FTS',len(rows)==1 and rows[0][2]=='Legacy operation Specter')
    check('Recall excludes other workspaces',c.execute(recall,{'q':'"specter"','w':3,'exclude':current_chat,'n':4}).fetchall()==[])
    c.execute("INSERT INTO messages(chat_id,role,content,created_at) VALUES(1,'assistant','Specter assistant-generated claims','now')")
    check('Recall excludes AI-generated messages',len(c.execute(recall,{'q':'"specter"','w':1,'exclude':current_chat,'n':4}).fetchall())==1)
    c.execute('DELETE FROM chats WHERE id=1')
    check('Deleting conversation cascades into recall index',c.execute(recall,{'q':'"specter"','w':1,'exclude':current_chat,'n':4}).fetchall()==[])
    check('Legacy proposals retained in General',c.execute('SELECT content,workspace_id FROM memory_proposals').fetchone()==('Legacy proposal',1))
    check('Unassigned documents linked into General',c.execute('SELECT workspace_id,document_id FROM workspace_documents').fetchone()==(1,1))
    c.execute("INSERT INTO workspaces(id,name) VALUES(2,'Operations'),(3,'Leadership')")
    c.execute("INSERT INTO documents(name,original_path,sha256,version,imported_at,active) VALUES('Ops','ops.txt','b',1,'now',1)")
    c.execute("INSERT INTO chunks(document_id,ordinal,content) VALUES(2,0,'Synthetic rotary dispatch manual')")
    c.execute('INSERT INTO workspace_documents(workspace_id,document_id) VALUES(2,2)')
    c.execute("INSERT INTO chunk_vectors(chunk_id,model_key,dims,vector) VALUES(1,'synthetic-embedding',16,?)",(bytes(64),))
    semantic=extract(source,r'public async Task<List<ChunkHit>> SearchHybridAsync\(.*?cmd.CommandText="""(.*?)""";')
    arguments={'model':'synthetic-embedding','dims':16,'workspace':2}
    check('Real C# semantic query retrieves assigned vectors',len(c.execute(semantic,arguments).fetchall())==1)
    arguments['workspace']=3
    check('Real C# semantic query excludes other workspace vectors',c.execute(semantic,arguments).fetchall()==[])
    c.execute("INSERT INTO memories(content,source,created_at,approved,workspace_id) VALUES('Rotary dispatch target 100','fixture','now',1,2)")
    original=c.execute('SELECT last_insert_rowid()').fetchone()[0]
    # Execute WHERE predicates copied verbatim from the production search SQL.
    search=extract(source,r'public List<ChunkHit> Search\(.*?cmd.CommandText = """(.*?)""";')
    rows=c.execute(search,{'query':'"rotary"','workspace':2,'limit':10}).fetchall()
    check('Real C# lexical query returns assigned document',len(rows)==1 and rows[0][1]=='Ops')
    check('Real C# lexical query excludes other workspaces',c.execute(search,{'query':'"rotary"','workspace':3,'limit':10}).fetchall()==[])
    check('Real C# lexical query supports explicit all-scope legacy API',len(c.execute(search,{'query':'"rotary"','workspace':None,'limit':10}).fetchall())==1)
    memory_search=extract(source,r'public List<MemoryRecord> SearchMemories\(.*?cmd.CommandText = """(.*?)""";')
    assert 'memories_fts' in memory_search
    query_args={'q':'"rotary"','n':10,'w':2}
    check('Approved FTS memory retrieved within workspace',len(c.execute(memory_search,query_args).fetchall())==1)
    query_args['w']=3
    check('Approved FTS memory excluded from other workspace',c.execute(memory_search,query_args).fetchall()==[])
    c.execute("INSERT INTO memories(content,source,created_at,approved,workspace_id) VALUES('Rotary dispatch target 200','fixture','now',1,2)")
    replacement=c.execute('SELECT last_insert_rowid()').fetchone()[0]
    c.execute('UPDATE memories SET approved=0,superseded_by=? WHERE id=?',(replacement,original))
    query_args['w']=2
    hits=c.execute(memory_search,query_args).fetchall()
    check('Superseded fact excluded after approved correction',len(hits)==1 and hits[0][0]==replacement)
    check('Supersession lineage persisted',c.execute('SELECT superseded_by FROM memories WHERE id=?',(original,)).fetchone()[0]==replacement)
    c.execute('DELETE FROM memories WHERE id=?',(replacement,))
    check('Deleting replacement preserves older historical record',c.execute('SELECT id,approved,superseded_by FROM memories WHERE id=?',(original,)).fetchone()==(original,0,None))
    check('Deleting memory removes its FTS hits',c.execute(memory_search,query_args).fetchall()==[])
    # Verify a newly created schema can be re-opened without executing migrations a second time.
    c.close()
    with sqlite3.connect(str(Path(folder)/'schema.db')) as reopened:
        check('Reopen preserves V7 version',reopened.execute('PRAGMA user_version').fetchone()[0]==7)
check('C# memory proposals require explicit approval', 'ResolveMemoryProposal(long id,bool approve)' in work and 'if(approve)' in work)
check('C# memory replacement checks workspace', 'approved=1 AND workspace_id=$w' in work)
check('C# memory corrections remain atomic', 'using var tx=con.BeginTransaction()' in work and 'tx.Commit();' in work)
check('C# prompt boundaries and capped excerpts exist', 'BEGIN UNTRUSTED RETRIEVED EVIDENCE' in ui and 'Math.Min(1100,references[i].Content.Length)' in ui)
check('Model output cannot directly write memory', '_store.QueueMemoryIfNew(proposedText' in ui and '_runtime.StreamAsync' in ui)
check('Workspace list rebinding does not accidentally reset active chat', '_refreshingWorkspaceItems = true;' in ui and 'if (_busy || _refreshingWorkspaceItems) return;' in ui)
check('Repeated conversational learning is idempotent', 'QueueMemoryIfNew' in work and 'if (check.ExecuteScalar() is not null) return false;' in work)
check('Deleted watched documents retired after complete scan', 'RetireMissingWatchedDocuments(folder.Path, seen, ct)' in work and 'if (complete)' in work)
check('Older messages explicitly untrusted', 'BEGIN PRIOR CONVERSATION CONTEXT (unverified' in ui)
print('LEARNING SQL + SOURCE QA:',sum(checks),'/',len(checks))
sys.exit(0 if all(checks) else 1)
