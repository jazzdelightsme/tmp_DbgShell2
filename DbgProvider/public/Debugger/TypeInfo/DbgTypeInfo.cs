﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DbgEngWrapper;
using Microsoft.Diagnostics.Runtime.Interop;

namespace MS.Dbg
{
    [DebuggerDisplay( "Type: {SymTag} Id {TypeId} Mod {m_modBase}" )]
    public class DbgTypeInfo : DebuggerObject, IEquatable< DbgTypeInfo >
    {
        //
        // The compiler has a set of base types which represent the lowest-level intrinsic
        // types in the type system. PDBs don't contain information for them, just
        // references to them. The following table defines the various base types which
        // the debugger might use and gives them artificial type IDs so that they can be
        // treated like other types.
        //

        protected const uint DBG_NATIVE_TYPE_BASE = 0x80000000;
        protected const uint DBG_GENERATED_TYPE_BASE = 0x80001000;

        protected enum DbgNativeType : uint
        {
            DNTYPE_VOID = DBG_NATIVE_TYPE_BASE,
            DNTYPE_CHAR,           // 0x80000001
            DNTYPE_WCHAR_T,        // 0x80000002
            DNTYPE_INT8,           // 0x80000003
            DNTYPE_INT16,          // 0x80000004
            DNTYPE_INT32,          // 0x80000005
            DNTYPE_INT64,          // 0x80000006
            DNTYPE_UINT8,          // 0x80000007
            DNTYPE_UINT16,         // 0x80000008
            DNTYPE_UINT32,         // 0x80000009
            DNTYPE_UINT64,         // 0x8000000a
            DNTYPE_FLOAT32,        // 0x8000000b
            DNTYPE_FLOAT64,        // 0x8000000c
            DNTYPE_FLOAT80,        // 0x8000000d
            DNTYPE_BOOL,           // 0x8000000e
            DNTYPE_LONG32,         // 0x8000000f
            DNTYPE_ULONG32,        // 0x80000010
            DNTYPE_HRESULT,        // 0x80000011

            //
            // The following types aren't true native types but are very basic aliases for
            // native types that need special identification. For example, WCHAR is here
            // so that the debugger knows it's characters and not just an unsigned short.
            //

            DNTYPE_WCHAR,          // 0x80000012

            //
            // Artificial type to mark cases where type information is coming from the
            // contained CLR value.
            //

            DNTYPE_CLR_TYPE,       // 0x80000013

            //
            // Artificial function type for CLR methods.
            //

            DNTYPE_MSIL_METHOD,    // 0x80000014
            DNTYPE_CLR_METHOD,     // 0x80000015
            DNTYPE_CLR_INTERNAL,   // 0x80000016

            //
            // Artificial pointer types for special-case handling of things like vtables.
            //

            DNTYPE_PTR_FUNCTION32, // 0x80000017
            DNTYPE_PTR_FUNCTION64, // 0x80000018

            //
            // Placeholder for objects that don't have valid type information but still
            // need to be represented for other reasons, such as enumeration.
            //

            DNTYPE_NO_TYPE,        // 0x80000019
            DNTYPE_ERROR,          // 0x8000001a

            DNTYPE_END_MARKER
        };


        /// <summary>
        ///    Indicates if a type id represents a type generated by dbgeng.
        /// </summary>
        internal static bool IsDbgGeneratedType( uint typeId )
        {
            return typeId >= DBG_GENERATED_TYPE_BASE;
        }

        protected static bool IsDbgNativeType( uint typeId )
        {
            return (typeId >= DBG_NATIVE_TYPE_BASE) && (typeId < DBG_GENERATED_TYPE_BASE);
        }


