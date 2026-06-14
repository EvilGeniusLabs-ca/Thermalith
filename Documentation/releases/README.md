# Release notes

One markdown file per release tag, named **exactly** after the tag — `v1.2.0.md`,
`v1.2.0-beta.1.md`, and so on. When you publish, the CI `release:github` job reads
the file matching the tag being released and uses it to create the GitHub release.

## How a release happens

1. Copy `TEMPLATE.md` to `<tag>.md` (e.g. `v1.2.0.md`), fill it in, and commit it.
2. Push the tag (`git tag v1.2.0 && git push origin v1.2.0`). All platforms and the
   manual PDF build automatically.
3. Press play on the **`release:github`** job to publish. It uploads every built
   artifact (binaries, `Niimbot.Net` nupkg, the manual PDF) to the GitHub release.

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
