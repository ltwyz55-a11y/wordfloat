#!/usr/bin/env python3
"""Exercise rejection of broken real Windows release folders without running Windows."""
import argparse
import json
import os
from pathlib import Path
import shutil
import tempfile
from verify_windows import verify_folder

def run(source):
    source=Path(source).resolve()
    good=verify_folder(source)
    print('PASS complete self-contained folder and all dependency assets')
    with tempfile.TemporaryDirectory(prefix='windows-folder-checks.') as temporary:
        folder=Path(temporary)/'app'
        shutil.copytree(source,folder,copy_function=os.link)
        def rejected(message):
            try:verify_folder(folder)
            except AssertionError:print('PASS '+message)
            else:raise AssertionError('Broken release accepted: '+message)
        native=folder/'coreclr.dll';native.unlink()
        rejected('missing native CLR rejected')
        os.link(source/native.name,native)
        config=folder/'WordFloat.runtimeconfig.json';config.unlink()
        runtime=json.loads((source/config.name).read_text(encoding='utf-8'))
        runtime['runtimeOptions']['framework']={'name':'Microsoft.NETCore.App','version':'10.0.13'}
        config.write_text(json.dumps(runtime),encoding='utf-8')
        rejected('unexpected external runtime requirement rejected')
        config.unlink();os.link(source/config.name,config)
        dependency=folder/'System.Security.Cryptography.dll';dependency.unlink()
        rejected('missing dependency outside the fixed required list rejected')
        os.link(source/dependency.relative_to(folder),dependency)
        core=folder/'WordFloat.Core.dll';core.unlink();core.write_bytes(b'broken assembly')
        rejected('missing embedded dictionary rejected')
    print('WINDOWS_FOLDER_CHECKS_PASSED: '+str(len(good['files']))+' release files inspected; no Windows execution')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('folder');run(parser.parse_args().folder)