        private static DbgNamedTypeInfo _GetTypeInfoForDbgNativeType( DbgEngDebugger debugger,
                                                                      ulong moduleBase,
                                                                      uint typeId,
                                                                      DbgTarget target )
        {
            switch( (DbgNativeType) typeId )
            {
                case DbgNativeType.DNTYPE_VOID:
                    return new DbgBaseTypeInfo( debugger, moduleBase, typeId, BasicType.btVoid, 0, target );

                case DbgNativeType.DNTYPE_CHAR:
                    return new DbgBaseTypeInfo( debugger, moduleBase, typeId, BasicType.btChar, 1, target );

                case DbgNativeType.DNTYPE_WCHAR_T:
                    return new DbgBaseTypeInfo( debugger, moduleBase, typeId, BasicType.btWChar, 2, target );

                case DbgNativeType.DNTYPE_INT8:
                    return new DbgBaseTypeInfo( debugger, moduleBase, typeId, BasicType.btInt, 1, target );

                case DbgNativeType.DNTYPE_INT16:
                    return new DbgBaseTypeInfo( debugger, moduleBase, typeId, BasicType.btInt, 2, target );

                case DbgNativeType.DNTYPE_INT32:
                    return new DbgBaseTypeInfo( debugger, moduleBase, typeId, BasicType.btInt, 4, target );

                case DbgNativeType.DNTYPE_INT64:
                    return new DbgBaseTypeInfo( debugger, moduleBase, typeId, BasicType.btInt, 8, target );

                case DbgNativeType.DNTYPE_UINT8:
                    return new DbgBaseTypeInfo( debugger, moduleBase, typeId, BasicType.btUInt, 1, target );

                case DbgNativeType.DNTYPE_UINT16:
                    return new DbgBaseTypeInfo( debugger, moduleBase, typeId, BasicType.btUInt, 2, target );

                case DbgNativeType.DNTYPE_UINT32:
                    return new DbgBaseTypeInfo( debugger, moduleBase, typeId, BasicType.btUInt, 4, target );

                case DbgNativeType.DNTYPE_UINT64:
                    return new DbgBaseTypeInfo( debugger, moduleBase, typeId, BasicType.btUInt, 8, target );

                case DbgNativeType.DNTYPE_FLOAT32:
                    return new DbgBaseTypeInfo( debugger, moduleBase, typeId, BasicType.btFloat, 4, target );

                case DbgNativeType.DNTYPE_FLOAT64:
                    return new DbgBaseTypeInfo( debugger, moduleBase, typeId, BasicType.btFloat, 8, target );

                case DbgNativeType.DNTYPE_FLOAT80:
                    return new DbgBaseTypeInfo( debugger, moduleBase, typeId, BasicType.btFloat, 10, target );

                case DbgNativeType.DNTYPE_BOOL:
                    return new DbgBaseTypeInfo( debugger, moduleBase, typeId, BasicType.btBool, 1, target );

                case DbgNativeType.DNTYPE_LONG32:
                    return new DbgBaseTypeInfo( debugger, moduleBase, typeId, BasicType.btLong, 4, target );

                case DbgNativeType.DNTYPE_ULONG32:
                    return new DbgBaseTypeInfo( debugger, moduleBase, typeId, BasicType.btULong, 4, target );

                case DbgNativeType.DNTYPE_HRESULT:
                    return new DbgBaseTypeInfo( debugger, moduleBase, typeId, BasicType.btHresult, 4, target );

                case DbgNativeType.DNTYPE_WCHAR:
                    return new DbgBaseTypeInfo( debugger, moduleBase, typeId, BasicType.btWChar, 2, target );

                case DbgNativeType.DNTYPE_CLR_TYPE:
                    throw new NotImplementedException();

                case DbgNativeType.DNTYPE_MSIL_METHOD:
                    throw new NotImplementedException();

                case DbgNativeType.DNTYPE_CLR_METHOD:
                    throw new NotImplementedException();

                case DbgNativeType.DNTYPE_CLR_INTERNAL:
                    throw new NotImplementedException();

                case DbgNativeType.DNTYPE_PTR_FUNCTION32:
                    throw new NotImplementedException();

                case DbgNativeType.DNTYPE_PTR_FUNCTION64:
                    throw new NotImplementedException();

                case DbgNativeType.DNTYPE_NO_TYPE:
                    return new DbgBaseTypeInfo( debugger, moduleBase, typeId, BasicType.btNoType, 0, target );

                case DbgNativeType.DNTYPE_ERROR:
                    return new DbgBaseTypeInfo( debugger, moduleBase, typeId, BasicType.btNoType, 0, target );

                default:
                    throw new NotImplementedException( Util.Sprintf( "Unknown DbgNativeType: {0}", typeId ) );
            } // end switch( typeId )
        } // end _GetTypeInfoForDbgNativeType()


