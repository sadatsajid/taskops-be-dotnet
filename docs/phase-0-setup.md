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

## Local Tooling

Restore repo-local tools after cloning:

```bash
dotnet tool restore
```

Install Git hooks once:

```bash
dotnet husky install
```

Husky.Net uses `.husky/task-runner.json` for hook tasks. The current contract is:

- `pre-commit`: verify formatting without restoring packages.
- `pre-push`: restore, build, and run the PostgreSQL-backed integration tests.

Docker must be running before `pre-push` because the tests use Testcontainers.
