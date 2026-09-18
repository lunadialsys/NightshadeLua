using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NightshadeLua.Bindings;
using static NightshadeLua.Bindings.Lua;

namespace NightshadeLua;

public unsafe partial record LuaValue
{
    public virtual LuaType Type => LuaType.None;

    public static LuaValue From(lua_State* L, int idx)
        => (LuaType)lua_type(L, idx) switch
        {
            LuaType.None => new None(),
            LuaType.Nil => new Nil(),
            LuaType.Boolean => Boolean.From(L, idx),
            LuaType.LightUserdata => LightUserdata.From(L, idx),
            LuaType.Number => Number.From(L, idx),
            LuaType.String => String.From(L, idx),
            LuaType.Table => Table.From(L, idx),
            LuaType.Function => Function.From(L, idx),
            LuaType.Userdata => Userdata.From(L, idx), // FIXME AWFUL NATIVE CRASH HERE!
            LuaType.Thread => Coroutine.From(L, idx),
            var f => throw new InvalidOperationException($"LuaValue.From called on bogus type {f}")
        };
    
    public static LuaValue From(LuaInterpreter i, int idx)
        => From(i.State, idx);

    public static LuaType TypeOf(lua_State* L, int idx)
        => (LuaType)lua_type(L, idx);

    public static LuaType TypeOf(LuaInterpreter i, int idx)
        => TypeOf(i.State, idx);
    
    public static implicit operator string(LuaValue v) => v switch
    {
        String s => s.Value,
        _ => throw new InvalidOperationException($"attempted to coerce LuaValue of type {v.Type} to string")
    };
    public static implicit operator LuaValue(string v) => new String(v);
    
    public static implicit operator bool(LuaValue v) => v switch
    {
        Boolean s => s.Value,
        _ => throw new InvalidOperationException($"attempted to coerce LuaValue of type {v.Type} to boolean")
    };
    public static implicit operator LuaValue(bool v) => new Boolean(v);
    
    public static implicit operator double(LuaValue v) => v switch
    {
        Number s => s.Value,
        _ => throw new InvalidOperationException($"attempted to coerce LuaValue of type {v.Type} to number")
    };
    public static implicit operator LuaValue(double v) => new Number(v);
    
    public static void Push(lua_State* L, LuaValue baseValue)
    {
        if (baseValue is None)
        {
            // it is impossible to push a none, so throw
            throw new InvalidOperationException("attempt to push lua none to stack");
        } else if (baseValue is Nil)
        {
            lua_pushnil(L);
        } else if (baseValue is Boolean boolean)
        {
            lua_pushboolean(L, boolean.Value ? 1 : 0);
        } else if (baseValue is LightUserdata lightuserdata)
        {
            lua_pushlightuserdata(L, (void*)lightuserdata.Value);
        } else if (baseValue is Number number)
        {
            lua_pushnumber(L, number.Value);
        } else if (baseValue is String @string)
        {
            var stringPtr = Marshal.StringToHGlobalAnsi(@string.Value);
            lua_pushstring(L, (sbyte*)stringPtr);
            Marshal.FreeHGlobal(stringPtr);
        } else if (baseValue is ByteArray byteArray)
        {
            fixed (byte* p = byteArray.Value)
            {
                lua_pushstring(L, (sbyte*)p);
            }
        } else if (baseValue is Table table)
        {
            // location on the stack of the new table
            var tableOffset = lua_gettop(L) + 1;
            // push a new blank table
            lua_createtable(L, 0, 0); // (== lua_newtable())
            foreach (var pair in table.Members)
            {
                // recursively push the key,value pair
                Push(L, pair.Key);
                Push(L, pair.Value);
                lua_settable(L, tableOffset);
            }
        } else if (baseValue is Function function)
        {
            // fetch the ref out of the function object. the function only gets unref'd when GC'd so we'll be fine.
            // if you are mixing function objects for ones to a different interpreter (which won't be ref'd),
            // ...then you very much won't be fine, though, so try not to do that.
            // I have no idea what happens then, but it probably leads to a segfault.
            lua_rawgeti(L, LuaUtil.RegistryIndex, function.@ref);
        } else if (baseValue is Delegate @delegate)
        {
            lua_pushcclosure(L, (delegate* unmanaged[Cdecl]<lua_State*, int>)@delegate.Address, 0);
        } else if (baseValue is WrappedUserdata wrappedUserdata)
        {
            wrappedUserdata.CreateAndMarkAlive(L);
        } else if (baseValue is Userdata userdata)
        {
            userdata.Create(L);
        }
    }