        protected static readonly Dictionary< DbgNativeType, string > sm_CompilerGeneratedTypeNames
            = new Dictionary< DbgNativeType, string >()
        {
            { DbgNativeType.DNTYPE_VOID, "void" },
            { DbgNativeType.DNTYPE_CHAR, "char" },
            { DbgNativeType.DNTYPE_WCHAR_T, "wchar_t" },
            { DbgNativeType.DNTYPE_INT8, "int8" },
            { DbgNativeType.DNTYPE_INT16, "int16" },
            { DbgNativeType.DNTYPE_INT32, "int32" },
            { DbgNativeType.DNTYPE_INT64, "int64" },
            { DbgNativeType.DNTYPE_UINT8, "uint8" },
            { DbgNativeType.DNTYPE_UINT16, "uint16" },
            { DbgNativeType.DNTYPE_UINT32, "uint32" },
            { DbgNativeType.DNTYPE_UINT64, "uint64" },
            { DbgNativeType.DNTYPE_FLOAT32, "float32" },
            { DbgNativeType.DNTYPE_FLOAT64, "float64" },
            { DbgNativeType.DNTYPE_FLOAT80, "float80" },
            { DbgNativeType.DNTYPE_BOOL, "bool" },
            { DbgNativeType.DNTYPE_LONG32, "long32" },
            { DbgNativeType.DNTYPE_ULONG32, "ulong32" },
            { DbgNativeType.DNTYPE_HRESULT, "hresult" },

            //
            // The following types aren't true native types but are very basic aliases for
            // native types that need special identification. For example, WCHAR is here
            // so that the debugger knows it's characters and not just an unsigned short.
            //

            { DbgNativeType.DNTYPE_WCHAR, "wchar" },

            //
            // Artificial type to mark cases where type information is coming from the
            // contained CLR value.
            //

            { DbgNativeType.DNTYPE_CLR_TYPE, "clr_type" },

            //
            // Artificial function type for CLR methods.
            //

            { DbgNativeType.DNTYPE_MSIL_METHOD, "msil_method" },
            { DbgNativeType.DNTYPE_CLR_METHOD, "clr_method" },
            { DbgNativeType.DNTYPE_CLR_INTERNAL, "clr_internal" },

            //
            // Artificial pointer types for special-case handling of things like vtables.
            //

            { DbgNativeType.DNTYPE_PTR_FUNCTION32, "ptr_function32" },
            { DbgNativeType.DNTYPE_PTR_FUNCTION64, "ptr_function64" },

            //
            // Placeholder for objects that don't have valid type information but still
            // need to be represented for other reasons, such as enumeration.
            //

            { DbgNativeType.DNTYPE_NO_TYPE, "no_type" },
            { DbgNativeType.DNTYPE_ERROR, "type_error" },
        };


    //  private WDebugSymbols m_ds;
    //  protected WDebugSymbols DS
    //  {
    //      get
    //      {
    //          if( null == m_ds )
    //          {
    //              Debugger.ExecuteOnDbgEngThread( () =>
    //                  {
    //                      m_ds = (WDebugSymbols) Debugger.DebuggerInterface;
    //                  } );
    //          }
    //          return m_ds;
    //      }
    //  } // end property DS


        private int m_symbolCookie;

        /// <summary>
        ///    The module's SymbolCookie value when the DbgTypeInfo object was created.
        ///    If the module's symbols get unloaded and reloaded, the module's
        ///    SymbolCookie value will be incremented (and this DbgTypeInfo's type id(s)
        ///    will no longer be valid).
        /// </summary>
        public int SymbolCookie { get { return m_symbolCookie; } }


        private bool _CheckValid()
        {
            // I don't /think/ that these ever go bad with symbol unloading/re-loading.
            if( IsDbgNativeType( TypeId ) )
                return true;

            // We could do Module.SymbolCookie here, but he's just going to ask the
            // target anyway.
            return m_symbolCookie == Target.GetSymbolCookie( m_modBase );
        } // end _CheckValid()


