using System;

namespace NightshadeLua;

[Flags]
public enum LuaLibrary
{
    Base = 1 << 0,
    Package = 1 << 1,
    Coroutine = 1 << 2,
    Debug = 1 << 3,
    Io = 1 << 4,
    Math = 1 << 5,
    Os = 1 << 6,
    String = 1 << 7,
    Table = 1 << 8,
    Utf8 = 1 << 9,
    
    All = Base | Package | Coroutine | Debug | Io | Math | Os | String | Table | Utf8
}