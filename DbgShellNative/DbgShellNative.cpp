//#include <windows.h>
//#include <stdio.h>

#include "pch.h"
#include <assert.h>

#include "ClrHost.h"


static WCHAR* g_pDbgShellExePath = nullptr;
static ClrHost* g_pClrHost = nullptr;

EXTERN_C IMAGE_DOS_HEADER __ImageBase;

WCHAR* _GetDbgShellBinaryPath()
{
    DWORD cch;

    const DWORD cchPath = MAX_PATH + 1;
    WCHAR* path = new WCHAR[ cchPath ];

    //
    // Get the path to the currently executing module (e.g. "C:\foo\bar\DbgShellExt.dll")
    //
    cch = GetModuleFileName( reinterpret_cast<HMODULE>( &__ImageBase ), path, cchPath );
    if( 0 == cch )
    {
        wprintf( L"GetModuleFileName failed: %i\n", GetLastError() );
        RaiseFailFastException( nullptr, 0, 0 );
    }
    else if( cch == cchPath )
    {
        DWORD dwErr = GetLastError();
        if( dwErr == ERROR_INSUFFICIENT_BUFFER )
        {
            wprintf( L"We need a bigger buffer.\n" );
            RaiseFailFastException( nullptr, 0, 0 );
        }
    }

    //
    // Rewind to last '\'
    //
    WCHAR* p = path + cch - 1; // points to last character
    while( (*p != '\\') && (p > path) )
        p--;

    if( *p != '\\' )
        RaiseFailFastException( nullptr, 0, 0 );

    // Keep the '\'.
    p++;

    // Append "DbgShell.exe".
    wcscpy_s( p, cchPath - (p - path), L"DbgShell.exe" );

    return path;
} // end _GetDbgShellBinaryPath()


int IgnoreDebugBreakFilter( EXCEPTION_POINTERS* pEp )
{
    wprintf( L"In exception filter. ExceptionCode: %#x Flags: %#x\n",
             pEp->ExceptionRecord->ExceptionCode,
             pEp->ExceptionRecord->ExceptionFlags );

    if( (pEp->ExceptionRecord->ExceptionCode == STATUS_BREAKPOINT) ||
        (pEp->ExceptionRecord->ExceptionCode == STATUS_ASSERTION_FAILURE) )
    {
        return EXCEPTION_CONTINUE_EXECUTION;
    }

    return EXCEPTION_EXECUTE_HANDLER;
}

HRESULT SehWrapper()
{
    HRESULT hr = S_OK;

    __try
    {
        hr = g_pClrHost->RunAssembly(0); // TODO: args
    }
    __except( IgnoreDebugBreakFilter( GetExceptionInformation() ) )
    {
        wprintf( L"DbgShellExt: Unexpected: SEH exception. failed: %#x\n", hr );

        g_pClrHost->CallInEmergency();
    }

    return hr;
}

int main()
{
    printf( "Hello world.\n" );

    bool bItWorked = SetEnvironmentVariableW( L"DOTNET_ROOT", L"C:\\temp\\just_dotnet" );
    assert( bItWorked );

    g_pDbgShellExePath = _GetDbgShellBinaryPath();

    g_pClrHost = new ClrHost( g_pDbgShellExePath );
    HRESULT hr = g_pClrHost->Initialize( /* createNewAppDomain = */ false );
    if( FAILED( hr ) )
    {
        delete g_pClrHost;
        delete g_pDbgShellExePath;
        g_pClrHost = nullptr;
        g_pDbgShellExePath = nullptr;
        goto Cleanup;
    }
    wprintf( L"DbgShellNative: Initialized CLR stuff.\n" );

    hr = SehWrapper();

Cleanup:
    return hr;
}
