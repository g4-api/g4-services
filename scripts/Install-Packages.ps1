<#
.SYNOPSIS
    Automates the process of publishing .NET SDKs, installing NuGet packages, and managing directories.

.DESCRIPTION
    This script performs the following tasks:
    1. Publishes the specified .NET SDK version.
    2. Sets up necessary directories for source, installation, and package copying.
    3. Parses .csproj files to identify NuGet package dependencies.
    4. Installs the identified NuGet packages.
    5. Copies the installed NuGet packages to a designated directory.
    6. Cleans up temporary installation directories upon script exit.

.PARAMETER SourceDirectory
    The directory containing the source code (.csproj files). Defaults to "../src".

.PARAMETER InstallDirectory
    The directory where NuGet packages will be installed. Defaults to "../install".

.PARAMETER CopyDirectory
    The directory where installed NuGet packages (.nupkg files) will be copied. Defaults to "../packages".

.PARAMETER DotnetPath
    The directory where the .NET SDK will be published. Defaults to "../sdk/dotnet".

.PARAMETER MajorVersion
    The major version number of the .NET SDK to publish (e.g., 8). Defaults to 8.

.EXAMPLE
    .\Publish-Script.ps1 -MajorVersion 8 -Verbose

.NOTES
    - Ensure that the script has the necessary permissions to create and modify directories.
    - This script supports Windows and Unix-based systems.
#>

param (
    [Parameter(Mandatory = $false)]
    [string]$SourceDirectory  = "../src",

    [Parameter(Mandatory = $false)]
    [string]$InstallDirectory = "../install",

    [Parameter(Mandatory = $false)]
    [string]$CopyDirectory    = "../packages",

    [Parameter(Mandatory = $false)]
    [string]$DotnetPath       = "../sdk/dotnet",

    [Parameter(Mandatory = $false)]
    [int]$MajorVersion       = 8
)

Write-Verbose "Importing DevelopmentKits module from: $([System.IO.Path]::GetDirectoryName($PSCommandPath))/Modules/DevelopmentKits.psm1"
Import-Module -Force "$([System.IO.Path]::GetDirectoryName($PSCommandPath))/Modules/DevelopmentKits.psm1"

$currentScriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Definition
Write-Verbose "Current Script Directory: $currentScriptDirectory"

$sourceDirectory  = Join-Path -Path $currentScriptDirectory -ChildPath $SourceDirectory
Write-Verbose "Resolved Source Directory: $sourceDirectory"

$installDirectory = Join-Path -Path $currentScriptDirectory -ChildPath $InstallDirectory
Write-Verbose "Resolved Install Directory: $installDirectory"

$copyDirectory    = Join-Path -Path $currentScriptDirectory -ChildPath $CopyDirectory
Write-Verbose "Resolved Copy Directory: $copyDirectory"

$dotnetPath       = Join-Path -Path $currentScriptDirectory -ChildPath $DotnetPath
Write-Verbose "Resolved Dotnet Path: $dotnetPath"

$dotnetExePath = Join-Path -Path $dotnetPath -ChildPath "dotnet"
Write-Verbose "Initial Dotnet Executable Path: $dotnetExePath"

if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows) -eq "WINDOWS") {
    $dotnetExePath = "$dotnetExePath.exe"
    Write-Verbose "Appended .exe to Dotnet Executable Path: $dotnetExePath"
}
else {
    Write-Verbose "Dotnet Executable Path remains: $dotnetExePath"
}

Write-Verbose "Publishing .NET SDK with Major Version: $MajorVersion to Output Directory: $dotnetPath"
Publish-DotNetSdk -MajorVersion $MajorVersion -OutputDirectory $dotnetPath -Verbose

Write-Verbose "Checking if Source Directory exists: $sourceDirectory"
if (!(Test-Path $sourceDirectory -Verbose)) {
    Write-Verbose "Source Directory does not exist. Creating directory: $sourceDirectory"
    New-Item -Path $sourceDirectory -ItemType Directory -Verbose | Out-Null
}
$sourceDirectory = (Resolve-Path $sourceDirectory -Verbose).Path
Write-Verbose "Source Directory Resolved Path: $sourceDirectory"

Write-Verbose "Checking if Install Directory exists: $installDirectory"
if (!(Test-Path $installDirectory -Verbose)) {
    Write-Verbose "Install Directory does not exist. Creating directory: $installDirectory"
    New-Item -Path $installDirectory -ItemType Directory -Verbose | Out-Null
}
$installDirectory = (Resolve-Path $installDirectory -Verbose).Path
Write-Verbose "Install Directory Resolved Path: $installDirectory"

