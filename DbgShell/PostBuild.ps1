# How to inovke in post-build step: powershell.exe -NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -Command ". '$(SolutionDir)PostBuild.ps1' '$(ProjectDir)' '$(PlatformName)' '$(TargetDir)'; exit $LastExitCode"

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

[Console]::WriteLine( "DbgShell PostBuild.ps1: ProjectDir is: {0}", $ProjectDir )
[Console]::WriteLine( "DbgShell PostBuild.ps1: PlatformName is: {0}", $PlatformName )
[Console]::WriteLine( "DbgShell PostBuild.ps1: DestDir is: {0}", $DestDir )

EnsureDirectoryExists $DestDir

$uqProjectDir = RemoveTrailingWhack (Unquote $ProjectDir)
$uqDestDir = RemoveTrailingWhack (Unquote $DestDir)


[Console]::WriteLine( "DbgShell PostBuild.ps1: Moving PS Modules directory ..." )
[Console]::WriteLine( "robocopy `"$uqDestDir\runtimes\win\lib\net5.0\Modules`" `"$uqDestDir\Modules`" * /MOVE /S /NP" )
robocopy "$uqDestDir\runtimes\win\lib\net5.0\Modules" "$uqDestDir\Modules" * /MOVE /S /NP

[Console]::WriteLine( "DbgShell PostBuild.ps1: Removing the rest of the unneeded `"runtimes`" dir..." )
[Console]::WriteLine( "del `"$uqDestDir\runtimes`" -Recurse -Force" )
del "$uqDestDir\runtimes" -Recurse -Force

[Console]::WriteLine( "DbgShell PostBuild.ps1: Removing other unneeded dirs..." )

$unneededDirs = @( 'cs'
                   'de'
                   'es'
                   'fr'
                   'it'
                   'ja'
                   'ko'
                   'pl'
                   'pt-BR'
                   'ru'
                   'tr'
                   'zh-Hans'
                   'zh-Hant' )

foreach( $ud in $unneededDirs )
{
    del "$uqDestDir\$ud" -Recurse -Force
}

$LastExitCode = 0

