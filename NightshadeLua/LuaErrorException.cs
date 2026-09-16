using System;

namespace NightshadeLua;

public class LuaErrorException : Exception
{
    public override string Message { get; }

    public LuaErrorException(string msg)
    {
        Message = msg;
    }
}