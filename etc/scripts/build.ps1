[string]$workspace = "$PSScriptRoot\..\.."
[string]$configuration = "Release"

& dotnet restore "$workspace\src\"

If ($LASTEXITCODE -ne 0) {
    Throw "Restore command failed with code $LASTEXITCODE"
}

& dotnet build "$workspace\src\" --configuration $configuration

If ($LASTEXITCODE -ne 0) {
    Throw "Build command failed with code $LASTEXITCODE"
}

& dotnet test "$workspace\src\Community.AspNetCore.JsonRpc.FunctionalTests\Community.AspNetCore.JsonRpc.FunctionalTests.csproj" --configuration $configuration

If ($LASTEXITCODE -ne 0) {
    Throw "Test command failed with code $LASTEXITCODE"
}

& dotnet pack "$workspace\src\Community.AspNetCore.JsonRpc\Community.AspNetCore.JsonRpc.csproj" --no-build --configuration $configuration

If ($LASTEXITCODE -ne 0) {
    Throw "Pack command failed with code $LASTEXITCODE"
}