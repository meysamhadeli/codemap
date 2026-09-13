# Contributing to codemap

> [!TIP]
> Keep changes small, testable, and easy to review. Start with [AGENTS.md](AGENTS.md) for repository conventions.

Thanks for contributing to codemap.

## 🧭 Start Here

- Read [README.md](README.md) for installation, CLI usage, configuration, and feature behavior.
- Read [docs/how-it-works.md](docs/how-it-works.md) before changing the packing pipeline or adding an extension point.
- Read [AGENTS.md](AGENTS.md) for repository structure, conventions, and maintenance expectations.

## 🛠️ Development Setup

Requirements:

- .NET SDK 10 or newer.
- Git for Git-related tests and remote repository behavior.

Restore, build, and test with:

```bash
dotnet restore Codemap.slnx
dotnet build Codemap.slnx
dotnet test --solution Codemap.slnx
```

## 🗂️ Project Structure

- `src/Codemap.Core/`: reusable discovery, transformation, rendering, and metadata pipeline.
- `src/Codemap.Cli/`: command-line host and argument mapping.
- `tests/Codemap.Tests/Unit/`: focused core behavior tests.
- `tests/Codemap.Tests/Integration/`: process-level CLI tests.
- `docs/`: architecture and implementation guidance.

## ✍️ Making Changes

- Keep core processing independent from CLI presentation where practical.
- Preserve deterministic ordering, cancellation, cross-platform paths, and explicit configuration.
- Add focused tests for every new behavior.
- Update [README.md](README.md) for user-visible behavior.
- Update [docs/how-it-works.md](docs/how-it-works.md) when pipeline boundaries or extension guidance change.
- Update [CHANGELOG.md](CHANGELOG.md) for notable user-visible changes.
- Do not commit generated output from `bin/`, `obj/`, `artifacts/`, or test result directories.

## 🔎 Pull Requests

Keep changes focused. Explain what changed, why it changed, and how it was tested. Confirm that the solution builds, tests pass, and no unrelated files were modified.
