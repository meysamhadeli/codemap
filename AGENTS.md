# codemap Contributor Guide

## Project Overview

codemap is a C# developer tool for packaging source code into AI-friendly output. The current implementation is intentionally small and extensible.

## Repository Structure

The repository uses a solution with these boundaries:

- `src/Codemap.Core/`: reusable discovery, transformation, and rendering pipeline.
- `src/Codemap.Cli/`: command-line host and argument mapping.
- `tests/Codemap.Tests/Unit/`: focused core behavior tests.
- `tests/Codemap.Tests/Integration/`: process-level CLI tests.
- `docs/`: user and architecture documentation when behavior becomes stable.

Do not add generated build output to source control.

## Tech Stack

- C# and .NET 10, using the SDK version declared by `Directory.Build.props` or `global.json`.
- Prefer built-in .NET APIs and small focused abstractions before adding dependencies.
- Keep command-line concerns separate from core processing logic so the core can be reused by other hosts.

## Build and Run

Build with `dotnet build Codemap.slnx`. Test with `dotnet test --solution Codemap.slnx`. Run the installed CLI from the target repository directory with `codemap --format markdown --output codemap-output.md`.

## Testing

New behavior should include focused tests under `tests/Codemap.Tests/`. Prefer deterministic tests for file discovery, filtering, formatting, configuration, and error handling.

## Key Patterns and Conventions

- Use feature-oriented folders and explicit interfaces at extension points.
- Keep public APIs small and names descriptive.
- Separate input discovery, processing, formatting, and output writing.
- Make configuration explicit and composable; avoid hidden global state.
- Preserve cancellation, predictable error reporting, and cross-platform path behavior.
- Use nullable reference types and analyzers when the first project is created.
- Do not introduce abstractions without a current caller or clear extension point.

## Adding a New Feature

1. Identify the owning capability under `src/`.
2. Add or extend a small interface only when multiple implementations or user customization require it.
3. Register the implementation through the host's composition root; do not use scattered service-locator lookups.
4. Add focused tests under the matching `tests/` area.
5. Update `README.md`, `docs/`, and `CHANGELOG.md` when the feature changes user-visible behavior.
6. Update `.github/copilot-instructions.md` if the dependency or registration path changes.

## CI/CD

GitHub Actions workflows build and test `Codemap.slnx` on pull requests and pushes.

## Documentation Status

User-facing usage is documented in `README.md`; pipeline boundaries are documented in `docs/how-it-works.md`.

## Common Pitfalls

- Keep C# namespaces aligned with feature-oriented boundaries rather than external project layouts.
- Do not read every file into memory by default; support bounded, cancellable processing.
- Do not make output formatting inseparable from source discovery or configuration.
- Do not add a package before checking whether the .NET platform already provides the needed capability.
- Do not claim a feature is implemented until it has a test and documented behavior.