    // ReSharper disable MemberHidesStaticFromOuterClass, this is on purpose.
    public record None : LuaValue
    {
        public override LuaType Type => LuaType.None;
    }

    public record Nil : LuaValue
    {
        public override LuaType Type => LuaType.Nil;
    }

    public record Boolean(bool Value) : LuaValue
    {
        public override LuaType Type => LuaType.Boolean;

        public static implicit operator bool(Boolean v) => v.Value;
        public static implicit operator Boolean(bool v) => new(v);

        public new static Boolean From(lua_State* L, int idx)
        {
            var t = lua_toboolean(L, idx);
            return new(t == 1);
        }
    }

    public record LightUserdata(nint Value) : LuaValue
    {
        public override LuaType Type => LuaType.LightUserdata;
        
        public new static LightUserdata From(lua_State* L, int idx)
        {
            var t = lua_touserdata(L, idx);
            return (nint)t != 0 ? new((nint)t) : null;
        }
    }
    
    public record Number(double Value) : LuaValue
    {
        public override LuaType Type => LuaType.Number;

        public static implicit operator double(Number v) => v.Value;
        public static implicit operator Number(double v) => new(v);
        
        public new static Number From(lua_State* L, int idx)
        {
            int ok = 0;
            var t = lua_tonumberx(L, idx, &ok);
            return ok == 1 ? new(t) : null;
        }
    }

    public record String(string Value) : LuaValue
    {
        public override LuaType Type => LuaType.String;

        public static implicit operator string(String v) => v.Value;
        public static implicit operator String(string v) => new(v);

        public override string ToString() => Value;

        public new static String From(lua_State* L, int idx)
        {
            var ptr = (nint)lua_tolstring(L, idx, null);
            if (ptr == 0) return null;
            var r = Marshal.PtrToStringUTF8(ptr);
            return new(r);
        }
    }

    /// <summary>
    /// Marshalled as a string into Lua. Cannot be retrieved from LuaValue.From.
    /// </summary>
    public record ByteArray(byte[] Value) : LuaValue
    {
        public override LuaType Type => LuaType.String;
    }
    
    public record Table : LuaValue
    {
        public override LuaType Type => LuaType.Table;

        public Table(Dictionary<LuaValue, LuaValue> k)
        {
            Members = k;
        }

        public LuaValue Get(LuaValue key)
        {
            if (!Members.TryGetValue(key, out var value))
                return new Nil();
            return value;
        }

        public Dictionary<LuaValue, LuaValue> Members { get; }

        public static Table FromArray(LuaValue[] array)
        {
            var contents = new Dictionary<LuaValue, LuaValue>();
            for (int i = 0; i < array.Length; i++)
            {
                // remember, arrays-as-table indices start at 1
                contents[new Number(i + 1)] = array[i];
            }
            return new Table(contents);
        }

        public new static Table From(lua_State* L, int idx)
        {
            var contents = new Dictionary<LuaValue, LuaValue>();
            // push first key (nil means 'the first one', I guess)
            lua_pushnil(L);
            // lua_next's behaviour is to:
            //  - pop the key (@ idx -1)
            //  - then push a (value,key) pair onto the stack (in that order),
            //    so that the stack looks like:
            //         0: --TOP--
            //        -1: value
            //        -2: key
            //        -3: ...
            while (lua_next(L, idx) != 0)
            {
                // retrieve k-v pair
                var value = LuaValue.From(L, -1);
                var key = LuaValue.From(L, -2);
                contents[key] = value;
                // pop the value, leave the key; lua_next will consume it next iteration
                LuaUtil.Pop(L, 1);
            }
            return new Table(contents);
        }
    }
    
    public record Function(int @ref) : LuaValue
    {
        public override LuaType Type => LuaType.Function;

        private nint state;

        ~Function()
        {
            // if we must, unref it when this object gets destructed (to unpin the corresponding object from the lua GC)
            if (state != 0)
                Lauxlib.luaL_unref((lua_State*)state, LuaUtil.RegistryIndex, @ref);
        }
        
        public new static Function From(lua_State* L, int idx)
        {
            if (lua_type(L, idx) != (int)LuaType.Function)
                throw new InvalidOperationException("not a function");
            lua_pushnil(L); // push dummy nil
            lua_copy(L, idx, -1); // copy the value to the dummy nil
            var theRef = Lauxlib.luaL_ref(L, LuaUtil.RegistryIndex);
            // value will be popped by the luaL_ref call
            return new Function(theRef) { state = (nint)L };
        }
    }

