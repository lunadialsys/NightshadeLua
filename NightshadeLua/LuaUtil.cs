using System;
using System.Runtime.InteropServices;
using System.Text;
using NightshadeLua.Bindings;
using static NightshadeLua.Bindings.Lua;

namespace NightshadeLua;

public static unsafe class LuaUtil
{
    public delegate int LuaDelegate(lua_State* L);
    public delegate int KFunction(lua_State* L, int status, nint ctx);

    public const int RegistryIndex = (-(int.MaxValue / 2 + 1000));

    public static LuaResult LoadBytes(lua_State* L, byte[] code, string chunkname, string mode = "t")
    {
        int errorCode;
        fixed (byte* f = code)
        fixed (byte* name = Encoding.UTF8.GetBytes(chunkname))
        fixed (byte* pMode = Encoding.UTF8.GetBytes(mode))
        {
            var fedTheChunk = false;
            sbyte* Reader(lua_State* _L, void* ptr, nint* size)
            {
                if (!fedTheChunk)
                {
                    fedTheChunk = true;
                    *size = code.Length;
                    return (sbyte*)ptr;
                }
                return (sbyte*)0;
            }

            var reader = Marshal.GetFunctionPointerForDelegate(Reader);
            errorCode = lua_load(L,
                (delegate* unmanaged[Cdecl]<lua_State*, void*, UIntPtr*, sbyte*>)reader,
                f, (sbyte*)name, (sbyte*)pMode);
        }

        return (LuaResult)errorCode;
    }
    
    public static void Pop(lua_State* L, int count)
    {
        lua_settop(L, -(count)-1);
    }
    
    public static void Pop(LuaInterpreter L, int count)
        => Pop(L.State, count);

    public static string ToBasicString(lua_State* L, int idx)
    {
        var s = lua_tolstring(L, idx, null);
        var st = Marshal.PtrToStringUTF8((nint)s);
        return st;
    }

    public static string ToBasicString(LuaInterpreter L, int idx)
        => ToBasicString(L.State, idx);
    
    public static string ToString(lua_State* L, int idx)
    {
        var s = Lauxlib.luaL_tolstring(L, idx, null);
        var st = Marshal.PtrToStringUTF8((nint)s);
        Pop(L, 1); // need to pop it because luaL_tolstring pushes the string onto the stack
        return st;
    }

    public static string ToString(LuaInterpreter L, int idx)
        => ToString(L.State, idx);
    
    public static void Remove(lua_State* L, int n)
    {
        lua_rotate(L, n, -1);
        Pop(L, 1);
    }

    public static void Remove(LuaInterpreter L, int idx)
        => Remove(L.State, idx);
    
    public static int Gettop(lua_State* L)
    {
        return lua_gettop(L);
    }
    
    public static int Gettop(LuaInterpreter L)
        => Gettop(L.State);

    public static string CheckString(lua_State* L, int arg)
    {
        var a = Lauxlib.luaL_checklstring(L, arg, null);
        if ((nint)a == 0) return null;
        var st = Marshal.PtrToStringUTF8((nint)a);
        return st;
    }

    public static string CheckString(LuaInterpreter L, int arg)
        => CheckString(L.State, arg);
}