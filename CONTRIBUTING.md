# Contributing to Thermalith

Thanks for your interest in Thermalith — an open-source (GPL-3.0-or-later) NIIMBOT label designer and
driver. Contributions are welcome: bug reports, fixes, features, docs, and hardware test reports for
printers we don't own yet.

## Where the code actually lives

The **canonical upstream is the self-hosted GitLab instance at `gitlab.evilgeniuslabs.ca`** (the
Thermalith repository). All development, CI, and releases happen there.

**The GitHub repository is a one-way mirror** (GitLab → GitHub). It exists to give the project a public
home and a download host. Because the mirror is one-directional, **a pull request merged on GitHub would
not flow back to the upstream and would be overwritten on the next sync.** That's not a snub — it's just
how the topology works. So the contribution flow has one extra step, described below.

## How to contribute right now

**Issues — bug reports and feature requests:** open them on **GitHub Issues**. Include your OS, the
printer model + label size if hardware-related, and steps to reproduce.

**Code — pull requests:**

1. Fork the GitHub mirror and create a branch off `main`.
2. Make your change. Keep one logical change per PR, match the surrounding code style, and make sure the
   test suite still passes (`dotnet test` — see [README](README.md#building) for setup).
3. **Sign your commits** with the Developer Certificate of Origin: `git commit -s` (adds a
   `Signed-off-by:` line certifying you wrote the change and can submit it under the project license).
4. Open the PR against the GitHub mirror.
5. A maintainer reviews it, then **lands your commits into the GitLab upstream** with your authorship
   preserved. The mirror syncs them back to GitHub, and your PR is closed with a comment pointing at the
   upstream commit. Your work — and credit — ships; it just travels through the upstream to get there.

No CLA is required. Thermalith is GPL-3.0-or-later and **inbound contributions are accepted under the
same license** (inbound = outbound). The DCO sign-off is all we ask for provenance.

## Contributing directly on GitLab (future)

Once the GitLab instance is opened to public registration, you'll be able to skip the GitHub round-trip
and submit **merge requests directly on `gitlab.evilgeniuslabs.ca`**, where CI runs natively. Until then,
the GitHub PR flow above is the path in. This document will be updated when that opens.

## Development setup

See the [Building](README.md#building) section of the README — you need the **.NET 10 SDK** (not just the
runtime). The normal loop is `dotnet restore` / `dotnet build` / `dotnet test`; run the editor with
`dotnet run --project src/Thermalith.App`.

## Hardware test reports

Thermalith's print path is verified on a NIIMBOT **B1**; broader printer coverage is in progress. If you
have another model, a connect/detect/print report (model id, dpi, label size, what worked or didn't) is a
genuinely valuable contribution — open it as an issue.

## Conduct

Be respectful and constructive. Assume good faith, keep discussion technical, and help keep this a
welcoming project for everyone.
