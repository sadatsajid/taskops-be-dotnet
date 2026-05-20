# Phase 0 Setup Notes

TaskOps targets .NET 10 LTS.

## Verified Locally

```bash
/usr/local/share/dotnet/dotnet --version
docker --version
git --version
```

Current results:

- .NET SDK: 10.0.300
- Docker: 29.4.0
- Git: 2.50.1

## Local PATH Note

The .NET SDK is installed, but `dotnet` is not currently on the shell PATH in this environment.

For this machine, add the SDK directory to your shell profile:

```bash
export PATH="/usr/local/share/dotnet:$PATH"
```

Until that is added, run .NET commands with:

```bash
/usr/local/share/dotnet/dotnet
```

The repository includes `global.json` to pin the SDK to `10.0.300`.