        protected void _EnsureValid()
        {
            if( !_CheckValid() )
            {
                // TODO: Should we have a specific exception type for this?
                throw new DbgProviderException( "The underlying type id has been invalidated (such as via symbol unload). You should discard and re-acquire the symbol or type information.",
                                                "TypeIdInvalidated",
                                                System.Management.Automation.ErrorCategory.InvalidData,
                                                this );
            }
        } // end _EnsureValid()


        protected readonly ulong m_modBase;
        protected DbgModuleInfo __mod;
        public DbgModuleInfo Module
        {
            get
            {
                if( null == __mod )
                {
                    __mod = new DbgModuleInfo( Debugger, m_modBase, Target );
                }
                return __mod;
            }
        } // end property Module

        public readonly uint TypeId;

        public readonly SymTag SymTag;

        protected uint[] GetChildrenIds()
        {
            _EnsureValid();
            return DbgHelp.FindTypeChildren( Debugger.DebuggerInterface, Module.BaseAddress, TypeId );
        } // end GetChildrenIds()

        protected uint[] GetChildrenIds( uint numChildren )
        {
            _EnsureValid();
            return DbgHelp.FindTypeChildren( Debugger.DebuggerInterface, Module.BaseAddress, TypeId, numChildren );
        } // end GetChildrenIds()


        private static DbgTypeInfo _DefaultFactory( DbgEngDebugger debugger,
                                                    DbgModuleInfo module,
                                                    uint typeId,
                                                    SymTag symTag )
        {
            return new DbgTypeInfo( debugger, module, typeId, symTag );
        }

