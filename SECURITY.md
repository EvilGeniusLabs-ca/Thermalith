# Security Policy

Thermalith is a local-first desktop app: no account, no cloud, no telemetry. Your labels and data stay on
your machine. That keeps the attack surface small, but the app does talk to USB and Bluetooth serial
devices and parses `.nlbl` files and image assets, so genuine issues are possible.

## Reporting a vulnerability

Please report security issues privately, not in a public issue or pull request.

Email **evilgenius@evilgeniuslabs.ca** with:

- what the problem is and where (component, file, or feature),
- how to reproduce it, ideally with a minimal example,
- the impact you think it has.

You'll get an acknowledgement within a few days. This is a small project run by one maintainer, so fixes
are best-effort rather than on a guaranteed clock, but real issues are taken seriously and a fix or
mitigation will be worked out. Once it's resolved you're welcome to be credited, or to stay anonymous.

## Scope

In scope: anything that lets a crafted `.nlbl` file, image asset, or printer response read or write outside
its lane, crash the app in a way that loses work, or run code unexpectedly.

Out of scope: the absence of code signing on the binaries (a known, deliberate state for early releases,
not a vulnerability), and issues in NIIMBOT's own firmware or apps (Thermalith is an independent project,
not affiliated with NIIMBOT).

## Supported versions

This is a fast-moving project. Fixes land on the latest release; there's no back-porting to older versions
during the beta. Update to the newest build before reporting.
