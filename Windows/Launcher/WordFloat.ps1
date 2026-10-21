param(
    [ValidateSet('Install','InstallWorker','Launch','SafeLaunch','Diagnose','Uninstall')]
    [string]$Mode = 'Launch',
    [switch]$NoDesktopShortcut
)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$title = '浮词'
$dataDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'WordFloat'
$installDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\WordFloat'
$registryPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\WordFloat'
$powershell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'

function Show-Message([string]$text, [bool]$failure = $false) {
    Write-Host $text
    try {
        Add-Type -AssemblyName System.Windows.Forms
        $icon = if ($failure) { [System.Windows.Forms.MessageBoxIcon]::Error } else { [System.Windows.Forms.MessageBoxIcon]::Information }
        [void][System.Windows.Forms.MessageBox]::Show($text, $title, [System.Windows.Forms.MessageBoxButtons]::OK, $icon)
    } catch { Write-Host 'Please copy the message above if the dialog is unavailable.' }
}

function Get-PayloadPath([string]$root, [string]$relative) {
    if ([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|[\\/])\.\.([\\/]|$)' -or $relative.Contains(':')) {
        throw "Invalid package path: $relative"
    }
    return Join-Path $root $relative
}

function Test-Payload([string]$root, [scriptblock]$OnFile = $null) {
    $manifest = Get-Content -LiteralPath (Join-Path $root 'package.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($manifest.product -ne 'WordFloat' -or $manifest.architecture -ne 'win-x64') { throw 'Invalid WordFloat package.' }
    $files = @($manifest.files.PSObject.Properties); $checked = 0
    foreach ($file in $files) {
        $path = Get-PayloadPath $root $file.Name
        if (!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.Value) {
            throw "文件缺失或损坏，请完整解压后重试：$($file.Name)"
        }
        $checked++
        if ($null -ne $OnFile -and ($checked % 16 -eq 0 -or $checked -eq $files.Count)) { & $OnFile $checked $files.Count | Out-Null }
    }
    foreach ($required in @('WordFloat.exe','WordFloat.dll','coreclr.dll','hostfxr.dll','PresentationFramework.dll')) {
        if ($null -eq $manifest.files.PSObject.Properties[$required]) { throw "Missing package entry: $required" }
    }
    return $manifest
}

function Stop-WordFloat {
    $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $mutex = [Threading.Mutex]::new($false, ('Local\WordFloat.' + $sid))
    $acquired = $false
    try {
        try { $acquired = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $acquired = $true }
        if (!$acquired) {
            $pipe = [IO.Pipes.NamedPipeClientStream]::new('.', ('WordFloat.' + $sid), [IO.Pipes.PipeDirection]::Out)
            try {
                $pipe.Connect(1800)
                $writer = [IO.StreamWriter]::new($pipe)
                try { $writer.WriteLine('quit'); $writer.Flush() } finally { $writer.Dispose() }
            } finally { $pipe.Dispose() }
            try { $acquired = $mutex.WaitOne(15000) } catch [Threading.AbandonedMutexException] { $acquired = $true }
            if (!$acquired) { throw '请先从托盘退出浮词，再重新安装或卸载。' }
        }
    } finally { if ($acquired) { $mutex.ReleaseMutex() }; $mutex.Dispose() }
}

function Remove-ManagedPayload([string]$root) {
    if (!(Test-Path -LiteralPath (Join-Path $root 'package.json'))) { return }
    $manifest = Get-Content -LiteralPath (Join-Path $root 'package.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($manifest.product -ne 'WordFloat') { return }
    foreach ($file in $manifest.files.PSObject.Properties) {
        $path = Get-PayloadPath $root $file.Name
        if (Test-Path -LiteralPath $path -PathType Leaf) { Remove-Item -LiteralPath $path -Force }
    }
    Remove-Item -LiteralPath (Join-Path $root 'package.json') -Force
    Get-ChildItem -LiteralPath $root -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending | ForEach-Object {
        if (@(Get-ChildItem -LiteralPath $_.FullName -Force).Count -eq 0) { Remove-Item -LiteralPath $_.FullName }
    }
    if (@(Get-ChildItem -LiteralPath $root -Force).Count -eq 0) { Remove-Item -LiteralPath $root }
}

function New-AppShortcut([string]$path, [string]$action) {
    $shell = New-Object -ComObject WScript.Shell
    try {
        $link = $shell.CreateShortcut($path)
        try {
            $link.TargetPath = $powershell
            $link.Arguments = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + (Join-Path $installDirectory 'WordFloat.ps1') + '" -Mode ' + $action
            $link.WorkingDirectory = $installDirectory
            $link.IconLocation = (Join-Path $installDirectory 'app\WordFloat.exe') + ',0'
            $link.Description = $title
            $link.Save()
        } finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($link) }
    } finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
}

function Move-Payload([string]$source, [string]$destination) {
    # The previous process can release its mutex just before Windows unloads its images.
    for ($attempt=0; ; $attempt++) {
        try { Move-Item -LiteralPath $source -Destination $destination; return }
        catch { if ($attempt -ge 20) { throw }; Start-Sleep -Milliseconds 200 }
    }
}

function Write-InstallEvent([hashtable]$event) {
    [Console]::Out.WriteLine('WORDFLOAT_SETUP:' + ($event | ConvertTo-Json -Compress -Depth 5))
    [Console]::Out.Flush()
}

function Report-InstallProgress([scriptblock]$callback, [int]$percent, [string]$message) {
    if ($null -ne $callback) { & $callback $percent $message | Out-Null }
}

function Get-LauncherFiles([string]$root) {
    @('WordFloat.ps1','Install.cmd','Start.cmd','SafeMode.cmd','Diagnose.cmd')
    if (Test-Path -LiteralPath (Join-Path $root 'WordFloatSetup.exe')) { 'WordFloatSetup.exe' }
}

function Install-WordFloat([bool]$CreateDesktopShortcut = $true, [scriptblock]$Progress = $null, [string]$SourceRoot = $PSScriptRoot) {
    $source = Join-Path $SourceRoot 'app'
    Report-InstallProgress $Progress 0 '正在校验安装文件…'
    $manifest = Test-Payload $source { param($done,$total) Report-InstallProgress $Progress ([int](15*$done/$total)) '正在校验安装文件…' }
    $launcherFiles = @(Get-LauncherFiles $SourceRoot)
    foreach ($name in $launcherFiles) {
        if (!(Test-Path -LiteralPath (Join-Path $SourceRoot $name) -PathType Leaf)) { throw "缺少启动文件：$name，请重新下载完整安装包。" }
    }
    Report-InstallProgress $Progress 16 '正在关闭旧版浮词…'
    Stop-WordFloat
    if ([IO.Path]::GetFullPath($SourceRoot).TrimEnd('\') -ne [IO.Path]::GetFullPath($installDirectory).TrimEnd('\')) {
        [void][IO.Directory]::CreateDirectory($installDirectory)
        $incoming = Join-Path $installDirectory ('.incoming-' + [Guid]::NewGuid().ToString('N'))
        $backup = Join-Path $installDirectory ('.previous-' + [Guid]::NewGuid().ToString('N'))
        $target = Join-Path $installDirectory 'app'
        [void][IO.Directory]::CreateDirectory($incoming)
        try {
            $files = @($manifest.files.PSObject.Properties); $copied = 0
            foreach ($file in $files) {
                $destination = Get-PayloadPath $incoming $file.Name
                [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
                Copy-Item -LiteralPath (Get-PayloadPath $source $file.Name) -Destination $destination
                $copied++
                if ($copied % 16 -eq 0 -or $copied -eq $files.Count) { Report-InstallProgress $Progress (20+[int](45*$copied/$files.Count)) '正在安装程序与离线词库…' }
            }
            Copy-Item -LiteralPath (Join-Path $source 'package.json') -Destination $incoming
            $null = Test-Payload $incoming { param($done,$total) Report-InstallProgress $Progress (65+[int](20*$done/$total)) '正在验证已复制的文件…' }
            if (Test-Path -LiteralPath $target) { Move-Payload $target $backup }
            try { Move-Payload $incoming $target }
            catch { if (Test-Path -LiteralPath $backup) { Move-Payload $backup $target }; throw }
            if (Test-Path -LiteralPath $backup) { Remove-ManagedPayload $backup }
            foreach ($name in $launcherFiles) {
                Copy-Item -LiteralPath (Join-Path $SourceRoot $name) -Destination (Join-Path $installDirectory $name) -Force
            }
        } finally { if (Test-Path -LiteralPath $incoming) { Remove-Item -LiteralPath $incoming -Recurse -Force } }
    }
    Report-InstallProgress $Progress 90 '正在创建启动入口…'
    $registration = Register-WordFloat $manifest.version $CreateDesktopShortcut
    # Remove only the known previous single-file executable after its process has stopped.
    $legacy = Join-Path $installDirectory ($title + '.exe')
    if (Test-Path -LiteralPath $legacy) { Remove-Item -LiteralPath $legacy -Force }
    Report-InstallProgress $Progress 100 '安装完成'
    return [pscustomobject]@{ Version=$manifest.version; Directory=$installDirectory; DesktopShortcutCreated=$registration.DesktopShortcutCreated; Warnings=@($registration.Warnings) }
}

function Register-WordFloat([string]$version, [bool]$CreateDesktopShortcut) {
    $warnings = [Collections.Generic.List[string]]::new(); $desktopCreated = $false
    $menu = Join-Path ([Environment]::GetFolderPath('Programs')) $title
    [void][IO.Directory]::CreateDirectory($menu)
    try {
        New-AppShortcut (Join-Path $menu ($title + '.lnk')) 'Launch'
        if ($CreateDesktopShortcut) { New-AppShortcut (Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) ($title + '.lnk')) 'Launch'; $desktopCreated = $true }
        New-AppShortcut (Join-Path $menu ('卸载' + $title + '.lnk')) 'Uninstall'
    } catch { $warnings.Add('部分快捷方式创建失败，可用安装文件夹中的 Start.cmd 打开。' + $_.Exception.Message) }
    [void](New-Item -Path $registryPath -Force)
    foreach ($entry in @{
        DisplayName=$title; DisplayVersion=$version; Publisher=$title; InstallLocation=$installDirectory
        DisplayIcon=((Join-Path $installDirectory 'app\WordFloat.exe') + ',0')
        UninstallString=('"' + $powershell + '" -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + (Join-Path $installDirectory 'WordFloat.ps1') + '" -Mode Uninstall')
    }.GetEnumerator()) { Set-ItemProperty -LiteralPath $registryPath -Name $entry.Key -Value $entry.Value }
    New-ItemProperty -LiteralPath $registryPath -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -LiteralPath $registryPath -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null
    return [pscustomobject]@{ DesktopShortcutCreated=$desktopCreated; Warnings=@($warnings.ToArray()) }
}

function Start-WordFloat([string]$root, [bool]$safe, [bool]$diagnose) {
    $payload = Join-Path $root 'app'
    $null = Test-Payload $payload
    $run = Join-Path $dataDirectory ('Logs\' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
    [void][IO.Directory]::CreateDirectory($run)
    $exe = Join-Path $payload 'WordFloat.exe'
    $info = [ordered]@{ version=(Get-Content -LiteralPath (Join-Path $payload 'package.json') -Raw -Encoding UTF8 | ConvertFrom-Json).version; os=[Environment]::OSVersion.VersionString; architecture=$env:PROCESSOR_ARCHITECTURE; safeMode=$safe; diagnostics=$diagnose; executable=$exe }
    $info | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'launch.json') -Encoding UTF8
    $oldTrace = $env:DOTNET_HOST_TRACE; $oldFile = $env:DOTNET_HOST_TRACEFILE
    try {
        $env:DOTNET_HOST_TRACE = '1'; $env:DOTNET_HOST_TRACEFILE = Join-Path $run 'host.log'
        $options = @{ FilePath=$exe; WorkingDirectory=$payload; PassThru=$true; RedirectStandardError=(Join-Path $run 'stderr.log'); RedirectStandardOutput=(Join-Path $run 'stdout.log') }
        $arguments = @()
        if ($safe) { $arguments += '--safe-mode' }
        if ($diagnose) { $arguments += @('--diagnostics', ('"' + (Join-Path $run 'checks') + '"')) }
        if ($arguments.Count) { $options.ArgumentList = $arguments }
        $process = Start-Process @options
    } finally { $env:DOTNET_HOST_TRACE=$oldTrace; $env:DOTNET_HOST_TRACEFILE=$oldFile }
    $info.processId = $process.Id
    $deadline = [DateTime]::UtcNow.AddSeconds($(if ($diagnose) { 60 } else { 15 }))
    $ready = $false
    do {
        Start-Sleep -Milliseconds 200
        $process.Refresh()
        if (!$diagnose) {
            $startup = Join-Path $dataDirectory 'startup.log'
            if (Test-Path -LiteralPath $startup) {
                $ready = @((Get-Content -LiteralPath $startup -Tail 80 -ErrorAction SilentlyContinue) | Where-Object { $_ -match ('\[' + $process.Id + '\] READY ') }).Count -gt 0
            }
        }
    } while (!$process.HasExited -and !$ready -and [DateTime]::UtcNow -lt $deadline)
    $info.ready = $ready; $info.exited = $process.HasExited
    if ($process.HasExited) { $info.exitCode = $process.ExitCode }
    $info | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'launch.json') -Encoding UTF8
    foreach ($name in @('startup.log','error.log')) {
        $path = Join-Path $dataDirectory $name
        if (Test-Path -LiteralPath $path) { Copy-Item -LiteralPath $path -Destination $run -Force }
    }
    if ($diagnose) {
        Show-Message ("诊断输出：`n" + $run + "`n请查看 checks\windows-checks.json；日志不包含其他应用截图。")
    } elseif ($process.HasExited -and $process.ExitCode -ne 0) {
        throw ("浮词退出，错误代码：" + $process.ExitCode + "`n启动日志：" + $run + "`n可再试 SafeMode.cmd（兼容启动）。")
    } elseif (!$ready -and !$process.HasExited) {
        Show-Message ("浮词尚未完成启动。`n诊断日志：" + $run + "`n请保留日志，并尝试 SafeMode.cmd。") $true
    }
}

function Uninstall-WordFloat {
    Add-Type -AssemblyName System.Windows.Forms
    if ([System.Windows.Forms.MessageBox]::Show('卸载浮词？学习进度和外观设置会保留。',$title,[System.Windows.Forms.MessageBoxButtons]::YesNo) -ne [System.Windows.Forms.DialogResult]::Yes) { return }
    Stop-WordFloat
    # A running PowerShell shortcut must not keep the installation directory locked.
    [Environment]::CurrentDirectory = [IO.Path]::GetTempPath()
    Set-Location -LiteralPath ([IO.Path]::GetTempPath())
    $menu = Join-Path ([Environment]::GetFolderPath('Programs')) $title
    foreach ($path in @((Join-Path $menu ($title+'.lnk')),(Join-Path $menu ('卸载'+$title+'.lnk')),(Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) ($title+'.lnk')))) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
    }
    if ((Test-Path -LiteralPath $menu) -and @(Get-ChildItem -LiteralPath $menu -Force).Count -eq 0) { Remove-Item -LiteralPath $menu }
    Remove-ManagedPayload (Join-Path $installDirectory 'app')
    foreach ($name in @('Install.cmd','Start.cmd','SafeMode.cmd','Diagnose.cmd','WordFloat.ps1','WordFloatSetup.exe')) {
        $path = Join-Path $installDirectory $name
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
    }
    if (Test-Path -LiteralPath $registryPath) { Remove-Item -LiteralPath $registryPath -Recurse }
    if ((Test-Path -LiteralPath $installDirectory) -and @(Get-ChildItem -LiteralPath $installDirectory -Force).Count -eq 0) { Remove-Item -LiteralPath $installDirectory }
    Show-Message '浮词已卸载。学习进度和外观设置已保留。'
}

