# Contributing to codemap

Thanks for contributing. Keep changes focused and easy to review.

## Before you start

- Read [AGENTS.md](AGENTS.md) for repository conventions.
- Read [README.md](README.md) when changing user-visible behavior.
- Use .NET SDK 10 or newer.

## Build and test

```bash
dotnet build Codemap.slnx
dotnet test --solution Codemap.slnx
```

Add focused tests for new or changed behavior. Do not commit generated files from `bin/`, `obj/`, `artifacts/`, or test result directories.

## Open a pull request

1. Explain what changed and why.
2. Describe how you tested it.
3. Update documentation for user-visible changes.
4. Add a release label such as `feature`, `bug`, `patch`, `documentation`, or `major` when appropriate.
