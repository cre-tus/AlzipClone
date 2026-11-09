# SFX stub binary

`SfxStub.exe` is the compiled single-file stub from `src/SfxStub`
(framework-dependent, needs .NET 8 runtime). It is committed like 7-Zip
ships `7zS.sfx`, so SFX creation works from a plain `git clone` + `dotnet
build` without a publish step.

Rebuild after changing the stub:

```powershell
dotnet publish src/SfxStub/SfxStub.csproj -c Release -o $env:TEMP/sfx-pub
Copy-Item $env:TEMP/sfx-pub/SfxStub.exe src/AlzipClone.Core/sfx/SfxStub.exe -Force
```