try {
    if ($Mode -eq 'InstallWorker') { [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false) }
    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'This launcher requires Windows.' }
    # Windows PowerShell's manifest can virtualize Environment.OSVersion; read the real build.
    $windowsVersion = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
    if (![Environment]::Is64BitOperatingSystem -or [int]$windowsVersion.CurrentBuildNumber -lt 19045) {
        throw '需要 Windows 10 22H2 或更新的 64 位系统。'
    }
    switch ($Mode) {
        'Install' {
            $wizard = Join-Path $PSScriptRoot 'WordFloatSetup.exe'
            if (Test-Path -LiteralPath $wizard) { Start-Process -FilePath $wizard }
            else {
                $result = Install-WordFloat
                Show-Message ("浮词安装完成。`n可从桌面或开始菜单打开。`n`n" + $result.Directory + "`n" + ($result.Warnings -join "`n"))
                Start-WordFloat $installDirectory $false $false
            }
        }
        'InstallWorker' {
            $result = Install-WordFloat -CreateDesktopShortcut:(!$NoDesktopShortcut) -Progress { param($percent,$message) Write-InstallEvent @{kind='progress';percent=$percent;message=$message} }
            Write-InstallEvent @{kind='complete';version=$result.Version;directory=$result.Directory;desktopShortcutCreated=$result.DesktopShortcutCreated;warnings=@($result.Warnings)}
        }
        'Launch' { Start-WordFloat $PSScriptRoot $false $false }
        'SafeLaunch' { Start-WordFloat $PSScriptRoot $true $false }
        'Diagnose' { Start-WordFloat $PSScriptRoot $true $true }
        'Uninstall' { Uninstall-WordFloat }
    }
    exit 0
} catch {
    $message = $_.Exception.ToString() + "`n" + $_.ScriptStackTrace
    try {
        [void][IO.Directory]::CreateDirectory($dataDirectory)
        Add-Content -LiteralPath (Join-Path $dataDirectory 'launcher-error.log') -Value ((Get-Date -Format o) + "`n" + $message) -Encoding UTF8
    } catch { }
    if ($Mode -eq 'InstallWorker') { Write-InstallEvent @{kind='error';message=$message;log=(Join-Path $dataDirectory 'launcher-error.log')} }
    else { Show-Message ($message + "`n日志：" + (Join-Path $dataDirectory 'launcher-error.log')) $true }
    exit 1
}
