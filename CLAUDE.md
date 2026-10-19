# Mosaic

A self-hosted control plane for servers and containers. .NET 10, C# 14, an ASP.NET Core API and an
Avalonia desktop client.

It ships as a free core and a paid pro layer built from a separate private repository. **Core must
compile and run with pro absent from the disk**, so no file here may name a pro type.

## The projects

| Project | Holds | References |
|---|---|---|
| `Mosaic.Domain` | entities, typed constants, errors | nothing |
| `Mosaic.Contracts` | wire shapes, the `ApiResponse` envelope, the vocabulary the desktop names | nothing |
| `Mosaic.Application` | services, catalogues, registries, the interfaces infrastructure implements | Domain, Contracts |
| `Mosaic.Infrastructure` | EF Core, persistence configurations, DI registration | Application |
| `Mosaic.Api` | controllers, middleware, the exception handler | Application, Infrastructure |
| `Mosaic.Desktop` | the shell, screens, themes and strings — a **library** | Contracts only |
| `Mosaic.Desktop.App` | `Program.cs` and the manifest: the core-only entry point | Desktop |

The desktop is a library because two entry points need it and an executable cannot be referenced as
the shell by another executable. Pro's entry point is its twin in the private repository, and the two
differ by one line: which composition is built.

## Running it locally

The database is a container, not a local install:

```
docker compose up -d
```

Postgres 18 on **127.0.0.1:5433**, database `mosaic`. The port is deliberately not 5432, so a
connection made by mistake never lands on whatever else is listening.

```
dotnet run --project src/Mosaic.Api
dotnet run --project src/Mosaic.Desktop.App
```

The desktop reads `MOSAIC_API_URL` when it is set and falls back to its built-in address otherwise.

## Build and test

Run both **from the repository root**. `global.json` is found from the working directory, not from the
solution file, so running from a project folder picks a different SDK.

```
dotnet build
dotnet test
```

**Never pass `--nologo`.** With Microsoft.Testing.Platform it is forwarded to the runner, which prints
its help and reports zero tests with exit code 5.

Warnings are errors and the analysers are strict. Zero warnings is the bar, not a target.

Tests are xUnit v3 on Microsoft.Testing.Platform, selected in `global.json`. Not VSTest.

## Packages and lock files

Versions live in `Directory.Packages.props`, never in a csproj — central package management is on, with
transitive pinning. A csproj names the package, not the version.

**`PrivateAssets` on a `PackageVersion` is ignored.** It belongs on the `PackageReference`. Getting this
wrong once shipped Roslyn, MSBuild and EF Design inside the API and took it from 8 MB to 33 MB.

Every project has a committed `packages.lock.json` and CI restores with `--locked-mode`. **A package
change means committing the lock files in the same commit**, or CI fails on a stale lock rather than
regenerating it.

## Migrations

```
dotnet ef migrations add <Name> --project src/Mosaic.Infrastructure --output-dir Persistence/Migrations
```

**Infrastructure is its own startup project.** Passing `--startup-project src/Mosaic.Api` fails, because
the API does not reference `Microsoft.EntityFrameworkCore.Design` and must not — carrying it shipped
Roslyn, MSBuild and the design tools inside the API and took its output from 8 MB to 33 MB. Infrastructure
owns the design-time factory, so it needs no other project.

**`--output-dir Persistence/Migrations` is not optional.** Without it EF writes to a `Migrations/` folder
at the project root, away from the ones already there.

`add` and `remove` never open a connection, so they work with no database running. Anything that does
connect needs the connection string **in the environment** — the design-time factory reads that and never
`appsettings.json`:

```
ConnectionStrings__Management="Host=127.0.0.1;Port=5433;Database=mosaic;Username=mosaic;Password=mosaic-dev-only" \
  dotnet ef database update --project src/Mosaic.Infrastructure
```

Without the variable the factory falls back to a credential-free placeholder aimed at 5433, so a command
that connects by mistake cannot reach a real server.

**`dotnet ef` defaults to the Debug output.** Passing `--no-build` after a Release build tests a stale
assembly, which makes a fixed bug look unfixed.

