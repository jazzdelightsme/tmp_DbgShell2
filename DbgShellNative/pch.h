// pch.h: This is a precompiled header file.
// Files listed below are compiled only once, improving build performance for future builds.
// This also affects IntelliSense performance, including code completion and many code browsing features.
// However, files listed here are ALL re-compiled if any one of them is updated between builds.
// Do not add files here that you will be updating frequently as this negates the performance advantage.

#pragma once

// add headers that you want to pre-compile here

#define INITGUID

#define WIN32_LEAN_AND_MEAN             // Exclude rarely-used stuff from Windows headers
// Windows Header Files:
#include <windows.h>
#include <DbgEng.h>
#include <stdio.h>
#include <stdarg.h>
#include <cguid.h>
#include <atlbase.h>
#include <string>
#include <filesystem>
#include <vector>
#include <strsafe.h>


static WCHAR g_LastError[ 1024 ] = { 0 };


__inline
void DbgPrintf( const wchar_t* format, ... )
{
    wchar_t buffer[ 1024 ] = { 0 };

	va_list args;
	va_start( args, format );
	StringCchVPrintf( buffer, sizeof( buffer ) / sizeof( buffer[ 0 ] ), format, args );
	va_end( args );

	OutputDebugStringW( buffer );
}

__inline
void DbgPrintf_Error( const wchar_t* format, ... )
{
	va_list args;
	va_start( args, format );
	StringCchVPrintf( g_LastError, sizeof( g_LastError ) / sizeof( g_LastError[ 0 ] ), format, args );
	va_end( args );

	OutputDebugStringW( g_LastError );
}

