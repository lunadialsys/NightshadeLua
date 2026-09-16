namespace NightshadeLua;

public enum LuaType
{
    /// <summary>
    /// No type. Used for an invalid value.
    /// </summary>
    None = -1,
    
    /// <summary>
    /// Represents a nil value.
    /// </summary>
    Nil,
    
    /// <summary>
    /// Boolean. Can be either true or false.
    /// </summary>
    Boolean,
    
    /// <summary>
    /// "Light userdata". Like userdata, but represents an unmanaged pointer.
    /// </summary>
    LightUserdata,
    
    /// <summary>
    /// A number. Represented as a double.
    /// </summary>
    Number,
    
    /// <summary>
    /// A string. Always UTF-8, represented as a byte*,
    /// will be automatically converted to and from managed strings.
    /// </summary>
    String,
    
    /// <summary>
    /// A table. Can hold many values.
    /// </summary>
    Table,
    
    /// <summary>
    /// A function, either defined in Lua code or native code.
    /// </summary>
    Function,
    
    /// <summary>
    /// A userdata object.
    /// </summary>
    Userdata,
    
    /// <summary>
    /// A thread object (aka a coroutine).
    /// </summary>
    Thread
}