# Codemap AI Contribution Instructions

## Repository State

Codemap is a pre-scaffold C# tool repository. Do not invent existing projects, commands, namespaces, or dependencies. Before changing implementation, inspect the current solution and project files.

## C# Conventions

- Prefer clear, feature-oriented namespaces and small classes with one responsibility.
- Enable nullable reference types and analyzers in project configuration.
- Use async APIs and `CancellationToken` for file and process work that can be long-running.
- Treat paths as cross-platform values; avoid platform-specific separators and shell assumptions.
- Keep core processing independent from CLI presentation and file-system side effects where practical.
- Use explicit options/configuration objects and validate them at boundaries.
- Avoid global mutable state, service locators, speculative abstractions, and unnecessary dependencies.

## Feature Design

A typical capability should separate source discovery, filtering, processing, formatting, and output. Add extension points only where callers need customization. Prefer composition through a clear host/composition root.

## Testing

Add focused tests for each new behavior. Cover empty input, ignored paths, invalid configuration, cancellation, deterministic ordering, and platform-sensitive paths where relevant. Keep tests independent of the developer machine and network.

## Documentation and Change Tracking

User-visible behavior belongs in `README.md` or `docs/`. Record notable changes in `CHANGELOG.md`. Keep `AGENTS.md` aligned with actual commands and structure.

## Maintenance Matrix

| Change | Update or verify |
| --- | --- |
| Add or move a production feature | Matching `src/` area, composition root, focused `tests/`, and `AGENTS.md` if structure changes |
| Add a CLI option or configuration field | Option model, validation, CLI help, tests, `README.md`, and `CHANGELOG.md` |
| Change source discovery/filtering | Discovery and filter implementations, path-focused tests, docs describing inclusion/exclusion behavior |
| Change output format | Formatter, snapshot/golden tests if used, output docs, compatibility notes in `CHANGELOG.md` |
| Add a package or external service | Project file, lock/restore behavior, CI, security review, and contributor instructions |
| Change build/test commands | Project files, `AGENTS.md`, README setup instructions, and both GitHub workflows |
| Change repository automation | Relevant workflow/template plus `AGENTS.md` and `CHANGELOG.md` when contributor behavior changes |

## Review Expectations

Keep pull requests narrowly scoped. Explain behavior changes and how they were tested. Do not modify existing user changes or commit generated output.
