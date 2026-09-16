namespace NightshadeLua;

/// <summary>
/// This class exists for the sole purpose of being `using static`'d in component APIs.
/// </summary>
public static class CommonValues
{
    public static LuaValue.None None { get; } = new();
    public static LuaValue.Nil Nil { get; } = new();
}