    /// <summary>
    /// There are restrictions to what you can do inside a Lua delegate!
    /// Do NOT return without making sure the right number of objects are on the interpreter's stack!
    /// Do NOT assume thread-safety (use `lock`s)!
    /// And especially do NOT allow the object the delegate is on to be GC'd while Lua is using it!
    /// Heed my advice or have three generations of your family fall victim to a deadly curse!
    /// </summary>
    public record Delegate : LuaValue
    {
        public override LuaType Type => LuaType.Function;

        public LuaUtil.LuaDelegate Method;
        public nint Address;

        public Delegate(LuaUtil.LuaDelegate del)
        {
            Method = del;
            Address = Marshal.GetFunctionPointerForDelegate(del);
        }
    }
    
    public record Userdata : LuaValue
    {
        public override LuaType Type => LuaType.Userdata;

        public ulong Size;
        
        /// <summary>
        /// Setting this member yourself serves no purpose.
        /// This member will only be set after the userdata is pushed to the stack.
        /// </summary>
        // ReSharper disable once NotAccessedField.Global, see above doc comment.
        public nint Pointer = -1;

        public string MetatableType;
        public Table Metatable;
        
        /// <summary>
        /// Keys MUST be in the range of {1 .. count}, otherwise the library will misbehave.
        /// </summary>
        public Dictionary<int, LuaValue> Members;

        public void Create(lua_State* L)
        {
            // create the userdata
            Pointer = (nint)lua_newuserdatauv(L, (nuint)Size, Members.Count);
            Unsafe.InitBlock((void*)Pointer, 0, (uint)Size);
            
            // put the values out of Members into it
            foreach (var pair in Members)
            {
                Push(L, pair.Value);
                lua_setiuservalue(L, -2, pair.Key);
            }
            
            // copy the metatable & tag it with some more info
            var newMetaMembers = new Dictionary<LuaValue, LuaValue>
            {
                ["__nightshadelua_MemberCount"] = Members.Count,
                ["__nightshadelua_MetaType"] = MetatableType
            };
            foreach (var pair in Metatable.Members)
            {
                newMetaMembers[pair.Key] = pair.Value;
            }
            var newMetatable = new Table(newMetaMembers);
            
            // push & set the userdata's metatable
            Push(L, newMetatable);
            lua_setmetatable(L, -2);
            
            // done, the topmost item on the stack will now be the userdata
        }
        
        public new static Userdata From(lua_State* L, int idx)
        {
            var hasMeta = lua_getmetatable(L, idx) == 1;
            if (!hasMeta)
                throw new InvalidOperationException("no metatable, is this a NightshadeLua userdata?");

            var metatable = Table.From(L, -1);
            LuaUtil.Pop(L, 1); // pop the metatable off the stack after we retrieve it

            var memberCountValue = (Number)metatable.Get("__nightshadelua_MemberCount");
            var metaTypeValue = (String)metatable.Get("__nightshadelua_MetaType");

            var memberCount = (int)memberCountValue.Value;
            var metaType = metaTypeValue.Value;

            var members = new Dictionary<int, LuaValue>();
            for (var i = 1; i <= memberCount; i++)
            {
                // put the datum on the top of the stack...
                var valueType = (LuaType)lua_getiuservalue(L, idx, i);
                if (valueType is LuaType.None)
                    throw new InvalidOperationException($"was unable to retrieve userdata member @ i={i}");
                // retrieve it...
                var datum = LuaValue.From(L, -1);
                // then pop it
                LuaUtil.Pop(L, 1);
                // and then copy it into the members table
                members[i] = datum;
            }
            
            // then, we can retrieve the native data
            var sz = lua_rawlen(L, idx);
            var ptr = lua_touserdata(L, idx);
            
            return new Userdata
            {
                Size = sz,
                Pointer = (nint)ptr,
                MetatableType = metaType,
                Metatable = metatable,
                Members = members
            };
        }
    }

    public partial record WrappedUserdata : Userdata
    {
        // this type's main implementation lives in LuaValue.WrappedUserdata.cs because it's massive.
        // (seriously, this one record would increase the code size of this file by half.)
    }

    public record Coroutine : LuaValue
    {
        public override LuaType Type => LuaType.Thread;

        public required nint StatePointer;

        public new static Coroutine From(lua_State* L, int idx)
        {
            var state = lua_tothread(L, idx);
            return new Coroutine { StatePointer = (nint)state };
        }
    }
    // ReSharper restore MemberHidesStaticFromOuterClass
}