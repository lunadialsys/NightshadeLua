# NightshadeLua
Somewhat-coarse managed bindings to [the Lua programming language](https://lua.org), for .NET.

## An important note
This library is made for a single purpose (an in-development prototype of a video game that heavily utilises Lua).
It's released under an open-source license in the hope it will be useful to others.
Please don't expect to receive great support, or to have your PRs or issues looked at in a timely fashion.
Also don't expect the API surface to be perfect, or for the library to not cause AccessViolationExceptions.

Both libraries are built for .NET 8.0 (target `net8.0`), because that's the target that Godot Engine's C# integration compiles against (as of version `4.7.2`).

## Usage
To generate the bindings:
- Ensure you have ClangSharpPInvokeGenerator in your PATH.
- Fetch the source tarball for your target version of Lua from lua.org.
- Place `lua.h`, `lualib.h`, and `lauxlib.h` from the source distribution in the `generation` directory.
- Run the `generate.ps1` PowerShell script.
- You may need to manually correct `NightshadeLua.Bindings/Lua.cs` to remove an erroneous reference to `long double`, which does not exist in C#.
  - I tried to fix this and couldn't, sorry.
    - If you manage to divine the correct arcane incantation to make ClangSharpPInvokeGenerator emit the right types, please open a pull request.
  - Replacing it with `double` will make it compile.

Note that you don't have to regenerate the bindings; there are bindings already generated. They were generated against Lua 5.5.1.

The API surface of the main `NightshadeLua` namespace is a very fragile moving target, and as such, undocumented. Good luck.

## License
MIT