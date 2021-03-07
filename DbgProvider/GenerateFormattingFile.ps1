<#
.SYNOPSIS
    Reads Debugger.Format.ps1xml.in, replaces pieces of it according to the specified switches, then writes it out to the specified file.
#>
[CmdletBinding()]
param( [string] $OutputPath,
       [switch] $Color,
       [switch] $ThirtyTwoBit
     )

process
{
    try
    {
        # IsPathFullyQualified would be the better choice here, but it is not available in
        # [legacy] Windows PowerShell (5.1).
        if( ![System.IO.Path]::IsPathRooted( $OutputPath ) )
        {
            throw "GenerateFormattingFile: OutputPath must be rooted."
        }

        if( $Color )
        {
            $CustomToStringBlock = @'
                    if( $_ -is [MS.Dbg.ISupportColor] )
                    {
                        $_.ToColorString().ToString( [MS.Dbg.DbgProvider]::HostSupportsColor )
                    }
                    else
                    {
                        $_.ToString()
                    }
'@

            $ToStringBlock = @'
                    $_.ToString( [MS.Dbg.DbgProvider]::HostSupportsColor )
'@
        }
        else
        {
            $CustomToStringBlock = @'
                    $_.ToString()
'@

            $ToStringBlock = @'
                    $_.ToString( $false )
'@
        }

        if( $ThirtyTwoBit )
        {
            $WidthBlock = @'
              <Width>10</Width>
'@
            $Is32Bit = 'true'
            $EightOrEighteen = @'
              <Width>8</Width>
'@
        }
        else
        {
            $WidthBlock = @'
              <Width>18</Width>
'@
            $Is32Bit = 'false'
            $EightOrEighteen = @'
              <Width>18</Width>
'@
        }

        Write-Host "GenerateFormattingFile: Processing Debugger.Format.ps1xml.in..."

        $content += [System.IO.File]::ReadAllText( "$PSScriptRoot\Debugger.Format.ps1xml.in" )

        $content = $content.Replace( 'CUSTOM_TO_STRING_BLOCK', $CustomToStringBlock )
        $content = $content.Replace( 'TO_STRING_BLOCK', $ToStringBlock )
        $content = $content.Replace( 'WIDTH_BLOCK', $WidthBlock )
        $content = $content.Replace( 'IS_THIRTY_TWO_BIT', $Is32Bit )
        $content = $content.Replace( 'EIGHT_OR_EIGHTEEN', $EightOrEighteen )

        [System.IO.File]::WriteAllText( $OutputPath, $content )

        Write-Host "GenerateFormattingFile: Done; written to: $OutputPath"
    }
    finally { }
}
