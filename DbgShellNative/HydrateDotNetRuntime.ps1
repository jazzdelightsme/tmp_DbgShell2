# How to inovke in post-build step: powershell.exe -NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -Command ". '$(SolutionDir)<this file>.ps1' '$(ProjectDir)' '$(PlatformName)' '$(TargetDir)'; exit $LastExitCode"

[CmdletBinding()]
param(
    [parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string] $ProjectDir,

    [parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string] $PlatformName,

    [parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string] $DestDir
)

Set-StrictMode -Version Latest

try
{
    function Unquote( [string] $s )
    {
        if( $s[ 0 ] -eq '"' )
        {
            return $s.Substring( 1, $s.Length - 2 )
        }
        else
        {
            return $s
        }
    }

    function EnsureQuoted( [string] $s )
    {
        if( $s[ 0 ] -eq '"' )
        {
            return $s
        }
        else
        {
            return '"' + $s + '"'
        }
    }

    function EnsureDirectoryExists( [string] $dir )
    {
        if( ![System.IO.Directory]::Exists( $dir ) )
        {
            $dirInfo = [System.IO.Directory]::CreateDirectory( $dir )
            "Created directory: " + $dirInfo.FullName | Out-Host
        }
    }

    function RemoveTrailingWhack( [string] $s )
    {
        if( $s[ $s.Length - 1 ] -eq '\' )
        {
            return $s.Substring( 0, $s.Length - 1 );
        }
        else
        {
            return $s
        }
    }

    [Console]::WriteLine( "ProjectDir is: {0}", $ProjectDir )
    [Console]::WriteLine( "PlatformName is: {0}", $PlatformName )
    [Console]::WriteLine( "DestDir is: {0}", $DestDir )

    EnsureDirectoryExists $DestDir

    $uqProjectDir = RemoveTrailingWhack (Unquote $ProjectDir)
    $uqDestDir = RemoveTrailingWhack (Unquote $DestDir)


    $dotnetDir = $uqDestDir + '\dotnet'
    EnsureDirectoryExists $dotnetDir

    $arch = $PlatformName
    if( $arch -eq 'Win32' )
    {
        $arch = 'x86'
    }

    [Console]::WriteLine( "Hydrating standalone dotnet runtime to $dotnetDir" )

    # We should avoid pulling this down every time... let's cache it as if it were a NuGet
    # package.

    $cachePath = "$PSScriptRoot\..\packages\dotnet_$arch"

    if( !(Test-Path $cachePath) )
    {
        [Console]::WriteLine( "Downloading dotnet runtime to local cache ($cachePath)" )

        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

        $scriptText = Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1'
        $script = [ScriptBlock]::Create( $scriptText )

        & $script -NoPath -Runtime dotnet -InstallDir $cachePath -Architecture $arch -Channel 5.0
    }
    else
    {
        [Console]::WriteLine( "Restoring from cache ($cachePath)" )
    }

    robocopy $cachePath $dotnetDir /MIR /NP

    # TODO: interpret robocopy result?

    $LastExitCode = 0
}
finally { }

