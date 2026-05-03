# Privacy Policy — Note Aerator

**TL;DR**: Note Aerator runs entirely on your machine and does not
collect any data about you, your notes, or your usage. The only
network calls it makes are to render Markdown libraries from a public
CDN (described below) — no telemetry, no analytics, no account
required.

This policy applies to the Note Aerator desktop application
distributed via this repository's GitHub Releases (and any future
distribution channel such as winget or the Microsoft Store).

## What Note Aerator does NOT do

- It does **not** collect telemetry of any kind.
- It does **not** send your notes, file paths, search queries, or
  comments to any server.
- It does **not** require an account, login, or sign-in.
- It does **not** receive automatic updates from the application
  itself (updates happen via your distribution channel — GitHub
  Releases, winget, etc. — and only when you initiate them).
- It does **not** call home for license checks, activation, or any
  other phone-home behavior.

## What Note Aerator DOES do (data you should know about)

### Files on your disk

Note Aerator reads the `.md` files in the project folders you choose
and writes a sidecar `<basename>-comments.json` file next to a `.md`
when you add a comment via the right-click / "+" UI. Source `.md`
files are opened **read-only** and never modified by the viewer.
The full file-handling guarantees are documented in
[`POC/README.md`](POC/README.md).

### Local app state

- `%APPDATA%\noteaerator\viewer\projects.json` — the list of project
  folders you've added (paths only).
- `%LOCALAPPDATA%\noteaerator\WebView2\` — the embedded browser
  cache used by WebView2 (analogous to what Microsoft Edge stores
  on disk).

Both stay on your machine. Neither is sent anywhere by Note
Aerator.

### Network calls

The renderer (`viewer.html`) loads the following JavaScript / CSS
libraries from the [jsDelivr](https://www.jsdelivr.com/) public CDN
the first time a Markdown file is rendered:

- `markdown-it`
- `markdown-it-task-lists`
- `highlight.js`
- `mermaid`
- `github-markdown-css`

These requests go to `cdn.jsdelivr.net`. jsDelivr is operated by an
independent provider with its own
[privacy policy](https://www.jsdelivr.com/privacy-policy-jsdelivr-net).
WebView2 (Microsoft Edge) caches these resources locally after the
first fetch.

Vendoring these libraries into the installed package — eliminating
the CDN call — is a tracked follow-up (see "known gaps" in
`POC/README.md`).

### Links you click

When you click a link inside a rendered note, Note Aerator opens it
in your default web browser via the standard Windows shell. Note
Aerator does not see, log, or transmit which link you clicked.

### Microsoft Edge / WebView2

Note Aerator embeds the Microsoft Edge WebView2 runtime, which is
shipped with Edge on Windows 10/11. WebView2's own telemetry and
update behavior is governed by Microsoft Edge's settings — see
<https://privacy.microsoft.com/privacystatement>. Note Aerator does
not collect any additional data on top of that.

## Children

Note Aerator is a Markdown viewer suitable for general use. It is
not directed at children under 13 and does not knowingly collect any
information from them (or from anyone else).

## Changes to this policy

If this policy ever changes, the change will be visible in the git
history of `PRIVACY.md` in this repository.

## Contact

Open an issue at <https://github.com/rjduncan19/noteaerator/issues>.

_Last updated: 2026-05-03._
