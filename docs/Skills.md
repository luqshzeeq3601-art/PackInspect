# Skills – what the coding agent may invoke

The commands, tools and endpoints an AI coding agent is authorised to use in
this repository. Anything not listed here needs the owner's approval first.

Shell: PowerShell on Windows, run from the repository root.

## Allowed without asking

| Tool | Commands | Purpose |
|---|---|---|
| .NET SDK | `dotnet build`, `dotnet test`, `dotnet format`, `dotnet run`, `dotnet restore`, `dotnet clean`, `dotnet new` (inside the repo), `dotnet sln`, `dotnet --list-sdks` | Build, test, lint, run, scaffold |
| .NET SDK | `dotnet list package`, `dotnet list package --vulnerable` | Inspect dependencies |
| .NET SDK | `dotnet tool restore` | Restore the repo-local tools listed in `dotnet-tools.json` (dotnet-ef) |
| Git (local, read) | `git status`, `git diff`, `git log`, `git show`, `git branch` | Inspect the repository |
| File tools | Read, search, create and edit files inside the repository | Normal development |

The exact build, test and lint invocations are in [AGENTS.md](../AGENTS.md#commands).

## Ask first

| Action | Why |
|---|---|
| `dotnet add package` or any new entry in `Directory.Packages.props` | Licence must be checked and recorded first |
| `dotnet ef migrations add`, `dotnet ef database update` | Changes the schema; `Data.md` must change with it |
| `git add`, `git commit`, `git tag` | The owner decides when to commit |
| `git push`, `gh` (any GitHub CLI command) | Publishes outside this machine |
| Downloading a model or dataset | Large files, licence and attribution |
| Installing or updating a tool or SDK | Changes the machine |
| Deleting files that the current task did not create | Risk of data loss |

## Never

- Administrator or system-wide installs; changing system or security settings.
- `git push --force`, `git reset --hard`, `git clean`, history rewrites.
- Reading or writing outside the repository, except the NuGet package cache
  that `dotnet restore` manages.
- Printing, logging or committing a secret, password or token.
- Weakening, skipping or deleting a test to make a run pass.
- Starting anything that listens on an address other than `127.0.0.1`.

## Endpoints

| Endpoint | Protocol | Use | Access |
|---|---|---|---|
| `127.0.0.1:5020` | Modbus TCP | Simulated PLC and axes | Allowed |
| `127.0.0.1:5025` | SCPI over TCP | Simulated multimeter | Allowed |
| `127.0.0.1:5030` | Camera protocol over TCP | Simulated camera | Allowed |
| `127.0.0.1:5080` | HTTP | Optional result publisher; only contacted when `FabTrace:Enabled` is true, which it is not by default | Allowed |
| `https://api.nuget.org/v3-flatcontainer/` | HTTPS, read-only | Package versions and nuspec licence text; also used by `dotnet restore` | Allowed |
| `https://learn.microsoft.com/` | HTTPS, read-only | Official .NET documentation | Allowed |
| `https://huggingface.co/` | HTTPS | DINOv2 ONNX model (phase 5) | Reading model metadata allowed; downloads ask first |
| `https://registry.opendata.aws/visa` | HTTPS | VisA dataset (phase 5) | Ask first |
| `https://github.com/` | HTTPS | Repository hosting and Actions (phase 4) | Ask first |

No other network endpoint is used by the app, the simulator or the tests.