        // Indexed by SymTag
        private static readonly Func< DbgEngDebugger, DbgModuleInfo, uint, DbgTypeInfo >[] sm_factories =
        {
            DbgNullTypeInfo.GetNullTypeInfo,                                      // SymTag.Null
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.Exe ),                    // SymTag.Exe
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.Compiland ),              // SymTag.Compiland
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.CompilandDetails ),       // SymTag.CompilandDetails
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.CompilandEnv ),           // SymTag.CompilandEnv
            DbgFunctionTypeInfo.GetFunctionTypeInfo,                              // SymTag.Function
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.Block ),                  // SymTag.Block
            DbgDataTypeInfo.GetDataTypeInfo,                                      // SymTag.Data
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.Annotation ),             // SymTag.Annotation
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.Label ),                  // SymTag.Label
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.PublicSymbol ),           // SymTag.PublicSymbol
            DbgUdtTypeInfo.GetUdtTypeInfo,                                        // SymTag.UDT
            DbgEnumTypeInfo.GetEnumTypeInfo,                                      // SymTag.Enum
            DbgFunctionTypeTypeInfo.GetFunctionTypeTypeInfo,                      // SymTag.FunctionType
            DbgPointerTypeInfo.GetPointerTypeInfo,                                // SymTag.PointerType
            DbgArrayTypeInfo.GetArrayTypeInfo,                                    // SymTag.ArrayType
            DbgBaseTypeInfo.GetBaseTypeInfo,                                      // SymTag.BaseType
            DbgTypedefTypeInfo.GetTypedefTypeInfo,                                // SymTag.Typedef
            DbgBaseClassTypeInfo.GetBaseClassTypeInfo,                            // SymTag.BaseClass
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.Friend ),                 // SymTag.Friend
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.FunctionArgType ),        // SymTag.FunctionArgType
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.FuncDebugStart ),         // SymTag.FuncDebugStart
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.FuncDebugEnd ),           // SymTag.FuncDebugEnd
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.UsingNamespace ),         // SymTag.UsingNamespace
            DbgVTableShapeTypeInfo.GetVTableShapeTypeInfo,                        // SymTag.VTableShape
            DbgVTableTypeInfo.GetVTableTypeInfo,                                  // SymTag.VTable
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.Custom ),                 // SymTag.Custom
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.Thunk ),                  // SymTag.Thunk
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.CustomType ),             // SymTag.CustomType
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.ManagedType ),            // SymTag.ManagedType
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.Dimension ),              // SymTag.Dimension
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.CallSite ),               // SymTag.CallSite
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.InlineSite ),             // SymTag.InlineSite
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.BaseInterface ),          // SymTag.BaseInterface
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.VectorType ),             // SymTag.VectorType
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.MatrixType ),             // SymTag.MatrixType
            (x,y,z) => _DefaultFactory( x, y, z, SymTag.HLSLType ),               // SymTag.HLSLType
        }; // end sm_factories


        //
        // Cache stuff
        //
        // Looking up type information is expensive. Caching DbgTypeInfo objects can cut
        // down the time to create values for large objects by > 50%.
        //
        //                         target context         modBase           typeId
        private static Dictionary< DbgTarget, Dictionary< ulong, Dictionary< uint, DbgTypeInfo > > > sm_typeCache = null;

        private static void _InitCache()
        {
            if( null != sm_typeCache )
                return;

            sm_typeCache = new Dictionary< DbgTarget, Dictionary< ulong, Dictionary< uint, DbgTypeInfo > > >();
            DbgEngDebugger._GlobalDebugger.UmProcessRemoved += _GlobalDebugger_ProcessRemoved;
            DbgEngDebugger._GlobalDebugger.KmTargetRemoved += _GlobalDebugger_KmTargetRemoved;
        } // end _InitCache()

        private static void _GlobalDebugger_ProcessRemoved( object sender, UmProcessRemovedEventArgs e )
        {
            foreach( var procToRemove in e.Removed )
            {
                sm_typeCache.Remove( procToRemove );
            } // end foreach( procToRemove )
        } // end _GlobalDebugger_ProcessRemoved()


        private static void _GlobalDebugger_KmTargetRemoved( object sender, KmTargetRemovedEventArgs e )
        {
            foreach( var targetToRemove in e.Removed )
            {
                // What if we have types cached under a more-specific context? Let's just
                // dump everything with a matching system id.

                var toRemove = sm_typeCache.Keys.Where(
                    (key) => key.DbgEngSystemId == targetToRemove.DbgEngSystemId ).ToList();

                foreach( var removeMe in toRemove )
                {
                    sm_typeCache.Remove( targetToRemove );
                }
            } // end foreach( targetToRemove )
        } // end _GlobalDebugger_KmTargetRemoved()


        private static TValue _GetCacheEntryOrCreate< TKey, TValue >( TKey key, Dictionary< TKey, TValue > cache )
            where TValue : new()
        {
            TValue val = default( TValue );
            if( !cache.TryGetValue( key, out val ) )
            {
                val = new TValue();
                cache.Add( key, val );
            }
            return val;
        } // end _GetCacheEntryOrCreate()

        private static object sm_syncRoot = new object();

        private static int sm_cacheHits;
        private static int sm_cacheMisses;

        public static int CacheHits { get { return sm_cacheHits; } }
        public static int CacheMisses { get { return sm_cacheMisses; } }

        public static void ResetCacheStatistics()
        {
            lock( sm_syncRoot )
            {
                sm_cacheHits = 0;
                sm_cacheMisses = 0;
            }
        } // end ResetCacheStatistics()

        public static void PurgeCache()
        {
            lock( sm_syncRoot )
            {
                if( null == sm_typeCache )
                    return;

                sm_typeCache.Clear();
            }
        } // end PurgeCache()

        public static int GetCacheSize()
        {
            lock( sm_syncRoot )
            {
                if( null == sm_typeCache )
                    return 0;

                int total = 0;
                foreach( var modCache in sm_typeCache.Values )
                {
                    foreach( var typeCache in modCache.Values )
                    {
                        total += typeCache.Count;
                    }
                }
                return total;
            }
        } // end GetCacheSize()

    //  // For debugging purposes only.
    //  public static Dictionary< DbgTarget, Dictionary< ulong, Dictionary< uint, DbgTypeInfo > > > GetCache()
    //  {
    //      return sm_typeCache;
    //  }

        private static ulong _AdjustModBase( ulong moduleBase,
                                             uint typeId,
                                             DbgTarget target )
        {
            // For the generated types, we'll just pick a module. We want this code in its
            // own function so that I don't break stuff if I end up changing it. (it has
            // to stay in sync between when we check the cache and when we add it to the
            // cache)
            // For the IsDbgGeneratedType case, I had originally written the following
            // comment: "TODO: temp workaround for generated types..." It seems to be
            // working out alright so far.
            if( (IsDbgNativeType( typeId ) || IsDbgGeneratedType( typeId )) &&
                (0 == moduleBase) )
            {
                return target.Modules[ 0 ].BaseAddress;
            }
            else
            {
                return moduleBase;
            }
        } // end _AdjustModBase(


        private static bool _TryGetFromCache( ulong moduleBase,
                                              uint typeId,
                                              DbgTarget target,
                                              out DbgTypeInfo typeInfo )
        {
            if( !String.IsNullOrEmpty( Environment.GetEnvironmentVariable( "__DisableTypeCache" ) ) )
            {
                typeInfo = null;
                return false;
            }

            lock( sm_syncRoot )
            {
                _InitCache();

                moduleBase = _AdjustModBase( moduleBase, typeId, target );
                var perProcModCache = _GetCacheEntryOrCreate( target, sm_typeCache );
                var perModTypeCache = _GetCacheEntryOrCreate( moduleBase, perProcModCache );
                bool retVal = perModTypeCache.TryGetValue( typeId, out typeInfo );

                if( retVal )
                {
                    // Before deciding if this is actually a hit, check that the type is
                    // still valid (maybe its symbols got reloaded since we cached it).
                    if( !typeInfo._CheckValid() )
                    {
                        retVal = false;
                        typeInfo = null;

                        // If it's not valid for this type, it's not valid for any other
                        // types in the module.
                        perProcModCache.Remove( moduleBase );
                    }
                }


                if( retVal )
                    sm_cacheHits++;
                else
                    sm_cacheMisses++;

             // LogManager.Trace( "DbgTypeInfo._TryGetFromCache: modbase {0}, typeId {1}: found? {2}",
             //                   Util.FormatQWord( moduleBase ),
             //                   typeId,
             //                   retVal );
                return retVal;
            }
        } // end _TryGetFromCache()

        private static void _AddToCache( DbgTypeInfo typeInfo )
        {
            if( !String.IsNullOrEmpty( Environment.GetEnvironmentVariable( "__DisableTypeCache" ) ) )
            {
                return;
            }

            lock( sm_syncRoot )
            {
                _InitCache();

             // LogManager.Trace( "DbgTypeInfo._AddToCache: modbase {0}, typeId {1}",
             //                   Util.FormatQWord( typeInfo.Module.BaseAddress ),
             //                   typeInfo.TypeId );
                var perProcCache = _GetCacheEntryOrCreate( typeInfo.Target, sm_typeCache );
                var perModCache = _GetCacheEntryOrCreate( typeInfo.Module.BaseAddress, perProcCache );
                // Two threads could have both had a cache miss for the same type and then
                // subsequently try to add the type to the cache, so we don't fail if it
                // already exists.
                //perModCache.Add( typeInfo.TypeId, typeInfo );
                perModCache[ typeInfo.TypeId ] = typeInfo;
            }
        } // end _AddToCache()

        public static DbgTypeInfo GetTypeInfo( DbgEngDebugger debugger,
                                               ulong moduleBase,
                                               uint typeId,
                                               DbgTarget target )
        {
            DbgTypeInfo typeInfo;
            moduleBase = _AdjustModBase( moduleBase, typeId, target );
            if( !_TryGetFromCache( moduleBase, typeId, target, out typeInfo ) )
            {
                typeInfo = _LoadTypeInfo( debugger, moduleBase, typeId, target );
                _AddToCache( typeInfo );
            }
            return typeInfo;
        }

        private static DbgTypeInfo _LoadTypeInfo( DbgEngDebugger debugger,
                                                  ulong moduleBase,
                                                  uint typeId,
                                                  DbgTarget target )
        {
            moduleBase = _AdjustModBase( moduleBase, typeId, target );
            if( IsDbgNativeType( typeId ) )
            {
                return _GetTypeInfoForDbgNativeType( debugger, moduleBase, typeId, target );
            }
            if( 0 == moduleBase )
            {
                throw new ArgumentException( "You must supply a valid module base address.", "moduleBase" );
            }
            return _LoadTypeInfo( debugger,
                                  new DbgModuleInfo( debugger, moduleBase, target ),
                                  typeId );
        } // end _LoadTypeInfo()


        public static DbgTypeInfo GetTypeInfo( DbgEngDebugger debugger,
                                               ulong moduleBase,
                                               uint typeId,
                                               SymTag symTag,
                                               DbgTarget target )
        {
            moduleBase = _AdjustModBase( moduleBase, typeId, target );
            return GetTypeInfo( debugger,
                                new DbgModuleInfo( debugger, moduleBase, target ),
                                typeId,
                                symTag );
        }

        public static DbgTypeInfo GetTypeInfo( DbgEngDebugger debugger,
                                               DbgModuleInfo module,
                                               uint typeId )
        {
            DbgTypeInfo typeInfo;
            if( !_TryGetFromCache( module.BaseAddress, typeId, module.Target, out typeInfo ) )
            {
                typeInfo = _LoadTypeInfo( debugger, module, typeId );
                _AddToCache( typeInfo );
            }
            return typeInfo;
        }

        private static DbgTypeInfo _LoadTypeInfo( DbgEngDebugger debugger,
                                                  DbgModuleInfo module,
                                                  uint typeId )
        {
            if( null == debugger )
                throw new ArgumentNullException( "debugger" );

            if( IsDbgGeneratedType( typeId ) &&
                !DbgHelp.PeekSyntheticTypeExists( debugger.DebuggerInterface,
                                                  module.BaseAddress,
                                                  typeId ) )
            {
                return new DbgGeneratedTypeInfo( debugger, module, typeId );
            }

            return debugger.ExecuteOnDbgEngThread( () =>
                {
                    var symTag = DbgHelp.GetSymTag( debugger.DebuggerInterface,
                                                    module.BaseAddress,
                                                    typeId );
                    return _LoadTypeInfo( debugger, module, typeId, symTag );
                } );
        }

        public static DbgTypeInfo GetTypeInfo( DbgEngDebugger debugger,
                                               DbgModuleInfo module,
                                               uint typeId,
                                               SymTag symTag )
        {
            DbgTypeInfo typeInfo;
            if( !_TryGetFromCache( module.BaseAddress, typeId, module.Target, out typeInfo ) )
            {
                typeInfo = _LoadTypeInfo( debugger, module, typeId, symTag );
                _AddToCache( typeInfo );
            }
            return typeInfo;
        }

        private static DbgTypeInfo _LoadTypeInfo( DbgEngDebugger debugger,
                                                  DbgModuleInfo module,
                                                  uint typeId,
                                                  SymTag symTag )
        {
            if( null == debugger )
                throw new ArgumentNullException( "debugger" );

            if( IsDbgGeneratedType( typeId ) &&
                !DbgHelp.PeekSyntheticTypeExists( debugger.DebuggerInterface,
                                                  module.BaseAddress,
                                                  typeId ) )
            {
                return new DbgGeneratedTypeInfo( debugger, module, typeId, symTag );
            }

            return debugger.ExecuteOnDbgEngThread( () =>
                {
                    if( ((int) symTag) > sm_factories.Length )
                    {
                        // In case they extend the SymTag enum and I don't get updated.
                        Util.Fail( "Need to update SymTag enum." );
                        return _DefaultFactory( debugger, module, typeId, symTag );
                    }

                    return sm_factories[ (int) symTag ]( debugger, module, typeId );
                } );
        } // end _LoadTypeInfo()


        private static DbgNamedTypeInfo _EnsureIsNamed( DbgTypeInfo dti )
        {
            if( !(dti is DbgNamedTypeInfo) )
            {
                // TODO: should we throw some sort of DbgProviderException here? The idea
                // is that it would be very helpful to actually include the type in question
                // (dti) as part of the error.
                throw new InvalidOperationException( Util.Sprintf( "The requested type id does not represent a named type (it's a {0}, symTag {1}).",
                                                                   Util.GetGenericTypeName( dti ),
                                                                   dti.SymTag ) );
            }

            return (DbgNamedTypeInfo) dti;
        } // end _EnsureIsNamed()


        public static DbgNamedTypeInfo GetNamedTypeInfo( DbgEngDebugger debugger,
                                                         ulong moduleBase,
                                                         uint typeId,
                                                         DbgTarget target )
        {
            return _EnsureIsNamed( GetTypeInfo( debugger, moduleBase, typeId, target ) );
        }

        public static DbgNamedTypeInfo GetNamedTypeInfo( DbgEngDebugger debugger,
                                                         ulong moduleBase,
                                                         uint typeId,
                                                         SymTag symTag,
                                                         DbgTarget target )
        {
            return _EnsureIsNamed( GetTypeInfo( debugger,
                                                moduleBase,
                                                typeId,
                                                symTag,
                                                target ) );
        }

        public static DbgNamedTypeInfo GetNamedTypeInfo( DbgEngDebugger debugger,
                                                         DbgModuleInfo module,
                                                         uint typeId )
        {
            return _EnsureIsNamed( GetTypeInfo( debugger, module, typeId ) );
        }

        public static DbgNamedTypeInfo GetNamedTypeInfo( DbgEngDebugger debugger,
                                                         DbgModuleInfo module,
                                                         uint typeId,
                                                         SymTag symTag )
        {
            return _EnsureIsNamed( GetTypeInfo( debugger, module, typeId, symTag ) );
        }

        public DbgTarget Target { get; private set; }

        protected DbgTypeInfo( DbgEngDebugger debugger,
                               ulong moduleBase,
                               uint typeId,
                               SymTag symTag,
                               DbgTarget target )
            : base( debugger )
        {
            m_modBase = moduleBase;
            TypeId = typeId;
            SymTag = symTag;

            if( null == target )
                throw new ArgumentNullException( "target" );

            Target = target;
            m_symbolCookie = Target.GetSymbolCookie( moduleBase );
        } // end constructor

        protected static ulong GetModBase( DbgModuleInfo module )
        {
            if( null == module )
                throw new ArgumentNullException( "module" );
            return module.BaseAddress;
        }

        protected static ulong GetTargetPointerSize( DbgEngDebugger debugger )
        {
            if( null == debugger )
                throw new ArgumentNullException( "debugger" );

            return debugger.TargetIs32Bit ? (ulong) 4 : (ulong) 8;
        }

        protected ulong GetTargetPointerSize()
        {
            return GetTargetPointerSize( Debugger );
        }

        protected DbgTypeInfo( DbgEngDebugger debugger,
                               DbgModuleInfo module,
                               uint typeId,
                               SymTag symTag )
            : this( debugger, GetModBase( module ), typeId, symTag, module.Target )
        {
            __mod = module;
        } // end constructor


        #region IEquatable<DbgTypeInfo> Stuff

        //
        // N.B. For most type info types, we only consider them equal if they actually
        // represent the same type id. However, for certain types--particularly UDTs--we
        // use a slightly looser definition of equality. This is because the linker tends
        // to duplicate some types. So, for instance, two UDT type objects are considered
        // "equal" if they come from the same module, have the same name, size, and number
        // of children.
        //
        // You can find which DbgTypeInfo classes have different definitions of "equality"
        // by finding their overloads of this .Equals method.
        //
        // Ideally I could use dbghelp's/DIA's TI_IS_EQUIV_TO to query which types are
        // equivalent. Unfortunately its current implementation doesn't really work.
        //

        public virtual bool Equals( DbgTypeInfo other )
        {
            if( null == other )
                return false;

            bool result = (m_modBase == other.m_modBase) &&
                          (TypeId == other.TypeId);

#if DEBUG
            if( result )
                Util.Assert( SymTag == other.SymTag );
#endif
            return result;
        } // end Equals()

        public override bool Equals( object obj )
        {
            return Equals( obj as DbgTypeInfo );
        }

        public override int GetHashCode()
        {
            return m_modBase.GetHashCode() + TypeId.GetHashCode();
        }

        public static bool operator ==( DbgTypeInfo t1, DbgTypeInfo t2 )
        {
            if( null == (object) t1 )
                return (null == (object) t2);

            return t1.Equals( t2 );
        }

        public static bool operator !=( DbgTypeInfo t1, DbgTypeInfo t2 )
        {
            return !(t1 == t2);
        }

        #endregion
    } // end class DbgTypeInfo
}
