#!/usr/bin/env python3
"""Build and inspect the independent .NET Framework installer; no Windows execution implied."""
import hashlib,json,os,pathlib,struct,subprocess
ROOT=pathlib.Path(__file__).resolve().parents[1]
WORK=ROOT/'work/windows-setup'

def metadata(version,installed_bytes,payload=None):
    WORK.mkdir(parents=True,exist_ok=True)
    payload=pathlib.Path(payload) if payload else None
    result=dict(product='WordFloat',version=version,installedBytes=installed_bytes,payloadBytes=0,payloadSha256='')
    if payload:
        import zipfile
        with zipfile.ZipFile(payload) as archive:result['payloadBytes']=sum(entry.file_size for entry in archive.infolist())
        result['payloadSha256']=hashlib.sha256(payload.read_bytes()).hexdigest()
    (WORK/'metadata.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    return result

def build(embedded,version):
    output=WORK/('full' if embedded else 'host')
    if os.name=='nt':
        local=ROOT/'work/dotnet/dotnet.exe';command=[str(local) if local.exists() else 'dotnet']
    else:command=['sh',str(ROOT/'Scripts/dotnet.sh')]
    command+=['build',str(ROOT/'Windows/WordFloat.Setup'),'-c','Release',
        '-p:EmbedPayload='+str(embedded).lower(),'-p:Version='+version,'-p:OutputPath='+str(output)+os.sep,
        '-p:AppendTargetFrameworkToOutputPath=false','-p:DebugType=None','-p:DebugSymbols=false']
    if (ROOT/'Windows/WordFloat.Setup/obj/project.assets.json').exists():command+=['--no-restore']
    result=subprocess.run(command,cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    if result.returncode:raise RuntimeError(result.stdout)
    print('Compiled Windows setup ('+('embedded' if embedded else 'folder')+' payload).',flush=True)
    return output/'WordFloatSetup.exe'

def verify(executable,payload=None):
    data=pathlib.Path(executable).read_bytes();pe=struct.unpack_from('<I',data,0x3c)[0]
    assert data[:2]==b'MZ' and data[pe:pe+4]==b'PE\0\0','Invalid setup executable'
    assert struct.unpack_from('<H',data,pe+4)[0]==0x8664,'Installer must be Windows x64'
    optional=pe+24
    assert struct.unpack_from('<H',data,optional)[0]==0x20b
    assert struct.unpack_from('<H',data,optional+68)[0]==2,'Installer must use GUI subsystem'
    assert struct.unpack_from('<I',data,optional+112+14*8)[0]>0,'Missing CLR entry header'
    assert b'v4.0.30319' in data and b'.NETFramework,Version=v4.8' in data,'Expected .NET Framework 4.8 CLR image'
    meta=(WORK/'metadata.json').read_bytes();assert meta in data,'Missing setup metadata'
    if payload:
        content=pathlib.Path(payload).read_bytes();assert content in data,'Embedded application payload differs'
        assert hashlib.sha256(content).hexdigest()==json.loads(meta)['payloadSha256']
    else:assert len(data)<2*1024*1024,'Folder launcher unexpectedly embeds a large payload'
    return dict(status='passed',architecture='win-x64',bootstrap='.NET Framework 4.8',embeddedPayload=bool(payload),
        executableSha256=hashlib.sha256(data).hexdigest(),bytes=len(data),metadata=json.loads(meta),windowsExecution='not performed')
