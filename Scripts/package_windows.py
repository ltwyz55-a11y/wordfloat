#!/usr/bin/env python3
"""Package a verified, self-contained Windows installer without creating a remote release."""
import argparse, hashlib, json, pathlib, re, shutil, subprocess, tempfile, zipfile
from verify_windows import verify_folder
ROOT=pathlib.Path(__file__).resolve().parents[1]

def package(publish,destination):
    source=pathlib.Path(publish)
    report=verify_folder(source)
    version=re.search(r'Version = "([^"]+)"',(ROOT/'Windows/WordFloat.Core/AppInfo.cs').read_text(encoding='utf-8'))[1]
    destination=pathlib.Path(destination);destination.mkdir(parents=True,exist_ok=True)
    archive=destination/f'浮词-Windows-x64-{version}.zip'
    report['version']=version
    report['sourceCommit']=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
    with tempfile.TemporaryDirectory(prefix='windows-package.',dir=ROOT/'work') as temporary:
        stage=pathlib.Path(temporary)/f'浮词-Windows-{version}';stage.mkdir()
        shutil.copytree(source,stage/'app')
        (stage/'app/package.json').write_text(json.dumps(dict(product='WordFloat',version=version,architecture='win-x64',files=report['files']),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        for launcher in (ROOT/'Windows/Launcher').iterdir():
            if launcher.is_file():shutil.copy2(launcher,stage/launcher.name)
        for src,name in [('Windows/README.md','Windows使用说明.md'),('Windows/VALIDATION.md','Windows验收记录.md'),('DATA-NOTICE.md','词库来源.md')]:
            shutil.copy2(ROOT/src,stage/name);shutil.copy2(ROOT/src,destination/name)
        shutil.copytree(ROOT/'Windows/Licenses',stage/'第三方许可')
        shutil.copy2(ROOT/'Data/ECDICT-LICENSE.txt',stage/'第三方许可/ECDICT-LICENSE.txt')
        (stage/'EXE资源校验.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        (stage/'SHA256SUMS.txt').write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.relative_to(stage).as_posix()+'\n' for p in sorted(stage.rglob('*')) if p.is_file()),encoding='utf-8')
        with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
            for path in sorted(stage.rglob('*')):
                if path.is_file():z.write(path,path.relative_to(stage.parent))
        with zipfile.ZipFile(archive) as z:
            assert z.testzip() is None
            manifest=json.loads(z.read(stage.name+'/app/package.json'))
            for name,digest in manifest['files'].items():
                assert hashlib.sha256(z.read(stage.name+'/app/'+name)).hexdigest()==digest,name
            assert z.read(stage.name+'/WordFloat.ps1').startswith(b'\xef\xbb\xbf'),'Windows PowerShell requires UTF-8 BOM for Chinese strings'
            for name in ('Install.cmd','Start.cmd','SafeMode.cmd','Diagnose.cmd'):
                assert b'\r\n' in z.read(stage.name+'/'+name)
    (destination/'EXE资源校验.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    files=[archive.name,'Windows使用说明.md','Windows验收记录.md','词库来源.md','EXE资源校验.json']
    (destination/'SHA256SUMS.txt').write_text(''.join(hashlib.sha256((destination/n).read_bytes()).hexdigest()+'  '+n+'\n' for n in files),encoding='utf-8')
    print(json.dumps(dict(package=str(archive),bytes=archive.stat().st_size,sha256=hashlib.sha256(archive.read_bytes()).hexdigest(),nativeWindowsRun='not performed'),ensure_ascii=False))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--publish-dir',default=str(ROOT/'work/windows-folder'));parser.add_argument('--destination',default=str(ROOT/'outputs/Windows'))
    args=parser.parse_args();package(args.publish_dir,args.destination)
