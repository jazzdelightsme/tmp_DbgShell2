using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.InteropServices;

namespace DotNetLoadingShim
{
    internal static unsafe class Shim
    {
        static MethodInfo m_mi;
        static object[] m_parameters = new object[ 2 ];

        [UnmanagedCallersOnly]
        internal static unsafe int BounceToDefaultALC( IntPtr nativeArgs, int numArgs )
        {
            if( null == m_mi )
            {
                string dir = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location );
                string dbgShellBin = Path.Combine( dir, "DbgShell.dll" );

                Assembly dbgShellAsm = AssemblyLoadContext.Default.LoadFromAssemblyPath( dbgShellBin );

                Type mainClassType = dbgShellAsm.GetType( "MS.DbgShell.MainClass" );

                m_mi = mainClassType.GetMethod( "MainForNativeHost", BindingFlags.NonPublic | BindingFlags.Static );
            }

            m_parameters[ 0 ] = nativeArgs;
            m_parameters[ 1 ] = numArgs;

            return (int) m_mi.Invoke( null, m_parameters );
        }
    }
}
