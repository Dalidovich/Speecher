[CmdletBinding()]
param(
    [string]$ProjectDir = $PSScriptRoot,
    [string]$OutputDir = (Join-Path $PSScriptRoot '..\..\publish'),
    [string]$Runtime = 'win-x64',
    [string]$Configuration = 'Release',
    [switch]$Launch
)

$ErrorActionPreference = 'Stop'

function Write-Step([string]$msg) {
    Write-Host "`n=== $msg ===" -ForegroundColor Cyan
}

function Assert-SafeOutputDir([string]$path) {
    if (-not $path) { throw 'OutputDir is required, for example: -OutputDir "D:\dist\Speecher"' }
    $full = [System.IO.Path]::GetFullPath($path)
    $protected = @(
        [System.IO.Path]::GetPathRoot($full)
        $env:USERPROFILE
        $env:SystemRoot
        $env:ProgramFiles
        ${env:ProgramFiles(x86)}
        $ProjectDir
    ) | Where-Object { $_ }

    foreach ($candidate in $protected) {
        if ($full.TrimEnd('\') -ieq [System.IO.Path]::GetFullPath($candidate).TrimEnd('\')) {
            throw "Refusing to use -OutputDir '$full': the script replaces files in that directory. Pick a dedicated folder."
        }
    }
    return $full
}

$CsprojPath = Join-Path $ProjectDir 'Speecher.csproj'
$PublishDir = Join-Path $ProjectDir "bin\$Configuration\net9.0-windows\$Runtime\publish"

if (-not (Test-Path $CsprojPath)) {
    throw "Project not found: $CsprojPath"
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet SDK not found in PATH'
}

$OutputDir = Assert-SafeOutputDir $OutputDir

Write-Step 'Publishing single-file exe'
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
& dotnet publish $CsprojPath `
    -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None -p:DebugSymbols=false `
    -p:NoWarn=MSB3246
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

Write-Step "Copying to $OutputDir"
Get-Process -Name 'Speecher' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -like "$OutputDir*" } |
    Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir | Out-Null }
$runtimesOut = Join-Path $OutputDir 'runtimes'
if (Test-Path $runtimesOut) { Remove-Item $runtimesOut -Recurse -Force }
Copy-Item (Join-Path $PublishDir '*') $OutputDir -Recurse -Force

Write-Host "`nDone. Artifacts in: $OutputDir" -ForegroundColor Green
Write-Host 'Kept as is: settings.json, history.jsonl, models, runtime' -ForegroundColor Green

if ($Launch) {
    Start-Process -FilePath (Join-Path $OutputDir 'Speecher.exe') -WorkingDirectory $OutputDir
}
