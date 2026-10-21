param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $Root 'Windows/Launcher/WordFloat.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw ($errors | Out-String) }
# Load only function definitions; never run installation, registry or UI operations on the build host.
foreach ($function in $ast.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst]}, $false)) {
    Invoke-Expression $function.Extent.Text
}
function Require([bool]$condition, [string]$message) { if (!$condition) { throw $message } }
function Must-Fail([scriptblock]$action, [string]$message) {
    $failed = $false
    try { & $action } catch { $failed = $true }
    Require $failed $message
}
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('wordfloat-launcher-checks-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($temporary)
try {
    $app = Join-Path $temporary 'app'
    [void][IO.Directory]::CreateDirectory($app)
    $files = [ordered]@{}
    foreach ($name in @('WordFloat.exe','WordFloat.dll','coreclr.dll','hostfxr.dll','PresentationFramework.dll','zh-Hans/test.resources.dll')) {
        $path = Join-Path $app $name
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
        [IO.File]::WriteAllText($path,('test-payload-' + $name))
        $files[$name] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    }
    @{product='WordFloat'; architecture='win-x64'; version='test'; files=$files} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $app 'package.json') -Encoding UTF8
    $null = Test-Payload $app
    Write-Host 'PASS PowerShell syntax and complete payload hashes'
    $native = Join-Path $app 'coreclr.dll'
    $original = [IO.File]::ReadAllText($native)
    [IO.File]::WriteAllText($native,'damaged')
    Must-Fail {Test-Payload $app} 'Damaged native runtime was accepted'
    Remove-Item -LiteralPath $native
    Must-Fail {Test-Payload $app} 'Missing native runtime was accepted'
    [IO.File]::WriteAllText($native,$original)
    foreach ($relative in @('../outside.txt','folder/../../outside.txt','folder\..\outside.txt','C:\outside.txt')) {
        Must-Fail {Get-PayloadPath $app $relative} ('Unsafe path accepted: ' + $relative)
    }
    Write-Host 'PASS missing/tampered runtimes and traversal rejected'
    $script:moveAttempts=0
    function Move-Item {
        param($LiteralPath,$Destination)
        $script:moveAttempts++
        if ($script:moveAttempts -lt 3) { throw [IO.IOException]::new('simulated image lock') }
        Microsoft.PowerShell.Management\Move-Item -LiteralPath $LiteralPath -Destination $Destination
    }
    $from=Join-Path $temporary 'incoming'; $to=Join-Path $temporary 'promoted'
    [void][IO.Directory]::CreateDirectory($from)
    Move-Payload $from $to
    Require ($script:moveAttempts -eq 3 -and (Test-Path -LiteralPath $to)) 'Update did not retry transient process locks'
    Remove-Item function:\Move-Item
    Write-Host 'PASS transient process image locks retried before replacement'
    foreach ($name in @('WordFloat.ps1','Install.cmd','Start.cmd','SafeMode.cmd','Diagnose.cmd','WordFloatSetup.exe')) {
        [IO.File]::WriteAllText((Join-Path $temporary $name),'isolated launcher fixture')
    }
    $title='浮词';$installDirectory=Join-Path $temporary 'installed'
    $script:stops=0;$script:desktopRequested=$true;$script:progress=[Collections.Generic.List[int]]::new()
    function Stop-WordFloat { $script:stops++ }
    function Register-WordFloat([string]$version,[bool]$CreateDesktopShortcut) {
        $script:desktopRequested=$CreateDesktopShortcut
        return [pscustomobject]@{DesktopShortcutCreated=$CreateDesktopShortcut;Warnings=@()}
    }
    $originalStart=(Get-Item function:Start-WordFloat).ScriptBlock
    function Start-WordFloat { throw 'Installation must not launch before the completion page' }
    $result=Install-WordFloat -SourceRoot $temporary -CreateDesktopShortcut:$false -Progress {param($percent,$message) $script:progress.Add($percent)}
    Require ($result.Version -eq 'test' -and !$result.DesktopShortcutCreated -and !$script:desktopRequested) 'Desktop shortcut preference ignored'
    Require ($script:progress[0] -eq 0 -and $script:progress[$script:progress.Count-1] -eq 100) 'Install progress missing start or completion'
    for ($i=1;$i -lt $script:progress.Count;$i++) { Require ($script:progress[$i] -ge $script:progress[$i-1]) 'Install progress moved backwards' }
    $null=Test-Payload (Join-Path $installDirectory 'app')
    Require (Test-Path -LiteralPath (Join-Path $installDirectory 'WordFloatSetup.exe')) 'Installed wizard launcher missing'
    [IO.File]::WriteAllText((Join-Path $installDirectory 'app/old-marker.txt'),'old app retained')
    $originalMove=(Get-Item function:Move-Payload).ScriptBlock
    function Move-Payload([string]$source,[string]$destination) {
        if ((Split-Path $source -Leaf).StartsWith('.incoming-')) { throw [IO.IOException]::new('simulated replacement failure') }
        & $originalMove $source $destination
    }
    $script:progress.Clear()
    Must-Fail { Install-WordFloat -SourceRoot $temporary -Progress {param($percent,$message) $script:progress.Add($percent)} } 'Replacement failure accepted'
    Require (Test-Path -LiteralPath (Join-Path $installDirectory 'app/old-marker.txt')) 'Previous app was not restored after replacement failure'
    Require (!($script:progress -contains 100)) 'Failed install reported completion'
    Set-Item function:Move-Payload $originalMove
    Set-Item function:Start-WordFloat $originalStart
    Write-Host 'PASS real file installation progress, shortcut choice, deferred launch and replacement rollback'
    $originalOutput=[Console]::Out;$capture=[IO.StringWriter]::new()
    try { [Console]::SetOut($capture);Write-InstallEvent @{kind='complete';version='test';directory=$installDirectory;warnings=@()} }
    finally { [Console]::SetOut($originalOutput) }
    $event=$capture.ToString().Trim();Require ($event.StartsWith('WORDFLOAT_SETUP:')) 'Worker protocol missing prefix'
    Require (($event.Substring(16) | ConvertFrom-Json).kind -eq 'complete') 'Worker event JSON invalid'
    Write-Host 'PASS structured installer completion protocol'
    $dataDirectory = Join-Path $temporary 'data'
    [void][IO.Directory]::CreateDirectory($dataDirectory)
    $script:simulatedExit = $false; $script:simulatedCode = 0; $script:argumentsSeen = @()
    function Show-Message([string]$text, [bool]$failure = $false) { throw ('Unexpected message: ' + $text) }
    function Start-Process {
        param($FilePath,$WorkingDirectory,$PassThru,$RedirectStandardError,$RedirectStandardOutput,$ArgumentList)
        $script:argumentsSeen = $ArgumentList
        [IO.File]::WriteAllText($RedirectStandardError,'simulated-host-output')
        if (!$script:simulatedExit) { [IO.File]::WriteAllText((Join-Path $dataDirectory 'startup.log'),'[9001] READY library') }
        $process = [pscustomobject]@{ Id=9001; HasExited=$script:simulatedExit; ExitCode=$script:simulatedCode }
        $process | Add-Member -MemberType ScriptMethod -Name Refresh -Value {}
        return $process
    }
    $env:DOTNET_HOST_TRACE='previous-value';$env:DOTNET_HOST_TRACEFILE='previous-file'
    Start-WordFloat $temporary $true $false
    Require ($script:argumentsSeen -contains '--safe-mode') 'Safe mode not passed to application'
    Require ($env:DOTNET_HOST_TRACE -eq 'previous-value' -and $env:DOTNET_HOST_TRACEFILE -eq 'previous-file') 'Host trace environment leaked'
    $launch = Get-ChildItem -LiteralPath (Join-Path $dataDirectory 'Logs') -Filter launch.json -Recurse | Select-Object -First 1
    $record = Get-Content -LiteralPath $launch.FullName -Raw | ConvertFrom-Json
    Require ($record.ready -and $record.processId -eq 9001) 'Readiness report did not use the launched process'
    $script:simulatedExit=$true;$script:simulatedCode=-1073741515
    Must-Fail {Start-WordFloat $temporary $false $false} 'Native startup failure was silently accepted'
    Write-Host 'PASS simulated ready/early-native-exit reports and safe-rendering arguments'
    [IO.File]::WriteAllText((Join-Path $app 'my-notes.txt'),'keep user content')
    Remove-ManagedPayload $app
    Require (Test-Path -LiteralPath (Join-Path $app 'my-notes.txt')) 'Uninstall removed unknown content'
    Require (!(Test-Path -LiteralPath (Join-Path $app 'WordFloat.exe'))) 'Managed application files were not removed'
    Require (!(Test-Path -LiteralPath (Join-Path $app 'zh-Hans'))) 'Empty managed resource directories were not removed'
    Write-Host 'PASS managed cleanup retains unknown files'
    Write-Host 'LAUNCHER_CHECKS_PASSED (file/logic simulation; Windows integration not executed)'
} finally { Remove-Item -LiteralPath $temporary -Recurse -Force }
