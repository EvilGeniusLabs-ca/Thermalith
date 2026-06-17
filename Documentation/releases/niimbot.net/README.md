# Niimbot.Net release notes

Release notes for the **`Niimbot.Net`** NuGet — the standalone protocol + transport library. It is
versioned **independently** of the Thermalith app (`<Version>` in `src/Niimbot.Net/Niimbot.Net.csproj`)
and has its own notes here, separate from the app's notes one level up.

One file per driver version, named `v<MAJOR.MINOR.PATCH>.md`, with the same YAML frontmatter as the app
notes (`title`, `prerelease`). Copy `../TEMPLATE.md` to start one.

## How it ships

The driver builds and packs in the **same CI pipeline** as the app — there is no separate driver build.
Today the `.nupkg` rides along as an asset on the app's GitHub release. Its version and notes are its
own, though:

- The current version's highlights go in the csproj `<PackageReleaseNotes>` so nuget.org shows them.
- This folder is the full per-version history.

When the driver moves to independent publishing (its own nuget.org push / tag), these files feed that
release directly — no reformatting needed.

## When the driver changes in a release

Bump `<Version>` in `src/Niimbot.Net/Niimbot.Net.csproj`, add a `v<version>.md` here, and update
`<PackageReleaseNotes>` — all in the same pre-tag commit as the app's release prep (see `../README.md`).
If a release touches only the app and not the driver, leave the driver version and notes untouched.
