# Contributing

## Commits

Use [Conventional Commits](https://www.conventionalcommits.org/):

- `feat(scope): summary`
- `fix(scope): summary`
- `docs(scope): summary`
- `test(scope): summary`
- `chore(scope): summary`

Keep each commit to one logical change. Update [CHANGELOG.md](CHANGELOG.md) in the same commit when the change is something a listener or a plugin user can observe. Update [docs/](docs/) in the same commit as the behavior it describes.

## Checks

Engine changes ship with tests. From the repository root, with the .NET 8 SDK:

```text
dotnet test
```

`dotnet test` runs the tests on .NET 8. `dotnet build` also compiles the engine for .NET Framework 4.8, which is what MusicBee loads.

## Layout

- `src/Ladder.Engine` — ranking, pairing, and storage. No MusicBee types and no UI.
- `src/Ladder.Plugin` — the MusicBee plugin.
- `docs/` — the ranking model and, once it exists, the ladder file format.

## Data

Do not commit a personal library, exported ratings, credentials, or machine-specific paths.
