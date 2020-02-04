#define INITGUID
#include "pch.h"

#include <nethost.h>

using namespace std;
using namespace filesystem;


// This is necessary because the v2 of <filesystem> didn't handle UNC paths well--it
// frequently squashes the two leading whacks down to one.
wstring _FixUncPathIfNecessary( path& path )
{
 // if( (path.generic_wstring().size() > 2) &&
 //     (path.generic_wstring()[ 0 ] == '\\') &&
 //     (path.generic_wstring()[ 1 ] != '\\') )
 // {
 //     return wstring( L"\\" ) + path.generic_wstring();
 // }
 // else
 // {
        return path.generic_wstring();
 // }
}


class ClrHost
{
private:
    path m_exePath;
    path m_appBasePath;
    path m_configFilePath;

    bool m_separateAppDomain = false;

    bool m_emergencyStopped = false;


public:
    ClrHost( path exePath )
        : m_exePath( exePath )
    {
        m_appBasePath = exePath.parent_path();
        m_configFilePath = _FixUncPathIfNecessary( exePath ) + L".config";
    } // end constructor


    ~ClrHost()
    {
    } // end destructor


    void CallInEmergency()
    {
        m_emergencyStopped = true;
    } // end CallInEmergency()




    // Loads and starts the CLR (if necessary) and creates a new appdomain to run
    // managed code.
    //
    // (We don't want to use the default appdomain because we want to be able to unload
    // stuff, which you can only do by unloading an appdomain, and you can't unload the
    // default appdomain without shutting down the CLR, and we don't want to shut down the
    // CLR, because then it can't be restarted.)
    HRESULT Initialize( bool createNewAppDomain )
    {
        HRESULT hr = S_OK;

        return hr;
    } // end Initialize()


    HRESULT RunAssembly( int numArgs, ... )
    {
        va_list vlArgs;
        vector< LPCWSTR > vectorArgs( numArgs );

        va_start( vlArgs, numArgs );

        for( LONG i = 0; i < numArgs; i++ )
        {
            vectorArgs[ i ] = va_arg( vlArgs, LPCWSTR );
        }

        va_end( vlArgs );

        return RunAssembly( vectorArgs );
    } // end RunAssembly()

    typedef int (*component_entry_point_fn)(void *arg, int32_t arg_size_in_bytes);

    HRESULT RunAssembly( const vector< LPCWSTR >& args )
    {
        if( m_emergencyStopped )
        {
            return E_ABORT;
        }

        HRESULT hr = S_OK;

        DWORD retval = -1;
        path assembly = m_exePath;
        assembly.replace_extension( L"dll" );

        component_entry_point_fn fn = 0;
     // hr = m_runtimeHost->CreateDelegate( m_domainId,
     //                                     L"DbgShell, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
     //                                     L"MS.DbgShell.MainClass",
     //                                     L"MainForNativeHost",
     //                                     (INT_PTR*) &fn );
     // if( FAILED( hr ) )
     // {
     //     wprintf( L"Failed to create the delegate: %#x\n", hr );
     //     goto Cleanup;
     // }

        retval = fn(
            (LPCWSTR*) ((args.size() > 0) ? args.data() : nullptr), 
            args.size() );

        wprintf( L"Assembly execution finished! Exit code: %i\n", retval );

    Cleanup:
        return hr;
    }
}; // end class ClrHost

