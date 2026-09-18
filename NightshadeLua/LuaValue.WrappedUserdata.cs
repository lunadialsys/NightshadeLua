using System;
using System.Collections.Generic;
using System.Reflection;
using NightshadeLua.Bindings;

namespace NightshadeLua;

// we define this in its own part to not bloat the code size of LuaValue.cs too much.
// some people may consider this bad, so sue me.
public partial record LuaValue
{
    /// <summary>
    /// A nicer interface to producing userdata types that are shaped like tables of functions.
    /// Note that this is intended to be used with types you keep around in Lua,
    /// and that do not pass back through the managed boundary.
    /// (This means "if you LuaValue.From a WrappedUserData, you'll get a regular LuaValue.Userdata instead".)
    ///
    /// Please also note that WrappedUserdata do not actually behave like tables of functions.
    /// The references returned by their __index metamethod are unique, so hooking methods from Lua will
    /// only work on *that specific* instance of the object, and not on any others.
    /// </summary>
    public abstract unsafe partial record WrappedUserdata
    {
        // ==[!!! THERE IS BESPOKE MEMORY MANAGEMENT HAPPENING HERE, BE CAREFUL !!!]==
        // This dictionary exists to keep around a reference to the object, to ensure it does not get GC'd
        // by the CLR's garbage collector before it has had a chance to get GC'd by Lua, which would inevitably
        // lead to the dead object becoming a dangling pointer, and thus, causing a segfault when Lua next calls a method on it.
        private static readonly Dictionary<Guid, WrappedUserdata> _gcInsurance = new();

        // Used to maintain a marking of object liveness across the managed-to-native bound.
        // DO NOT *EVER* TOUCH THIS, OR THE LIVENESS SYSTEM WILL GET VERY CONFUSED.
        private readonly Guid _gcId;
        
        protected abstract string TypeName { get; }
        
        private struct DispatchMethodInfo
        {
            public MethodInfo target;
            public LuaType[] args;
        }
        private Dictionary<string, DispatchMethodInfo> methodCache;
        
        public WrappedUserdata()
        {
            _gcId = Guid.NewGuid();
            
            methodCache = new Dictionary<string, DispatchMethodInfo>();
            
            Metatable = new Table(new()
            {
                ["__index"] = new Delegate(_Index),
                ["__tostring"] = new Delegate(_Tostring),
                ["__gc"] = new Delegate(_Gc)
            });
            
            var myMethods = GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var method in myMethods)
            {
                if (method.GetCustomAttribute<MemberMethodAttribute>() is { } attr)
                {
                    var name = attr.Name;
                    if (method.ReturnType != typeof(LuaValue))
                        throw new InvalidOperationException(
                            $"{GetType().Name}#{method.Name} does not return LuaValue");
                    var methodParams = method.GetParameters();
                    var args = new LuaType[methodParams.Length];
                    var i = 0;
                    foreach (var param in methodParams)
                    {
                        var t = param.ParameterType;
                        if (!t.IsAssignableTo(typeof(LuaValue)))
                            throw new InvalidOperationException(
                                $"Parameter {param.Name} of {GetType().Name}#{method.Name} is not a LuaValue");
                        
                        // bad-looking code inbound! but afaik there is no better way to do this.. what a sad world we live in.
                        LuaType theType = LuaType.None; // just to please the compiler (it's exhaustive)
                        if (t == typeof(LuaValue) || t == typeof(None) || t == typeof(Nil))
                            throw new InvalidOperationException(
                                $"Nonsense type encountered on parameter {param.Name} of {GetType().Name}#{method.Name}");
                        if (t == typeof(Boolean))
                            theType = LuaType.Boolean;
                        else if (t == typeof(LightUserdata))
                            theType = LuaType.LightUserdata;
                        else if (t == typeof(Number))
                            theType = LuaType.Number;
                        else if (t == typeof(String))
                            theType = LuaType.String;
                        else if (t == typeof(Table))
                            theType = LuaType.Table;
                        else if (t == typeof(Function))
                            theType = LuaType.Function;
                        else if (t == typeof(Userdata))
                            theType = LuaType.Userdata;
                        else if (t == typeof(Coroutine))
                            theType = LuaType.Thread;
                        args[i++] = theType;
                    }
                    methodCache[name] = new DispatchMethodInfo
                    {
                        target = method,
                        args = args
                    };
                }
            }
        }

