Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

function Generate() {
    $generationDir = Join-Path -Path "." -ChildPath "generation"
    Push-Location $generationDir
    ClangSharpPInvokeGenerator "@lauxlib.rsp"
    ClangSharpPInvokeGenerator "@lualib.rsp"
    ClangSharpPInvokeGenerator "@lua.rsp"
    Pop-Location
}

Generate
