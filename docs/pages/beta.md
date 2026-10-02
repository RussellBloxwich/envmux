# Beta launch

The initial public target is Windows x64 with Docker Desktop in Linux-container
mode. Releases live at https://github.com/envmux/envmux/releases and carry the
MIT license, self-contained executable, project skills and SHA-256 checksums.

## Release gates

1. Run `scripts/beta-verify.ps1 -Docker` against the candidate tree. Keep its TRX
   evidence locally. No live test may silently skip.
2. Run `scripts/secret-audit.ps1 -Betterleaks <scanner>` against the working tree and
   all reachable history. Inspect strict findings separately: the configured
   exceptions cover only exact public nonce and dummy-auth fixtures.
3. Build a versioned archive with `scripts/release-build.ps1 -Version
   0.1.0-beta.1`; verify it with `scripts/release-check.ps1 -Archive <zip>
   -Version 0.1.0-beta.1 -Docker`. Use a new version for every changed artifact.
4. Review a clean source commit. Run `scripts/export-initial.ps1` if making a
   fresh public repository with one `initial commit`. It exports HEAD, never
   rewrites local history, and refuses dirty trees by default. Use `-WorkingTree` to review an audited snapshot before committing. Preserve the MIT attribution.
   Keep local context, chat, artifacts, transcripts and client-specific material
   out of the public export. Review archive and spikes explicitly before export;
   retained examples must be appropriate for public distribution.
5. Run the archive on a second clean Windows machine, with no existing envmux
   home or golden cache. Check first image build, setup skills, browser, VS Code,
   logs, resume, commit return and a dry-run prune. This machine cannot establish
   the independent clean-machine gate by itself.
6. Tag the reviewed source with an immutable beta tag. The release workflow
   creates a draft prerelease, not an automatically public release. Attach
   redacted local Docker/archive verification evidence and review the draft
   before publishing. Do not move an existing release tag.

## Kitchen rehearsal

Set `"chef": true` in `.envmux.json` and start `envmux chef`. Its guest gets
`ENVMUX_CHEF_URL` and a separate `ENVMUX_CHEF_TOKEN`. The bundled chef skill uses
these to list, launch and stop workers in this repository. A maximum of three
active workers is enforced at dispatch. Worker records preserve the selected
backend. Names deduplicate active work by returning a conflict; after a timeout,
list before retrying. Returned branches must be reviewed on the workstation.

The existing room is append-only `.context/chatroom/` text, with bearer HTTP
transport and polling. This reuses envmux's C# listener; no separate Node daemon,
arbitrary-repository chat server, or Docker socket is shipped into the guest.
Nickname text is untrusted. Tokens rotate with the controlling process.

Before advertising kitchen orchestration as proven, run a three-worker real
Claude rehearsal with bounded tasks and explicitly approved account use. Unit
and API tests establish the capability boundary and limit; they do not establish
live model quality, billing, or successful integration of three real handoffs.

## Discord checklist

Post the release link, Windows/Docker prerequisites and the README quickstart.
Explain that container credentials and uncommitted work persist in kept volumes.
Ask for versions and redacted errors, never a portal token URL or environment
export. Keep a known-working previous archive available and provide a new version
for fixes. Do not describe shared-kernel Docker sessions as hostile-tenant
isolation or claim Linux/macOS support from cross-platform unit tests alone.