        internal void CreateAndMarkAlive(lua_State* L)
        {
            // we do this here because we can't call a virtual getter in the ctor
            MetatableType = TypeName;
            
            Create(L);
            
            // this object is now alive, store a reference to it if one isn't already there
            if (!_gcInsurance.ContainsKey(_gcId))
                _gcInsurance[_gcId] = this;
        }

        private int _Gc(lua_State* L)
        {
            // object is about to die, mark it as safe to collect from .NET
            if (_gcInsurance.ContainsKey(_gcId))
                _gcInsurance.Remove(_gcId);
            else // should be impossible
                throw new InvalidOperationException("__gc called on a WrappedUserdata that never became alive, what?");
            return 0;
        }

        private int _Tostring(lua_State* L)
        {
            Push(L, $"{TypeName}: {Pointer}");
            return 1;
        }

        private int _Index(lua_State* L)
        {
            // key is the LAST argument of the index metamethod
            var argument = LuaValue.From(L, -1);
            if (argument is not String procName)
            {
                Push(L, "expected index to be string");
                Lua.lua_error(L);
                return 0;
            }

            if (!methodCache.ContainsKey(procName))
            {
                Push(L, CommonValues.Nil);
                return 1;
            }

            var d = new Delegate(_L => _Dispatch(_L, procName.Value));
            Push(L, d);
            return 1;
        }

        private int _Dispatch(lua_State* L, string proc)
        {
            // TODO: we cannot do this because LuaValue.From on a userdata causes a native crash.
            // FIXME!!!!!!!!
            
            // user code may care about what the first argument is, let's block early.
            // (this is one of the little white lies this type tells the interpreter.)
            // var first = LuaValue.From(L, 1);
            // if (first is not Userdata ud ||
            //     ud.MetatableType != TypeName)
            // {
            //     Push(L, $"{TypeName}.{proc} expects instance as the first argument");
            //     Lua.lua_error(L);
            //     return 0;
            // }
            
            var dmi = methodCache[proc];
            
            var argCount = LuaUtil.Gettop(L) - 1;
            if (argCount != dmi.args.Length)
            {
                Push(L, $"{TypeName}.{proc} expects {dmi.args.Length} args (got {argCount})");
                Lua.lua_error(L);
                return 0;
            }
            
            var arguments = new LuaValue[dmi.args.Length];
            for (int idx = 1; idx <= argCount; idx++)
            {
                var theArg = LuaValue.From(L, idx + 1);
                var expectedType = dmi.args[idx - 1];
                if (theArg.Type != expectedType)
                {
                    Push(L, $"{TypeName}.{proc} argument {idx}: expected {expectedType}, got {theArg.Type}");
                    Lua.lua_error(L);
                    return 0;
                }
                arguments[idx - 1] = theArg;
            }

            try
            {
                // ReSharper disable once CoVariantArrayConversion
                // this doesn't matter as we won't run into covariance problems,
                // we've already made sure the argument types are proper.
                var returnValue = (LuaValue)dmi.target.Invoke(this, arguments);
                if (returnValue is null || returnValue is None)
                    return 0; // none return
                Push(L, returnValue);
                return 1;
            }
            catch (LuaErrorException ex) // if the target throws a LuaErrorException, report it as a lua error
            {
                Push(L, ex.Message);
                Lua.lua_error(L);
                return 0;
            }
        }
    }
}
