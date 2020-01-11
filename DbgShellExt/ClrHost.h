#define INITGUID
#include "pch.h"

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

    path m_clrBasePath = L"C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App\\3.1.0";
    HMODULE m_hCoreClr = nullptr;

    ICLRRuntimeHost4* m_runtimeHost = nullptr;

    DWORD m_domainId = 0;

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
        //path coreClrPath = m_appBasePath;
        path coreClrPath = m_clrBasePath;
        coreClrPath.append( L"coreClr.dll" );

        DbgPrintf( L"coreClr path: %s\n", coreClrPath.c_str() );
        m_hCoreClr = LoadLibraryExW( coreClrPath.c_str(), nullptr, 0 );

        if (!m_hCoreClr)
        {
            DWORD dwLastErr = GetLastError();
            DbgPrintf_Error( L"Failed to load coreClr: %i\n", dwLastErr );

            return HRESULT_FROM_WIN32( dwLastErr );
        }


        FnGetCLRRuntimeHost pfnGetCLRRuntimeHost =
            (FnGetCLRRuntimeHost) ::GetProcAddress( m_hCoreClr, "GetCLRRuntimeHost" );

        if( !pfnGetCLRRuntimeHost )
        {
            DWORD dwLastErr = GetLastError();
            DbgPrintf_Error( L"Failed to find GetCLRRuntimeHost : %i\n", dwLastErr );

            return HRESULT_FROM_WIN32( dwLastErr );
        }

        // Get the hosting interface
        hr = pfnGetCLRRuntimeHost( IID_ICLRRuntimeHost4, (IUnknown**) &m_runtimeHost );

        if( FAILED( hr ) )
        {
            DbgPrintf_Error( L"Failed to get runtime host : %#x\n", hr );
            return hr;
        }

        hr = m_runtimeHost->SetStartupFlags(
            // These startup flags control runtime-wide behaviors.
            // A complete list of STARTUP_FLAGS can be found in mscoree.h,
            // but some of the more common ones are listed below.
            static_cast<STARTUP_FLAGS>(
                // STARTUP_FLAGS::STARTUP_SERVER_GC |                               // Use server GC
                // STARTUP_FLAGS::STARTUP_LOADER_OPTIMIZATION_MULTI_DOMAIN |        // Maximize domain-neutral loading
                // STARTUP_FLAGS::STARTUP_LOADER_OPTIMIZATION_MULTI_DOMAIN_HOST |   // Domain-neutral loading for strongly-named assemblies
                STARTUP_FLAGS::STARTUP_CONCURRENT_GC |                      // Use concurrent GC
                STARTUP_FLAGS::STARTUP_SINGLE_APPDOMAIN |                   // All code executes in the default AppDomain
                                                                            // (required to use the runtimeHost->ExecuteAssembly helper function)
                STARTUP_FLAGS::STARTUP_LOADER_OPTIMIZATION_SINGLE_DOMAIN    // Prevents domain-neutral loading
            )
        );

        if( FAILED( hr ) )
        {
            DbgPrintf_Error( L"Failed to set startup flags : %#x\n", hr );
            return hr;
        }

        hr = m_runtimeHost->Start();

        if( FAILED( hr ) )
        {
            DbgPrintf_Error( L"Failed to start the runtime : %#x\n", hr );
            return hr;
        }

        if( createNewAppDomain )
        {
            int appDomainFlags =
                // APPDOMAIN_FORCE_TRIVIAL_WAIT_OPERATIONS |        // Do not pump messages during wait
                APPDOMAIN_ENABLE_PLATFORM_SPECIFIC_APPS |           // Enable platform-specific assemblies to run
                APPDOMAIN_ENABLE_PINVOKE_AND_CLASSIC_COMINTEROP |   // Allow PInvoking from non-TPA assemblies
                APPDOMAIN_DISABLE_TRANSPARENCY_ENFORCEMENT;         // Entirely disables transparency checks

            // TRUSTED_PLATFORM_ASSEMBLIES
            // "Trusted Platform Assemblies" are prioritized by the loader and always loaded with full trust.
            // A common pattern is to include any assemblies next to CoreCLR.dll as platform assemblies.
            // More sophisticated hosts may also include their own Framework extensions (such as AppDomain managers)
            // in this list.
            size_t tpaSize = 100 * MAX_PATH; // Starting size for our TPA (Trusted Platform Assemblies) list
            wchar_t* trustedPlatformAssemblies = new wchar_t[tpaSize];
            trustedPlatformAssemblies[0] = L'\0';

            // Extensions to probe for when finding TPA list files
            const wchar_t *tpaExtensions[] = {
                L"*.dll",
                L"*.exe",
                L"*.winmd"
            };

            // Probe next to CoreCLR.dll for any files matching the extensions from tpaExtensions and
            // add them to the TPA list. In a real host, this would likely be extracted into a separate function
            // and perhaps also run on other directories of interest.
            for (int i = 0; i < _countof(tpaExtensions); i++)
            {
                // Construct the file name search pattern
                wchar_t searchPath[MAX_PATH];
                //wcscpy_s(searchPath, MAX_PATH, m_appBasePath.c_str());
                wcscpy_s(searchPath, MAX_PATH, m_clrBasePath.c_str());
                wcscat_s(searchPath, MAX_PATH, L"\\");
                wcscat_s(searchPath, MAX_PATH, tpaExtensions[i]);

                // Find files matching the search pattern
                WIN32_FIND_DATAW findData;
                HANDLE fileHandle = FindFirstFileW(searchPath, &findData);

                if (fileHandle != INVALID_HANDLE_VALUE)
                {
                    do
                    {
                        // Construct the full path of the trusted assembly
                        wchar_t pathToAdd[MAX_PATH];
                        wcscpy_s(pathToAdd, MAX_PATH, m_appBasePath.c_str());
                        wcscat_s(pathToAdd, MAX_PATH, L"\\");
                        wcscat_s(pathToAdd, MAX_PATH, findData.cFileName);

                        // Check to see if TPA list needs expanded
                        if (wcsnlen(pathToAdd, MAX_PATH) + (3) + wcsnlen(trustedPlatformAssemblies, tpaSize) >= tpaSize)
                        {
                            // Expand, if needed
                            tpaSize *= 2;
                            wchar_t* newTPAList = new wchar_t[tpaSize];
                            wcscpy_s(newTPAList, tpaSize, trustedPlatformAssemblies);
                            trustedPlatformAssemblies = newTPAList;
                        }

                        // Add the assembly to the list and delimited with a semi-colon
                        wcscat_s(trustedPlatformAssemblies, tpaSize, pathToAdd);
                        wcscat_s(trustedPlatformAssemblies, tpaSize, L";");

                        // Note that the CLR does not guarantee which assembly will be loaded if an assembly
                        // is in the TPA list multiple times (perhaps from different paths or perhaps with different NI/NI.dll
                        // extensions. Therefore, a real host should probably add items to the list in priority order and only
                        // add a file if it's not already present on the list.
                        //
                        // For this simple sample, though, and because we're only loading TPA assemblies from a single path,
                        // we can ignore that complication.
                    }
                    while (FindNextFileW(fileHandle, &findData));
                    FindClose(fileHandle);
                }
            }


            // APP_PATHS
            // App paths are directories to probe in for assemblies which are not one of the well-known Framework assemblies
            // included in the TPA list.
            //
            // For this simple sample, we just include the directory the target application is in.
            // More complex hosts may want to also check the current working directory or other
            // locations known to contain application assets.
            wchar_t appPaths[MAX_PATH * 50];

            // TODO: Debugger dir?
            wcscpy_s(appPaths, m_clrBasePath.c_str());
            wcscat_s(appPaths, MAX_PATH * 50, L";");
            wcscat_s(appPaths, MAX_PATH * 50, m_appBasePath.c_str());

            const wchar_t* runtimeDirs[] = {
                L"runtimes\\win\\lib\\netcoreapp3.1",
                L"runtimes\\win\\lib\\netcoreapp3.0",
                L"runtimes\\win\\lib\\netcoreapp2.1",
                L"runtimes\\win\\lib\\netcoreapp2.0",
                L"runtimes\\win\\lib\\netstandard2.0",
                L"runtimes\\win\\lib\\netstandard1.6",
                L"runtimes\\win10-x64\\lib\\netstandard1.6",
            };

            for (auto runtimeDir : runtimeDirs)
            {
                path p = m_appBasePath;
                p.append(runtimeDir);
                wcscat_s(appPaths, MAX_PATH * 50, L";");
                wcscat_s(appPaths, MAX_PATH * 50, p.c_str());
            }


            // APP_NI_PATHS
            // App (NI) paths are the paths that will be probed for native images not found on the TPA list.
            // It will typically be similar to the app paths.
            // For this sample, we probe next to the app and in a hypothetical directory of the same name with 'NI' suffixed to the end.
            wchar_t appNiPaths[MAX_PATH * 50] = { 0 };


            // NATIVE_DLL_SEARCH_DIRECTORIES
            // Native dll search directories are paths that the runtime will probe for native DLLs called via PInvoke
            wchar_t nativeDllSearchDirectories[MAX_PATH * 50];
            wcscpy_s(nativeDllSearchDirectories, appPaths);
            wcscat_s(nativeDllSearchDirectories, MAX_PATH * 50, L";");
            wcscat_s(nativeDllSearchDirectories, MAX_PATH * 50, m_clrBasePath.c_str());
            wcscat_s(nativeDllSearchDirectories, MAX_PATH * 50, L";");
            wcscat_s(nativeDllSearchDirectories, MAX_PATH * 50, m_appBasePath.c_str());


            // PLATFORM_RESOURCE_ROOTS
            // Platform resource roots are paths to probe in for resource assemblies (in culture-specific sub-directories)
            wchar_t platformResourceRoots[MAX_PATH * 50];
            wcscpy_s(platformResourceRoots, appPaths);



            // Setup key/value pairs for AppDomain  properties
            const wchar_t* propertyKeys[] = {
                L"TRUSTED_PLATFORM_ASSEMBLIES",
                L"APP_PATHS",
                L"APP_NI_PATHS",
                L"NATIVE_DLL_SEARCH_DIRECTORIES",
                L"PLATFORM_RESOURCE_ROOTS"
            };

            // Property values which were constructed in step 5
            const wchar_t* propertyValues[] = {
                trustedPlatformAssemblies,
                appPaths,
                appNiPaths,
                nativeDllSearchDirectories,
                platformResourceRoots
            };

            // Create the AppDomain
            hr = m_runtimeHost->CreateAppDomainWithManager(
                L"DbgShellExt Host AppDomain",  // Friendly AD name
                appDomainFlags,
                NULL,                           // Optional AppDomain manager assembly name
                NULL,                           // Optional AppDomain manager type (including namespace)
                sizeof(propertyKeys) / sizeof(wchar_t*),
                propertyKeys,
                propertyValues,
                &m_domainId);

            if( FAILED( hr ) )
            {
                DbgPrintf_Error( L"Failed to create the appdomain : %#x\n", hr );
                return hr;
            }
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

        component_entry_point_fn fn = 0;
        hr = m_runtimeHost->CreateDelegate( m_domainId,
                                            L"DbgShell, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                                            L"MS.DbgShell.MainClass",
                                            L"MainForNativeHost",
                                            (INT_PTR*) &fn );
        if( FAILED( hr ) )
        {
            wprintf( L"Failed to create the delegate: %#x\n", hr );
            goto Cleanup;
        }

        retval = fn(
            (LPCWSTR*) ((args.size() > 0) ? args.data() : nullptr), 
            args.size() );

        wprintf( L"Assembly execution finished! Exit code: %i\n", retval );

    Cleanup:
        return hr;
    }
}; // end class ClrHost

