#define INITGUID
#include "pch.h"

#include <nethost.h>
#include <coreclr_delegates.h>
#include <hostfxr.h>

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

    hostfxr_initialize_for_runtime_config_fn init_fptr = nullptr;
    hostfxr_get_runtime_delegate_fn get_delegate_fptr = nullptr;
    hostfxr_get_runtime_properties_fn get_runtime_properties_fptr = nullptr;
    hostfxr_set_runtime_property_value_fn set_runtime_property_value_fptr = nullptr;
    hostfxr_close_fn close_fptr = nullptr;

    HRESULT load_hostfxr()
    {
        // Pre-allocate a large buffer for the path to hostfxr
        char_t buffer[MAX_PATH];
        size_t buffer_size = sizeof(buffer) / sizeof(char_t);
        int hr = get_hostfxr_path(buffer, &buffer_size, nullptr);
        if (hr != 0)
        {
            wprintf( L"get_hostfxr_path failed: %#x\n", hr );
            return hr;
        }

        // Load hostfxr and get desired exports
        HMODULE lib = LoadLibraryW(buffer);
        if( !lib )
        {
            DWORD dwErr = GetLastError();
            hr = HRESULT_FROM_WIN32( dwErr );
            wprintf( L"LoadLibraryW( %s ) failed: %#x\n", buffer, hr );
            return hr;
        }

        init_fptr = (hostfxr_initialize_for_runtime_config_fn) GetProcAddress(lib, "hostfxr_initialize_for_runtime_config");
        if( !init_fptr )
        {
            DWORD dwErr = GetLastError();
            hr = HRESULT_FROM_WIN32( dwErr );
            wprintf( L"GetProcAddress( hostfxr_initialize_for_runtime_config ) failed: %#x\n", hr );
            return hr;
        }
        get_delegate_fptr = (hostfxr_get_runtime_delegate_fn) GetProcAddress(lib, "hostfxr_get_runtime_delegate");
        if( !get_delegate_fptr )
        {
            DWORD dwErr = GetLastError();
            hr = HRESULT_FROM_WIN32( dwErr );
            wprintf( L"GetProcAddress( hostfxr_get_runtime_delegate ) failed: %#x\n", hr );
            return hr;
        }
        get_runtime_properties_fptr = (hostfxr_get_runtime_properties_fn) GetProcAddress(lib, "hostfxr_get_runtime_properties");
        if( !get_runtime_properties_fptr )
        {
            DWORD dwErr = GetLastError();
            hr = HRESULT_FROM_WIN32( dwErr );
            wprintf( L"GetProcAddress( hostfxr_get_runtime_properties ) failed: %#x\n", hr );
            return hr;
        }
        set_runtime_property_value_fptr  = (hostfxr_set_runtime_property_value_fn) GetProcAddress(lib, "hostfxr_set_runtime_property_value");
        if( !set_runtime_property_value_fptr )
        {
            DWORD dwErr = GetLastError();
            hr = HRESULT_FROM_WIN32( dwErr );
            wprintf( L"GetProcAddress( hostfxr_set_runtime_property_value ) failed: %#x\n", hr );
            return hr;
        }
        close_fptr = (hostfxr_close_fn) GetProcAddress(lib, "hostfxr_close");
        if( !close_fptr )
        {
            DWORD dwErr = GetLastError();
            hr = HRESULT_FROM_WIN32( dwErr );
            wprintf( L"GetProcAddress( hostfxr_close ) failed: %#x\n", hr );
            return hr;
        }

        return S_OK;
    }

    load_assembly_and_get_function_pointer_fn get_dotnet_load_assembly(const char_t* config_path)
    {
        // Load .NET Core
        void* load_assembly_and_get_function_pointer = nullptr;
        hostfxr_handle cxt = nullptr;
        int rc = init_fptr(config_path, nullptr, &cxt);
        // rc might be 1 ("Success_HostAlreadyInitialized") if we've already done this
        // before. So we'll just check cxt.
        if (cxt == nullptr)
        {
            wprintf(L"Init failed: %#x\n", rc);
            close_fptr(cxt);
            return nullptr;
        }

        // Get the load assembly function pointer
        rc = get_delegate_fptr(
            cxt,
            hdt_load_assembly_and_get_function_pointer,
            &load_assembly_and_get_function_pointer);
        if (rc != 0 || load_assembly_and_get_function_pointer == nullptr)
        {
            wprintf(L"Get delegate failed: %#x\n", rc);
        }

        // TODO: removeme: this is not working; it fails with InvalidArgFailure (0x80008081)
        rc = set_runtime_property_value_fptr(cxt, L"APP_PATHS", L"Debugger" );
        if( rc )
        {
            wprintf( L"set_runtime_property_value_fptr failed: %#x\n", rc );
        }

        const char_t* keys[ 128 ] = { 0 };
        const char_t* values[ 128 ] = { 0 };
        size_t count = _countof( keys );

        rc = get_runtime_properties_fptr( cxt, &count, keys, values );
        if( rc )
        {
            wprintf( L"get_runtime_properties_fptr failed: %#x\n", rc );
        }
        else
        {
            wprintf( L"Found %i properties:\n", count );
            for( int i = 0; i < count; i++ )
            {
                wprintf( L"    [%s]: %s\n", keys[ i ], values[ i ] );
            }
        }

        close_fptr(cxt);
        return (load_assembly_and_get_function_pointer_fn) load_assembly_and_get_function_pointer;
    }

