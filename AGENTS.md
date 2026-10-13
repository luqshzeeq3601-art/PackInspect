# PackInspect – agent instructions

Control software for a **simulated** semiconductor package inspection and test
station. Portfolio project: the owner must be able to explain every line in an
interview, so clarity beats cleverness.

## Read before working

| Document | Read when |
|---|---|
| [docs/ARCHITECTURE-ESSENTIALS.md](docs/ARCHITECTURE-ESSENTIALS.md) | Always. The rules that must never be broken |
| [docs/PRD.md](docs/PRD.md) | Deciding what to build or whether something is in scope |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Adding a project, package, device or cross-cutting behaviour |
| [docs/Data.md](docs/Data.md) | Touching a table, a register, a SCPI command or a payload |
| [docs/Skills.md](docs/Skills.md) | Before running a command you have not run in this repo |
| [docs/Security.md](docs/Security.md) | Touching users, passwords, input parsing, files or the network |

Each fact lives in one document. Update that document in the same change that
makes it untrue; never copy a fact into a second file.

## Commands

Run from the repository root in PowerShell.

```powershell
dotnet build PackInspect.slnx                                  # build (warnings are errors)
dotnet test --solution PackInspect.slnx                        # all tests
dotnet test --solution PackInspect.slnx --filter-not-trait "Category=Integration"   # unit tests only
dotnet format PackInspect.slnx --verify-no-changes             # lint / formatting check
dotnet format PackInspect.slnx                                 # apply formatting
dotnet run --project src/PackInspect.Simulator                 # start the simulator first
dotnet run --project src/PackInspect.App                       # then the WPF app
dotnet tool restore                                            # once per clone: restores dotnet-ef
dotnet ef migrations add <Name> --project src/PackInspect.Data --startup-project src/PackInspect.Data   # after a schema change
```

Tests use xUnit v3 on Microsoft.Testing.Platform (selected in `global.json`), so
`dotnet test` needs `--solution`; the older `dotnet test PackInspect.slnx` form
fails on the .NET 10 SDK.

## Definition of done

A change is done only when all three pass, in this order:

1. `dotnet build PackInspect.slnx` with zero warnings.
2. `dotnet test --solution PackInspect.slnx` with zero failures.
3. `dotnet format PackInspect.slnx --verify-no-changes` with exit code 0.

If a check could not run, say so and say why. Never report a check as passed
unless you saw it pass.

## Hard constraints

- **C# on .NET 10.** Do not retarget. `global.json` pins the SDK major version.
- **Zero cost.** Only free, open-source packages. Before adding any NuGet
  package, read its licence from the package nuspec (not from memory), add the
  version to `Directory.Packages.props`, and record the licence in the table in
  `docs/ARCHITECTURE.md`. The same applies to models and datasets.
- **Never use FluentAssertions, MediatR or AutoMapper.**
- **No real hardware exists.** Never write or imply that anything was validated
  on a real machine. The README and all docs say it is a simulation.
- ML.NET and ONNX Runtime are **phase 5 only**.

## Workflow

- Work is split into six phases, listed in `docs/PRD.md`. Do one phase at a time.
- At the end of a phase: run the definition of done, show how to run the result,
  explain the main concepts in plain language, then **stop and wait** for the
  owner. Do not start the next phase unasked.
- Explain each key design decision as it is made; the owner will be asked about
  it in interviews.
- Do exactly what the phase needs. Mention useful extras instead of building them.

## Coding standards

- File-scoped namespaces, one public type per file, async methods end in `Async`.
- Constructor injection through `Microsoft.Extensions.DependencyInjection`. No
  service locator, no static mutable state.
- Comments explain *why* (a machine, safety or protocol reason), not *what*.
- Prefer the .NET base library over a new package.
- Tests use xUnit's built-in `Assert`. Test names read as sentences:
  `Door_open_while_running_raises_alarm`.
- Integration tests carry `[Trait("Category", "Integration")]`.
- Tests wait on events with a timeout; no fixed `Task.Delay` or `Thread.Sleep`
  to "let things settle".

## Error handling

- A device fault is a normal event, not a crash: it becomes an alarm with a code
  from `AlarmCodes` and moves the machine to the Alarm state.
- Catch exceptions at the device boundary and where an activity is started; do
  not catch and ignore. Every caught exception is logged with Serilog.
- `OperationCanceledException` means "asked to stop": let it propagate, never
  turn it into an alarm.
- Validate everything read from a device, a file or the user before using it.
- Fix the cause of a failing test. Never weaken, skip or delete a test to make a
  run pass.

## Commits

- Commit only when the owner asks. Never push, force-push or rewrite history
  without being asked.
- Conventional Commits: `type(scope): summary` in the imperative, 72 characters
  or fewer. Types: `feat`, `fix`, `test`, `docs`, `refactor`, `build`, `ci`,
  `chore`. Scope is the project or area: `core`, `devices`, `data`, `simulator`,
  `app`, `legacy`, `tests`, `docs`.
- One logical change per commit. The definition of done passes at every commit.
- Never commit secrets, databases, logs, datasets or model files.

## Boundaries

- Stay inside this repository. Do not install global tools or change system
  settings.
- Do not download models or datasets, add packages or contact any endpoint not
  listed in `docs/Skills.md` without asking first.
