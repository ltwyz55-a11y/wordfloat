# 浮词 Mac App Store 提交准备

当前状态：尚未上传、未提交审核、未上架。2026-11-22 检查钥匙串得到 0 个有效签名身份。Xcode 26.5 已安装；通用二进制已编译为 arm64 + x86_64。原有本机浮词与 Windows 发行包不受此准备流程影响。

## 已完成

- `WordFloat.entitlements`：App Sandbox，仅允许用户主动选择的文件只读访问。壁纸随后复制到自身容器，无常驻外部文件访问、无需网络客户端或录音权限。
- `PrivacyInfo.xcprivacy`：无追踪、无主动收集数据。UserDefaults 用于本机学习状态；文件元数据用于容器文件和用户选择的图片；启动时间 API 仅为诊断中的事件时间与等待测量。
- `zh-Hans/metadata.json`：中文名称、简介、关键词、说明和审核操作说明草稿。未猜填卖家、联系信息、年龄问卷、价格、地区或授权。
- `privacy-policy.html`：待填写开发者联系方式并部署到公开 HTTPS 网址的草稿。应用菜单另提供离线隐私说明和词库许可。
- 三张原生界面截图：`outputs/AppStore/Screenshots/`，2560×1600（16:10）。最终词库范围确认后需重新检查截图与实际提交版本一致。
- `Scripts/package_appstore.py`：生成本地通用 `.xcarchive`，显式选择是否包含已授权的用户 PDF 词本。当前尚未执行正式归档，等待词库授权范围和注册 Bundle ID 确认。

## 完成提交还需要

1. 付费 Apple Developer 账号，在 Xcode → Settings → Accounts 登录；由账号持有人完成协议、身份或商务信息。不要在聊天中发送密码、验证码或私钥。
2. 确认公开分发的词库范围。用户 PDF 来源仅记录本地导入，不能以本机导入记录代替公开分发授权。原本机词本不删除。
3. 在开发者账户注册 Bundle ID，并在 App Store Connect 建立 macOS 应用记录；确认正式名称可用。发布身份、团队和 Bundle ID 不由脚本虚构。
4. 准备 Mac App Distribution / Apple Distribution 应用签名证书、Mac Installer Distribution 安装包证书和匹配的 Mac App Store provisioning profile。现有临时签名不能提交。
5. 提供公开可访问的支持网址、隐私政策网址和审核联系人；确认定价与分发地区，按最终词典／例句内容如实填写年龄问卷及内容权利信息。

## 归档与提交

先构建通用程序（不改动全局 Xcode 选择）：

```sh
sh Scripts/swift.sh build -c release --arch arm64 --arch x86_64 --scratch-path .build/appstore
```

获得实际注册的 Bundle ID 后，选择其中一种内容策略：

```sh
/usr/bin/python3 Scripts/package_appstore.py --bundle-id "$APPSTORE_BUNDLE_ID" --content-policy exclude-import
# 仅在公开分发授权已确认时，才改用 include-authorized-import。
```

脚本保存 `Preparation.json`，明确 `uploadReady=false`。该归档仍是临时签名，不能直接上传；正式发布需嵌入匹配的 provisioning profile，并使用团队实际身份签名。App Store entitlement 还需包含该 profile 的实际 application-identifier 和 team-identifier，不能用占位值。

签名完成后，使用 Apple 的 `productbuild --component <应用路径> /Applications --sign <实际安装包证书> <输出.pkg>` 构建安装包，再通过 Transporter / App Store Connect 上传。服务器处理通过后，选择构建、填写商店资料并提交审核。审核批准才可能上架；本地编译或上传成功均不代表审核通过。

沙盒版本仍需使用真实签名和 profile 在干净环境测试：发音、文件选择、壁纸复制、进度恢复、卡片窗口与退出。当前桌面诊断是非商店本地构建，不等同于发行签名沙盒验收。Apple 芯片已交叉编译，未在 Apple 芯片实机运行。

## Apple 官方依据

- [Mac App Store 规则与沙盒](https://developer.apple.com/app-store/review/guidelines/#hardware-compatibility)
- [分发准备](https://developer.apple.com/documentation/xcode/preparing-your-app-for-distribution)
- [隐私 API 原因声明](https://developer.apple.com/documentation/bundleresources/app-privacy-configuration/nsprivacyaccessedapitypes/nsprivacyaccessedapitype)
- [Mac 截图尺寸](https://developer.apple.com/help/app-store-connect/reference/app-information/screenshot-specifications/)
- [上传构建](https://developer.apple.com/help/app-store-connect/manage-builds/upload-builds/)
