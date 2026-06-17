# Release notes

One markdown file per release tag, named **exactly** after the tag — `v1.2.0.md`,
`v1.2.0-beta.1.md`, and so on. When you publish, the CI `release:github` job reads
the file matching the tag being released and uses it to create the GitHub release.

## How a release happens

**Everything in step 1 goes into a single commit _before_ you tag.** The tag only points
CI at a commit; the build reads the version and notes from that commit's files, never from
the tag name — so anything you forget here ships mislabelled.

1. Prepare the release commit (notes + version + changelog, committed together):
   - **Notes** — copy `TEMPLATE.md` to `<tag>.md` (e.g. `v1.2.0.md`) and fill it in.
   - **Version** — bump `<Version>` in `src/Thermalith.App/Thermalith.App.csproj` to match the
     tag. Artifact filenames and the in-app About box read this, **not** the git tag, so a
     mismatch ships binaries labelled with the old version.
   - **Changelog** — in `CHANGELOG.md`, move the `[Unreleased]` items into a dated
     `[<version>] - YYYY-MM-DD` section and refresh the compare links at the bottom.
   - **Driver (only if it changed this release)** — bump `<Version>` + `<PackageReleaseNotes>` in
     `src/Niimbot.Net/Niimbot.Net.csproj`, and add a `v<driver-version>.md` under
     [`niimbot.net/`](niimbot.net/). It versions independently — leave it untouched if the release
     only touched the app.
   - Commit all of the above together.
2. Tag that commit and push: `git tag v1.2.0 && git push origin v1.2.0`. All platforms and the
   manual PDF build automatically (tag pushes only).
3. Press play on the **`release:github`** job to publish. It mirrors the source + tag to GitHub
   and uploads every built artifact (binaries, the `Niimbot.Net` nupkg, the manual PDF, and
   `printers.json`) to the release.

The `Niimbot.Net` nupkg is **versioned independently** of the app and keeps its own release notes — see
[`niimbot.net/`](niimbot.net/). Its `<Version>` and `<PackageReleaseNotes>` live in
`src/Niimbot.Net/Niimbot.Net.csproj`, and it builds/packs in this same pipeline (no separate build).
Bump it only when the driver's protocol/behaviour changes, not on every app release.

## File format

YAML frontmatter carries the release metadata; everything after the closing `---`
is the release body (plain markdown):

    ---
    title: Thermalith 1.2.0
    prerelease: false
    ---

    ## Highlights
    - ...

- **title** — the GitHub release title. Defaults to the tag if omitted.
- **prerelease** — `true` publishes it as a GitHub **Pre-release** (use for
  `-beta` / `-rc` tags). `false` (or omitted) = a full release.

So a beta you want public is just a notes file with `prerelease: true` — the CI job
flags it accordingly. No other change needed.

## Notes

- GitHub hosts the uploaded files directly (public, durable). GitLab CI artifacts
  (90-day, login-gated) cover the internal/beta audience — there is no GitLab
  release track.
- The job needs a GitHub PAT in the `GITHUB_PAT` CI variable (see the comment on
  `release:github` in `.gitlab-ci.yml`).