public:
    ClrHost( path exePath )
        : m_exePath( exePath )
    {
        m_appBasePath = exePath.parent_path();
        m_configFilePath = _FixUncPathIfNecessary( exePath );
        m_configFilePath = m_configFilePath.replace_extension( L"runtimeconfig.json" );
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

        // TODO: appdomain stuff?

        // TEMP tracing stuff
        // This doesn't seem to be getting me any output... are we failing too early???
        SetEnvironmentVariable( L"COREHOST_TRACE", L"1" );
        SetEnvironmentVariable( L"COREHOST_TRACE_VERBOSITY", L"4" );

        hr = load_hostfxr();
        //hr = S_OK;
        if( FAILED( hr ) )
        {
            wprintf( L"load_hostfxr() failed: %#x\n", hr );
        }

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
        assembly.replace_filename( L"DotNetLoadingShim.dll" );


        load_assembly_and_get_function_pointer_fn load_assembly_and_get_function_pointer = nullptr;
        load_assembly_and_get_function_pointer = get_dotnet_load_assembly( m_configFilePath.c_str() );

        if( !load_assembly_and_get_function_pointer )
        {
            wprintf( L"get_dotnet_load_assembly( %s ) failed\n", m_configFilePath.c_str() );
            return E_FAIL;
        }

        //const char_t* dotnet_type = L"MS.DbgShell.MainClass, DbgShell";
        const char_t* dotnet_type = L"DotNetLoadingShim.Shim, DotNetLoadingShim";
        const char_t* dotnet_type_method = L"BounceToDefaultALC";

        component_entry_point_fn entryPoint = nullptr;
        int rc = load_assembly_and_get_function_pointer( assembly.c_str(),
                                                         dotnet_type,
                                                         dotnet_type_method,
                                                         (const char_t*) -1, // "unmanaged callers only"
                                                         nullptr,
                                                         (void**) &entryPoint);

        if( !entryPoint )
        {
            wprintf( L"Failed to find managed entry point: %#x\n", rc );
            // Dunno what kind of return value this is...
            if( FAILED( rc ) )
            {
                hr = rc;
            }
            else
            {
                hr = HRESULT_FROM_WIN32( rc );
            }

            return hr;
        }

        retval = entryPoint(
            (LPCWSTR*) ((args.size() > 0) ? args.data() : nullptr), 
            args.size() );

        wprintf( L"Assembly execution finished! Exit code: %i\n", retval );

    Cleanup:
        return hr;
    }
}; // end class ClrHost

