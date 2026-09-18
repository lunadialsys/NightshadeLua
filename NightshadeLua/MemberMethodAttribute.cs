using System;

namespace NightshadeLua;

/// <summary>
/// When using WrappedUserdata, tag your member methods with this to register them into the type.
/// (Methods without this attribute on them will not appear within Lua.)
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class MemberMethodAttribute : Attribute
{
    public string Name;

    public MemberMethodAttribute(string name)
    {
        Name = name;
    }
}