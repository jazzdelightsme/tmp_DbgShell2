
Describe "WriteMem" {

    pushd
    New-TestApp -TestApp TestNativeConsoleApp -Attach -TargetName testApp -HiddenTargetWindow

    # Ignore the "symbol path is empty" warning.
    Set-DbgSymbolPath '' 3>&1 | Out-Null

    # 'csp': "call stack pointer" (processor-independent)
    $orig = dp $csp

    $memCommands = @( 'eb', 'ed', 'ep' )

    foreach( $memCmd in $memCommands )
    {
        It "can write memory with $memCmd" {
          # Write-Host ''
          # Write-Host "Trying command: $memCmd" -Fore Cyan
          # Write-Host ''

            $readCmd = $memCmd.Replace( 'e', 'd' )

            & $memCmd $csp 90 # <-- N.B. no '0x' or '0n'; it should be interpreted as hex.

            0x90 | Should Be ((& $readCmd $csp L1)[ 0 ])

            & $memCmd $csp 0x90

            0x90 | Should Be ((& $readCmd $csp L1)[ 0 ])

            & $memCmd $csp 0n90

            90 | Should Be ((& $readCmd $csp L1)[ 0 ])


            & $memCmd $csp 90 90 # <-- N.B. no '0x' or '0n'; it should be interpreted as hex.

            $readback = & $readCmd $csp L2
            0x90 | Should Be ($readback[ 0 ])
            0x90 | Should Be ($readback[ 1 ])


            # This used to result in a parameter binding error on PSv5.1--what got passed
            # in was a string, "90 90", which couldn't be handled by the
            # AddressTransformationAttribute. But on PSv7, a string[] gets passed instead
            # ("90", "90"), and they get parsed as hex, and it all works out.
            & $memCmd $csp @( 90, 90 ) -ErrorAction Stop

            0x90 | Should Be ((& $readCmd $csp L1)[ 0 ])

            $myBytes = New-Object 'System.byte[]' -arg @( 2 )
            $myBytes[ 0 ] = 0x9a
            $myBytes[ 1 ] = 0x9b

            $itThrew = $false
            try
            {
                # Unlike the previous specimen, this should still result in a parameter
                # binding error when we are trying to write bytes, even on PSv7. The
                # reason is because the string array passed in is @("154", "155")--they
                # got printed as decimal, but the AdddressTransformation stuff will parse
                # them as hex, and those values are too large to fit in a byte.
                & $memCmd $csp $myBytes -ErrorAction Stop
            }
            catch
            {
                $itThrew = $true
            }

            $itThrew | Should Be ($memCmd -eq 'eb')

            # This should work, but those are decimal (0n91, 0n92).
            & $memCmd $csp -Val @( 91, 92 )

            $readback = & $readCmd $csp L2
            0x5b | Should Be ($readback[ 0 ])
            0x5c | Should Be ($readback[ 1 ])


            # This should work.
            & $memCmd $csp -Val $myBytes

            $readback = & $readCmd $csp L2
            154 | Should Be ($readback[ 0 ])
            155 | Should Be ($readback[ 1 ])
        }
    } # end foreach( $memCmd )

    .kill
    popd
}

