namespace NightshadeLua;

public enum LuaResult
{
    /// <summary>
    /// Denotes the absence of an error.
    /// </summary>
    Ok = 0,
    
    /// <summary>
    /// Denotes that a coroutine has yielded.
    /// </summary>
    Yield = 1,
    
    /// <summary>
    /// Denotes a runtime error, such as one thrown by the error() method.
    /// </summary>
    ErrorRuntime = 2,
    
    /// <summary>
    /// Denotes a syntax error, will occur when trying to load
    /// a syntactically invalid or malformed chunk.
    /// </summary>
    ErrorSyntax = 3,
    
    /// <summary>
    /// Denotes a memory error, will occur if the Lua runtime does not have
    /// enough memory available to perform the required action.
    /// </summary>
    ErrorMemory = 4,
    
    /// <summary>
    /// Denotes an error occurring within an error handling procedure.
    /// </summary>
    ErrorError = 5
}