Write-Verbose "Checking if Copy Directory exists: $copyDirectory"
if (!(Test-Path $copyDirectory -Verbose)) {
    Write-Verbose "Copy Directory does not exist. Creating directory: $copyDirectory"
    New-Item -Path $copyDirectory -ItemType Directory -Verbose | Out-Null
}
$copyDirectory = (Resolve-Path $copyDirectory -Verbose).Path
Write-Verbose "Copy Directory Resolved Path: $copyDirectory"

Write-Verbose "Validating Dotnet Executable Path: $dotnetExePath"
if (!(Test-Path $dotnetExePath -Verbose)) {
    Write-Error "NuGet executable does not exist: $dotnetExePath"
    exit 1
}
Write-Verbose "Dotnet Executable Resolved Path: $dotnetExePath"

Write-Verbose "Defining cleanup script block."
$cleanupScript = {
    param ($installDirectoryPath)
    Write-Verbose "Executing cleanup for Install Directory: $installDirectoryPath"
    if (Test-Path -Path $installDirectoryPath -Verbose) {
        Write-Verbose "Removing Install Directory: $installDirectoryPath"
        Remove-Item -Path $installDirectoryPath -Recurse -Force -Verbose
        Write-Host "Temporary install folder has been deleted."
    }
}

# Register Cleanup Event
Write-Verbose "Registering cleanup event to trigger on PowerShell exit."
Register-EngineEvent -SourceIdentifier "PowerShell.Exiting" -Action {
    $installDirectoryPath = $event.MessageData
    Write-Verbose "PowerShell is exiting. Initiating cleanup for: $installDirectoryPath"
    if (Test-Path -Path $installDirectoryPath -Verbose) {
        Write-Verbose "Removing Install Directory: $installDirectoryPath"
        Remove-Item -Path $installDirectoryPath -Recurse -Force -Verbose
        Write-Host "Temporary install folder has been deleted."
    }
} -MessageData $installDirectory

# Initialize Packages Array
Write-Verbose "Initializing packages array."
$packages = @()

# Retrieve All .csproj Files
Write-Verbose "Retrieving all .csproj files from Source Directory: $sourceDirectory"
$csprojFiles = Get-ChildItem -Path $sourceDirectory -Recurse -Filter *.csproj -Verbose

# Parse .csproj Files for Package References
Write-Verbose "Parsing .csproj files for NuGet package references."
foreach ($csprojFile in $csprojFiles) {
    Write-Verbose "Processing .csproj file: $($csprojFile.FullName)"
    [xml]$csprojXml = Get-Content $csprojFile.FullName -Verbose
    $packageReferences = $csprojXml.Project.ItemGroup.PackageReference

    foreach ($packageReference in $packageReferences) {
        $packageName = $packageReference.Include
        $packageVersion = $packageReference.Version

        if (![string]::IsNullOrEmpty($packageName) -and ![string]::IsNullOrEmpty($packageVersion)) {
            Write-Verbose "Found Package: $packageName, Version: $packageVersion"
            $packages += [PSCustomObject]@{Name = $packageName; Version = $packageVersion}
        }
    }
}

# Sort and Remove Duplicate Packages
Write-Verbose "Sorting and removing duplicate packages."
$packages = $packages | Sort-Object Name, Version -Unique

# Install NuGet Packages
Write-Verbose "Installing NuGet packages."
$packages | ForEach-Object {
    $packageName = $_.Name
    $packageVersion = $_.Version
    Write-Verbose "Installing Package: $packageName, Version: $packageVersion to Install Directory: $installDirectory"
    & $dotnetExePath restore $packageName -Version $packageVersion -OutputDirectory $installDirectory -Verbosity detailed -NonInteractive
}

# Retrieve All .nupkg Files from Install Directory
Write-Verbose "Retrieving all .nupkg files from Install Directory: $installDirectory"
$nupkgFiles = Get-ChildItem -Path $installDirectory -Recurse -Filter *.nupkg -Verbose

# Copy .nupkg Files to Copy Directory
Write-Verbose "Copying .nupkg files to Copy Directory: $copyDirectory"
foreach ($file in $nupkgFiles) {
    $destination = Join-Path -Path $copyDirectory -ChildPath $file.Name
    Write-Verbose "Copying $($file.FullName) to $destination"
    Copy-Item -Path $file.FullName -Destination $destination -Force -Verbose
}

# Completion Message
Write-Host "Completed processing. All .nupkg files have been copied to $copyDirectory."

# Execute Cleanup Script
Write-Verbose "Executing cleanup script."
& $cleanupScript -installDirectoryPath $installDirectory

# Unregister Cleanup Event
Write-Verbose "Unregistering cleanup event."
Unregister-Event -SourceIdentifier "PowerShell.Exiting" -Verbose
