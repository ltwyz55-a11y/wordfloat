#!/usr/bin/env python3
"""Verify the PE target and the contents of .NET's single-file bundle without running Windows.

Bundle layout: dotnet/runtime v10.0.0, Microsoft.NET.HostModel/Bundle/{Manifest,FileEntry}.cs.
This is artifact inspection, not a replacement for native Windows execution.
"""
import argparse, hashlib, json, os, pathlib, struct, zlib
ROOT = pathlib.Path(__file__).resolve().parents[1]
BUNDLE_MARKER = bytes.fromhex('8b1202b96a612038727b930214d7a03213f5b9e6efae3318ee3b2dce24b36aae')

def read_string(data, position):
    length=0
    for shift in range(0,35,7):
        value=data[position];position+=1;length|=(value&127)<<shift
        if value<128:break
    else:raise ValueError('Invalid .NET string length')
    end=position+length
    assert end<=len(data),'String outside bundle'
    return data[position:end].decode('utf-8'),end

def bundle(data):
    assert data[:2]==b'MZ','Not a Windows executable'
    pe=struct.unpack_from('<I',data,0x3c)[0]
    assert data[pe:pe+4]==b'PE\0\0','Invalid PE signature'
    assert struct.unpack_from('<H',data,pe+4)[0]==0x8664,'Expected Windows x64'
    optional=pe+24
    assert struct.unpack_from('<H',data,optional)[0]==0x20b,'Expected PE32+'
    assert struct.unpack_from('<H',data,optional+68)[0]==2,'Expected a Windows GUI app'
    marker=data.find(BUNDLE_MARKER);assert marker>=8,'Missing single-file bundle marker'
    position=struct.unpack_from('<Q',data,marker-8)[0]
    assert 0<position<len(data),'Invalid bundle header location'
    major,minor,count=struct.unpack_from('<IIi',data,position);position+=12
    assert (major,minor)==(6,0) and 1<count<10000,'Unsupported bundle manifest'
    bundle_id,position=read_string(data,position)
    position+=40  # Deps JSON offset/size, runtime config offset/size, flags.
    files={}
    for _ in range(count):
        offset,size,compressed=struct.unpack_from('<qqq',data,position);position+=24
        kind=data[position];position+=1
        name,position=read_string(data,position)
        assert name not in files and size>=0 and compressed>=0 and offset>=0
        stored=compressed or size;assert offset+stored<=len(data),'Entry outside bundle'
        files[name]=dict(offset=offset,size=size,compressed=compressed,kind=kind)
    return bundle_id,files

def extract(data,entry):
    length=entry['compressed'] or entry['size']
    content=data[entry['offset']:entry['offset']+length]
    if entry['compressed']:content=zlib.decompress(content,-15)
    assert len(content)==entry['size'],'Bundle entry size mismatch'
    return content

def sections(data):
    pe=struct.unpack_from('<I',data,0x3c)[0]
    count=struct.unpack_from('<H',data,pe+6)[0]
    optional=struct.unpack_from('<H',data,pe+20)[0]
    result={}
    for i in range(count):
        position=pe+24+optional+i*40
        name=data[position:position+8].rstrip(b'\0').decode('ascii')
        size,offset=struct.unpack_from('<II',data,position+16)
        result[name]=data[offset:offset+size]
    return result

def verify(path):
    data=pathlib.Path(path).read_bytes();ident,files=bundle(data)
    required={'WordFloat.dll','WordFloat.Core.dll','System.Speech.dll','PresentationFramework.dll','PresentationCore.dll','WindowsBase.dll','System.Windows.Forms.dll','System.Private.CoreLib.dll','wpfgfx_cor3.dll','WordFloat.runtimeconfig.json'}
    assert required<=files.keys(), 'Missing runtime or application files: '+str(required-files.keys())
    runtime=json.loads(extract(data,files['WordFloat.runtimeconfig.json']))['runtimeOptions']
    included={f['name']:f['version'] for f in runtime.get('includedFrameworks',[])}
    assert {'Microsoft.NETCore.App','Microsoft.WindowsDesktop.App'}<=included.keys(),'Not self-contained'
    assert 'framework' not in runtime and 'frameworks' not in runtime,'Unexpected external runtime requirement'
    core=extract(data,files['WordFloat.Core.dll'])
    vocabulary=(ROOT/'App/Core/Resources/vocabulary.sqlite').read_bytes()
    assert vocabulary in core,'Bundled SQLite differs from the validated source'
    # The .NET 10 Windows singlefilehost statically includes the native CLR and host policy.
    # Compare its machine-code section with the SDK template when that template is available.
    pe_sections=sections(data)
    host_verified=False
    caches=[ROOT/'work/nuget',pathlib.Path(os.environ.get('NUGET_PACKAGES',str(pathlib.Path.home()/'.nuget/packages')))]
    for cache in caches:
        host=cache/'microsoft.netcore.app.host.win-x64'/included['Microsoft.NETCore.App']/'runtimes/win-x64/native/singlefilehost.exe'
        if host.exists():
            assert pe_sections['.text']==sections(host.read_bytes())['.text'],'Native host differs from SDK template'
            host_verified=True;break
    for name in files:extract(data,files[name])
    return dict(status='passed',inspection='PE and .NET bundle; Windows runtime execution not performed',architecture='win-x64',bundleId=ident,embeddedFiles=len(files),frameworks=included,
        nativeHostMatchesSdk=host_verified,nativeCodeSha256=hashlib.sha256(pe_sections['.text']).hexdigest(),vocabularySha256=hashlib.sha256(vocabulary).hexdigest(),executableSha256=hashlib.sha256(data).hexdigest(),bytes=len(data))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('executable');parser.add_argument('--report')
    args=parser.parse_args();report=verify(args.executable)
    if args.report:pathlib.Path(args.report).write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(report,ensure_ascii=False))
