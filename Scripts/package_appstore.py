#!/usr/bin/env python3
"""Prepare a local universal Mac App Store archive. Does not upload or publish."""
import argparse, datetime, hashlib, json, os, pathlib, plistlib, shutil, sqlite3, subprocess, tempfile
ROOT=pathlib.Path(__file__).resolve().parents[1]

def run(*args):
    return subprocess.check_output(args,cwd=ROOT,text=True).strip()

def filtered_database(source,target,include_import):
    with sqlite3.connect(source) as src,sqlite3.connect(target) as dst:
        src.backup(dst)
        if not include_import:
            dst.execute("DELETE FROM entries WHERE book_id='cambridge-listening-4-19'")
            dst.execute("DELETE FROM books WHERE id='cambridge-listening-4-19'")
            dst.execute('DELETE FROM learning_details WHERE word_id NOT IN (SELECT word_id FROM entries)')
            dst.execute('DELETE FROM words WHERE id NOT IN (SELECT word_id FROM entries)')
        dst.commit();dst.execute('VACUUM')
        assert dst.execute('PRAGMA integrity_check').fetchone()[0]=='ok'
        assert not dst.execute('PRAGMA foreign_key_check').fetchall()
        return dict(dst.execute('SELECT book_id,count(*) FROM entries GROUP BY book_id'))

def prepare(args):
    # Choice is explicit: the user's private import is never silently licensed for publication.
    include=args.content_policy=='include-authorized-import'
    executable=pathlib.Path(run('sh','Scripts/swift.sh','build','-c','release','--arch','arm64','--arch','x86_64','--scratch-path','.build/appstore','--show-bin-path'))/'WordFloat'
    assert executable.exists(),'Build the universal binary before preparing the archive'
    architectures=run('lipo','-archs',str(executable)).split();assert set(architectures)=={'arm64','x86_64'}
    destination=pathlib.Path(args.destination).resolve();destination.parent.mkdir(parents=True,exist_ok=True)
    if destination.exists():raise ValueError('Archive already exists; choose another destination')
    with tempfile.TemporaryDirectory(dir=ROOT/'work',prefix='appstore-stage.') as temp:
        archive=pathlib.Path(temp)/destination.name;app=archive/'Products/Applications/浮词.app';resources=app/'Contents/Resources'
        resources.mkdir(parents=True);(app/'Contents/MacOS').mkdir()
        shutil.copy2(executable,app/'Contents/MacOS/WordFloat')
        counts=filtered_database(ROOT/'App/Core/Resources/vocabulary.sqlite',resources/'vocabulary.sqlite',include)
        (resources/'counts.json').write_text(json.dumps(counts,ensure_ascii=False,indent=2)+'\n')
        notice=(ROOT/'DATA-NOTICE.md').read_text()
        if not include:notice=notice.split('## 用户导入：剑雅4')[0].rstrip()+'\n'
        (resources/'DATA-NOTICE.md').write_text(notice)
        shutil.copy2(ROOT/'Data/ECDICT-LICENSE.txt',resources/'ECDICT-LICENSE.txt')
        shutil.copy2(ROOT/'Distribution/AppStore/PrivacyInfo.xcprivacy',resources/'PrivacyInfo.xcprivacy')
        icon=ROOT/'outputs/浮词.app/Contents/Resources/AppIcon.icns';assert icon.exists(),'Build the regular application first to generate its icon'
        shutil.copy2(icon,resources/'AppIcon.icns')
        sdk=run('xcrun','--sdk','macosx','--show-sdk-version');sdkbuild=run('xcrun','--sdk','macosx','--show-sdk-build-version')
        xcode=run('xcodebuild','-version').splitlines();parts=xcode[0].split()[1].split('.')
        info=dict(CFBundleExecutable='WordFloat',CFBundleIdentifier=args.bundle_id,CFBundleName='浮词',CFBundleDisplayName='浮词',CFBundlePackageType='APPL',CFBundleShortVersionString='1.4.1',CFBundleVersion='10',CFBundleIconFile='AppIcon',LSMinimumSystemVersion='13.0',LSUIElement=True,NSHighResolutionCapable=True,CFBundleDevelopmentRegion='zh_CN',CFBundleLocalizations=['zh-Hans'],LSApplicationCategoryType='public.app-category.education',ITSAppUsesNonExemptEncryption=False,DTPlatformName='macosx',DTPlatformVersion=sdk,DTSDKName='macosx'+sdk,DTSDKBuild=sdkbuild,DTXcode=parts[0].zfill(2)+parts[1].zfill(1)+'0',DTXcodeBuild=xcode[1].split()[-1],BuildMachineOSBuild=run('sw_vers','-buildVersion'))
        (app/'Contents/Info.plist').write_bytes(plistlib.dumps(info))
        run('codesign','--force','--sign','-','--options','runtime','--entitlements',str(ROOT/'Distribution/AppStore/WordFloat.entitlements'),'--timestamp=none',str(app))
        run('codesign','--verify','--deep','--strict',str(app))
        archive_info=dict(ArchiveVersion=2,CreationDate=datetime.datetime.now(datetime.timezone.utc).replace(tzinfo=None),Name='浮词',SchemeName='WordFloat',ApplicationProperties=dict(ApplicationPath='Applications/浮词.app',CFBundleIdentifier=args.bundle_id,CFBundleShortVersionString='1.4.1',CFBundleVersion='10'))
        (archive/'Info.plist').write_bytes(plistlib.dumps(archive_info))
        report=dict(status='local-preparation-only',uploadReady=False,architectures=architectures,bundleId=args.bundle_id,contentPolicy=args.content_policy,books=counts,signature='ad-hoc; Apple distribution signing still required',sourceCommit=run('git','rev-parse','HEAD'),databaseSHA256=hashlib.sha256((resources/'vocabulary.sqlite').read_bytes()).hexdigest(),remaining=['Apple Developer team and distribution certificates','App Store Connect app record and registered bundle ID','Provisioning profile and signed installer package','Public support and privacy policy URLs','Store metadata, age rating and availability confirmation','App Review submission and approval'])
        (archive/'Preparation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
        shutil.move(str(archive),str(destination))
    print(json.dumps(report,ensure_ascii=False,indent=2))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--bundle-id',required=True);parser.add_argument('--content-policy',required=True,choices=['exclude-import','include-authorized-import']);parser.add_argument('--destination',default='outputs/AppStore/浮词.xcarchive');args=parser.parse_args()
    os.environ.setdefault('DEVELOPER_DIR','/Applications/Xcode.app/Contents/Developer')
    if not all(part and part.replace('-','').replace('_','').isalnum() for part in args.bundle_id.split('.')) or '.' not in args.bundle_id:parser.error('Use a registered reverse-DNS bundle identifier')
    prepare(args)
