# Building and testing

The solution targets .NET Framework 4.8 and `CAIME.csproj` is a legacy non-SDK
project, so it builds with Visual Studio's MSBuild. `dotnet build` will not
build it.

Run these from the repository root, in a Developer Command Prompt or Developer
PowerShell for Visual Studio:

```
nuget restore CampaignMapToolkit.sln
dotnet restore CAIME.Tests\CAIME.Tests.csproj
msbuild CampaignMapToolkit.sln /p:Configuration=Release
vstest.console.exe CAIME.Tests\bin\Release\net48\CAIME.Tests.dll
```

`CAIME.Tests` is an SDK-style project, which is why it needs the second restore
alongside the solution-wide `nuget restore`.

Build and test in `Release`, the configuration CI uses. For a `Debug` build the
test assembly is at `CAIME.Tests\bin\Debug\net48\CAIME.Tests.dll`.

To run a subset while working:

```
vstest.console.exe CAIME.Tests\bin\Release\net48\CAIME.Tests.dll /Tests:BordersGenerator
```

These are the steps `.github/workflows/ci.yml` runs, so a green local run means
a green CI run.