`migrations add` refuses a duplicate name. To redo one, delete its three files first — and if you want the
migration to keep its identity rather than appear as a new one, rename the regenerated files and the
`[Migration(...)]` attribute back afterwards.

## Comments

**One line or none.** Match the file next to the one you are editing — most have no comments at all.

- Never write `/// <summary>`. This codebase has none.
- Never write two adjacent `//` lines. A paragraph comment is the tell.
- A comment records what a reader would be surprised without: undocumented framework behaviour, a
  deliberate absence, a non-obvious ordering constraint. It never restates the name below it.
- **Never cite a planning document, a decision number, a task number or a milestone.** A reader may not
  have it, it rots the moment anything is renumbered, and it makes the source read as generated. State
  the mechanism or the consequence instead.
- When a comment explains a deliberate absence, name the failure it prevents.

Argues the design, and a reader cannot check any of it:

```csharp
// A record and not an enum, because the pro assembly declares its own values against this same type.
```

States the fact, and what breaks without it:

```csharp
// not an enum, pro adds its own keys and the core builds without pro.
```

## Tests

Tests ship in the same commit as the code they cover.

**A guard is not trusted until it has been proved by breaking it**: introduce the fault, watch the right
test fail, then revert. Green means nothing until you have seen red. Every drift guard here was proved
that way and should stay that way.

The drift guards are load-bearing. They check that every resource and style class the XAML asks for
exists, that the palettes declare the same names, that no colour literal sits outside them, that every
error code has a sentence in all three languages, that the model matches the migration, and that the
shell names no feature.

## Architecture

**The client displays, the server decides.** The desktop renders flattened per-screen shapes, shows a
control when a flag on the row says so, and computes no business fact. Permission, ownership and licence
decisions live only on the server.

**Errors cross the wire as a code, never as a sentence.** The client translates. A message on an error is
optional, English, diagnostic, and never shown to a user.

Rich entities with private setters and factory methods. Typed exceptions carrying a code at the boundary.
Hand-written static mappers. Injected `TimeProvider`, never `DateTime.UtcNow` inline.
`Guid.CreateVersion7()` for every id. EF Core is the data layer, not something to wrap.

**Rejected and staying rejected:** MediatR, CQRS, repositories over EF Core, AutoMapper, Hangfire, Redis,
SignalR, and a DI container in the desktop.

## Desktop

- Compiled bindings are on and the csproj says so explicitly. Every view declares `x:DataType`.
- No view locator. `App.axaml` registers a `DataTemplate` per screen, so a missing view is a compile
  error rather than a blank rectangle at runtime. A missing template renders `ToString()` silently.
- **The shell names no feature.** Only the composition and `App.axaml` may, and a test enforces it.
- `Localizer` is immutable. Choosing a language builds a new one and a new shell, and
  `MainWindowViewModel` swaps the shell in. Nothing re-translates itself in place.
- Colours live only in the palette files, and a test fails the build on a literal anywhere else.

## Traps that have already cost hours

**`HorizontalAlignment="Stretch"` with a `MaxWidth` centres the element.** Avalonia's arrange pass treats
Stretch and Center as one branch, so it drifts right as the window grows. There is no "fill up to N,
anchored left" — use a fixed width with `HorizontalAlignment="Left"`.

**The embedded strings need `WithCulture="false"`.** Without it the SDK reads `Strings.de.json` as a
German satellite assembly and moves it out of the main DLL. The build stays clean and the language
disappears from the picker.

**FluentIcons is pinned and guarded.** `FluentIcons.Common 2.1.339` throws from its own static
initializer on .NET 10, so the version is overridden and a build target fails the build on any bump, to
force a re-check.

**A `{DynamicResource}` type mismatch does not crash.** It silently does nothing, and the symptom is a
control that looks unstyled.

## Files

Source files are **CRLF**. A scripted edit must preserve the terminators — reading a file in a way that
normalises newlines and writing it back silently rewrites the whole file. Never add a BOM to a file that
does not have one.

## Commits

- Never mention AI assistance in a message, and never add a co-author trailer.
- Subject line, then two or three lines of **why**. Never a file list.
