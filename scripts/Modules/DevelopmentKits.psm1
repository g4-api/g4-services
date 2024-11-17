function Publish-DotNetSdk {
    <#
    .SYNOPSIS
    Downloads and publishes a specified version of the .NET SDK based on the operating system and architecture.

    .DESCRIPTION
    This function detects the current operating system and architecture, fetches the latest SDK release metadata for the specified major version, determines the appropriate download link, and publishes the .NET SDK to the designated output directory.

    .PARAMETER MajorVersion
    The major version number of the .NET SDK to publish (e.g., 8).

    .PARAMETER OutputDirectory
    The directory where the .NET SDK will be published. Defaults to the current location appended with '/dotnet'.

    .EXAMPLE
    Publish-DotNetSdk -MajorVersion 8 -OutputDirectory "C:\dotnet"

    .EXAMPLE
    Publish-DotNetSdk -MajorVersion 7

    .NOTES
    - Ensure you have the necessary permissions to write to the specified output directory.
    - This function supports Windows, Linux, and macOS platforms.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true, Position = 0)]
        [int]$MajorVersion,

        [Parameter(Mandatory = $false)]
        [string]$OutputDirectory = "$(Get-Location)/dotnet"
    )

    Write-Verbose "Starting Publish-DotNetSdk function."

    try {
        $isLinux = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Linux)
        $isOsx   = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)
        $currentOS = switch ($PSVersionTable.PSPlatform) {
            'Win32NT' {
                'win'
            }
            'Unix' {
                if ($isLinux) { 
                    'linux' 
                }
                elseif ($isOsx) { 
                    'osx' 
                }
                else { 
                    'linux' 
                }
            }
            default {
                'win'
            }
        }
        Write-Verbose "Detected Operating System: $currentOS"

        # Detect the system architecture
        $arch = switch ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) {
            'X64' { 'x64' }
            'Arm64' { 'arm64' }
            'X86' { 'x86' }
            default { 'x64' }
        }
        Write-Verbose "Detected Architecture: $arch"

        $releaseMetadataUrl = "https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/$MajorVersion.0/releases.json"
        Write-Verbose "Fetching release metadata from URL: $releaseMetadataUrl"

        Write-Verbose "Initiating web request to fetch release metadata."
        $metadataJson = (Invoke-WebRequest -Uri $releaseMetadataUrl -Method Get -UseBasicParsing).Content
        Write-Verbose "Successfully fetched release metadata."

        $metadata = ConvertFrom-Json $metadataJson
        Write-Verbose "Converted JSON response to PowerShell object."

        $sdkVersion = $metadata."latest-sdk"
        Write-Verbose "Latest SDK Version Identified: $sdkVersion"

        $rid = "$($currentOS)-$($arch)"
        Write-Verbose "Determined Runtime Identifier (RID): $rid"

        # Find the SDK release matching the latest SDK version
        $sdk = $metadata.releases | Where-Object { $_.sdk.version -eq $sdkVersion }

        if (-not $sdk) {
            Write-Error "No SDK release found for version $sdkVersion in the release metadata."
            return
        }
        Write-Verbose "Found SDK release for version $sdkVersion."

        # Determine the download link for the SDK archive
        $downloadLink = $sdk.sdk.files | Where-Object { 
            ($_.url -notmatch "exe") -and 
            ($_.url -notmatch "pkg") -and 
            ($_.url -match $rid) 
        } | Select-Object -First 1 | Select-Object -ExpandProperty "url"

        if (-not $downloadLink) {
            Write-Error "No suitable download link found for RID $rid."
            return
        }
        Write-Verbose "Download link determined: $downloadLink"

        # Extract the file name from the download link
        if ($downloadLink -match "(?<=\/)dotnet.*$") {
            $fileName = "$($matches[0])".Replace("-$($sdkVersion)-$($rid)", [string]::Empty)
            Write-Verbose "Extracted file name from download link: $fileName"
        }
        else {
            Write-Error "Failed to extract file name from the download URL."
            return
        }

        Write-Verbose "Calling Publish-RemoteArchive with the following parameters:"
        Write-Verbose "  DeploymentPath: $OutputDirectory"
        Write-Verbose "  DownloadUrl: $downloadLink"
        Write-Verbose "  OutputFile: $fileName"
        Write-Verbose "  Filter: '*dotnet-*'"
        Write-Verbose "  RootArchive: $true"

        Publish-RemoteArchive `
            -DeploymentPath $OutputDirectory `
            -DownloadUrl    $downloadLink `
            -OutputFile     $fileName `
            -Filter         '*dotnet-*' `
            -RootArchive

        Write-Verbose "Publish-DotNetSdk function completed successfully."
    }
    catch {
        Write-Error "An unexpected error occurred in Publish-DotNetSdk: $_"
    }
}

function Publish-RemoteArchive {
    <#
    .SYNOPSIS
    Downloads and extracts a remote archive to a specified deployment path.

    .DESCRIPTION
    This function downloads an archive file from a given URL, extracts its contents to the specified deployment directory, and performs cleanup based on whether the archive is a root archive. It supports both Windows and Unix-based systems.

    .PARAMETER DeploymentPath
    The path where the archive will be extracted.

    .PARAMETER DownloadUrl
    The URL to download the archive file from.

    .PARAMETER RootArchive
    A switch indicating if the downloaded archive is a root archive (i.e., the files are directly under the archive).

    .PARAMETER Filter
    Filter to find subdirectories in the extracted contents.

    .PARAMETER OutputFile
    The local file path where the downloaded archive will be saved.

    .EXAMPLE
    Publish-RemoteArchive -DeploymentPath "C:\deploy" -DownloadUrl "https://example.com/archive.zip" -OutputFile "archive.zip" -Filter '*dotnet-*'

    .NOTES
    Ensure that the system has the necessary tools installed for archive extraction (e.g., tar for Unix-based systems).
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [string]$DownloadUrl,

        [Parameter(Mandatory = $true, Position = 1)]
        [string]$DeploymentPath,

        [Parameter(Mandatory = $true, Position = 2)]
        [string]$OutputFile,

        [Parameter(Mandatory = $false)]
        [Switch]$RootArchive,

        [Parameter(Mandatory = $false)]
        [string]$Filter = '*.*'
    )

    Write-Verbose "Starting Publish-RemoteArchive function."

    Write-Verbose "Downloading archive from $DownloadUrl to $OutputFile."
    try {
        Invoke-WebRequest `
            -Uri     $DownloadUrl `
            -OutFile $OutputFile `
            -UseBasicParsing
        Write-Verbose "Successfully downloaded the archive."
    }
    catch {
        Write-Error "Failed to download archive from $DownloadUrl. $_"
        return
    }

    if ([System.IO.Directory]::Exists($DeploymentPath)) {
        Write-Verbose "Deployment path $DeploymentPath exists. Removing it to avoid conflicts."
        try {
            Remove-Item $DeploymentPath -Recurse -Force
            Write-Verbose "Successfully removed existing deployment path."
        }
        catch {
            Write-Error "Failed to remove existing deployment path $DeploymentPath. $_"
            return
        }
    }
    else {
        Write-Verbose "Deployment path $DeploymentPath does not exist. Creating it."
    }

    Write-Verbose "Extracting archive to $DeploymentPath."
    try {
        if ([System.Environment]::OSVersion.Platform -eq 'Unix') {
            Write-Verbose "Operating system is Unix-based."
            New-Item -Path ($DeploymentPath) -ItemType Directory -Force | Out-Null

            Write-Verbose "Extracting using tar."
            Invoke-Expression ("tar -vxf " + "`"$OutputFile`" -C " + "`"$DeploymentPath`"")
        }
        elseif ([System.Environment]::OSVersion.Platform -eq 'Win32NT') {
            Write-Verbose "Operating system is Windows."
            Write-Verbose "Extracting using Expand-Archive."
            Expand-Archive `
                -Path            $OutputFile `
                -DestinationPath $DeploymentPath `
                -Force
        }
        else {
            Write-Error "Unsupported platform. This function supports Windows and Unix-based systems only."
            return
        }
        Write-Verbose "Successfully extracted the archive."
    }
    catch {
        Write-Error "Failed to extract the archive. $_"
        return
    }

    if ($RootArchive) {
        Write-Verbose "RootArchive switch is set. Removing the downloaded archive file."
        try {
            Remove-Item $OutputFile -Recurse -Force
            Write-Verbose "Successfully removed the downloaded archive."
        }
        catch {
            Write-Error "Failed to remove the downloaded archive $OutputFile. $_"
            return
        }
        Write-Verbose "Exiting Publish-RemoteArchive function as RootArchive is set."
        return
    }

    Write-Verbose "Searching for subdirectories in $DeploymentPath with filter '$Filter'."
    $path = (Get-ChildItem -Path $DeploymentPath -Filter $Filter | Select-Object -First 1).FullName

    if (-not $path) {
        Write-Error "No subdirectories found in $DeploymentPath matching the filter '$Filter'."
        return
    }
    Write-Verbose "Found subdirectory: $path"

    Write-Verbose "Moving files from $path to $DeploymentPath."
    try {
        Get-ChildItem -Path $path -Filter '*.*' | Move-Item -Destination $DeploymentPath -Force
        Write-Verbose "Successfully moved files to $DeploymentPath."
    }
    catch {
        Write-Error "Failed to move files from $path to $DeploymentPath. $_"
        return
    }

    Write-Verbose "Removing the subdirectory $path."
    try {
        Remove-Item $path -Recurse -Force
        Write-Verbose "Successfully removed subdirectory $path."
    }
    catch {
        Write-Error "Failed to remove subdirectory $path. $_"
        return
    }

    Write-Verbose "Removing the downloaded archive file $OutputFile."
    try {
        Remove-Item $OutputFile -Recurse -Force
        Write-Verbose "Successfully removed the downloaded archive."
    }
    catch {
        Write-Error "Failed to remove the downloaded archive $OutputFile. $_"
        return
    }

    Write-Verbose "Completed Publish-RemoteArchive function."
}
