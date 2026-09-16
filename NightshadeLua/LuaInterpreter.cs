using System;
using System.Runtime.InteropServices;
using System.Text;
using NightshadeLua.Bindings;
using static NightshadeLua.Bindings.Lua;
using static NightshadeLua.Bindings.Lauxlib;
using static NightshadeLua.Bindings.Lualib;

namespace NightshadeLua;

public unsafe class LuaInterpreter : IDisposable
{
    public lua_State* State;
    
    public void LoadString(string code)
    {
        fixed (byte* f = Encoding.UTF8.GetBytes(code))
        {
            luaL_loadstring(State, (sbyte*)f);
        }
    }

    public void LoadBytes(byte[] codeArray, string chunkname, string mode = "bt")
    {
        int errorCode;
        fixed (byte* name = Encoding.UTF8.GetBytes(chunkname))
        fixed (byte* f = codeArray)
        fixed (byte* pMode = Encoding.UTF8.GetBytes(mode))
        {
            var fedTheChunk = false;
            sbyte* Reader(lua_State* L, void* ptr, nint* size)
            {
                if (!fedTheChunk)
                {
                    fedTheChunk = true;
                    *size = codeArray.Length;
                    return (sbyte*)ptr;
                }
                return (sbyte*)0;
            }

            var reader = Marshal.GetFunctionPointerForDelegate(Reader);
            errorCode = lua_load(State,
                (delegate* unmanaged[Cdecl]<lua_State*, void*, UIntPtr*, sbyte*>)reader,
                f, (sbyte*)name, (sbyte*)pMode);
        }

        switch (errorCode)
        {
            case 0: // LUA_OK
            {
                break;
            }
            case 3: // LUA_ERRSYNTAX
            {
                var err = LuaValue.From(State, -1);
                LuaUtil.Pop(State, 1);
                throw new LuaErrorException(err.ToString());
            }
            case 4: // LUA_ERRMEM
            {
                throw new OutOfMemoryException("got LUA_ERRMEM within Load");
            }
        }
    }

    public void Load(string code, string chunkname)
        => LoadBytes(Encoding.UTF8.GetBytes(code), chunkname, "t");

    public void Push(LuaValue v)
        => LuaValue.Push(State, v);
    
    public void SetGlobal(string name)
    {
        fixed (byte* f = Encoding.UTF8.GetBytes(name))
        {
            lua_setglobal(State, (sbyte*)f);
        }
    }

    public void SetGlobal(string name, LuaValue value)
    {
        PushValue(value);
        SetGlobal(name);
    }
    
    public void PushValue(LuaValue baseValue)
    {
        if (baseValue is LuaValue.None)
        {
            // it is impossible to push a none, so throw
            throw new InvalidOperationException("attempt to push lua none to stack");
        } else if (baseValue is LuaValue.Nil)
        {
            lua_pushnil(State);
        } else if (baseValue is LuaValue.Boolean boolean)
        {
            lua_pushboolean(State, boolean.Value ? 1 : 0);
        } else if (baseValue is LuaValue.LightUserdata lightuserdata)
        {
            lua_pushlightuserdata(State, (void*)lightuserdata.Value);
        } else if (baseValue is LuaValue.Number number)
        {
            lua_pushnumber(State, number.Value);
        } else if (baseValue is LuaValue.String @string)
        {
            var stringPtr = Marshal.StringToHGlobalAnsi(@string.Value);
            lua_pushstring(State, (sbyte*)stringPtr);
            Marshal.FreeHGlobal(stringPtr);
        } else if (baseValue is LuaValue.Table table)
        {
            // location on the stack of the new table
            var tableOffset = lua_gettop(State) + 1;
            // push a new blank table
            lua_createtable(State, 0, 0); // (== lua_newtable())
            foreach (var pair in table.Members)
            {
                // recursively push the key,value pair
                PushValue(pair.Key);
                PushValue(pair.Value);
                lua_settable(State, tableOffset);
            }
        } else if (baseValue is LuaValue.Function function)
        {
            // fetch the ref out of the function object. the function only gets unref'd when GC'd so we'll be fine.
            // if you are mixing function objects for ones to a different interpreter (which won't be ref'd),
            // ...then you very much won't be fine, though, so try not to do that.
            // I have no idea what happens then, but it probably leads to a segfault.
            lua_rawgeti(State, LuaUtil.RegistryIndex, function.@ref);
        } else if (baseValue is LuaValue.Delegate @delegate)
        {
            lua_pushcclosure(State, (delegate* unmanaged[Cdecl]<lua_State*, int>)@delegate.Address, 0);
        } else if (baseValue is LuaValue.Userdata)
        {
            // todo
            throw new NotImplementedException();
        }
    }
    
    public void Call(int argcount, int returncount)
    {
        var code = lua_pcallk(State, argcount, returncount, 0, 0, null);
        if (code > 0)
        {
            var f = LuaUtil.ToString(this, -1);
            throw new LuaErrorException(f);
        }
    }

    public LuaInterpreter(LuaLibrary libs = ~(LuaLibrary.Package | LuaLibrary.Io | LuaLibrary.Os))
    {
        State = luaL_newstate();
        if ((nint)State == 0)
            throw new Exception("Failed to initialise Lua interpreter state!");
        
        luaL_openselectedlibs(State, (int)libs, 0);
    }

    public void Dispose()
    {
        lua_close(State);
    }
}