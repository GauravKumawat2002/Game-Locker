param(
    [ValidateSet('ZIP', 'EXE', 'Both')]
    [string]$BuildType = 'Both',

    [string]$OutputDirectory = $PSScriptRoot,

    [string]$PackageDirectory = (Join-Path $PSScriptRoot 'installer-package')
)

$ErrorActionPreference = 'Stop'
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$PackageDirectory = [IO.Path]::GetFullPath($PackageDirectory)

$innoScript = Join-Path $PSScriptRoot 'installer/InnoSetup/GameLocker-Setup.iss'
$versionLine = Select-String -LiteralPath $innoScript -Pattern '^#define MyAppVersion "([^"]+)"$'
$version = $versionLine.Matches[0].Groups[1].Value
$artifactName = "GameLocker-Installer-v$version"
$zipPath = Join-Path $OutputDirectory "$artifactName.zip"
$packagePrefix = $PackageDirectory.TrimEnd([char[]]@('\', '/')) + [IO.Path]::DirectorySeparatorChar
if ($zipPath.StartsWith($packagePrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The output directory must be outside the package directory.'
}

& (Join-Path $PSScriptRoot 'installer/PowerShell/Package-Installer.ps1') -OutputPath $PackageDirectory
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

if ($BuildType -in @('ZIP', 'Both')) {
    Compress-Archive -Path (Join-Path $PackageDirectory '*') -DestinationPath $zipPath -Force
    Write-Host "Created $zipPath"
}

if ($BuildType -in @('EXE', 'Both')) {
    $compiler = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    $compilerPath = if ($compiler) {
        $compiler.Source
    } else {
        Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'
    }

    if (!(Test-Path -LiteralPath $compilerPath -PathType Leaf)) {
        if ($BuildType -eq 'EXE') {
            throw 'Inno Setup 6 compiler was not found.'
        }
        Write-Warning 'Inno Setup 6 compiler was not found. Created the ZIP package only.'
        return
    }

    $publishDirectory = Join-Path $PackageDirectory 'publish'
    & $compilerPath "/DGameLockerPublishDirectory=$publishDirectory" "/O$OutputDirectory" $innoScript
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit code $LASTEXITCODE."
    }
    Write-Host "Created $(Join-Path $OutputDirectory "$artifactName.exe")"
}
