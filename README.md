# envmux

Development sessions on their own branches, inside Docker containers. A session
browser makes `localhost` mean that container, including servers bound only to
its loopback. The terminal UI and browser portal show tasks, logs and shells.
Commits return to `envmux/<session>` in your workstation repository.

The public beta targets **Windows x64 and Docker Desktop in Linux-container
mode**. Releases also include experimental Linux x64/ARM64 and macOS ARM64
archives. Incus remains an optional backend. MIT licensed.

## Try it

Download the Windows x64 archive and `SHA256SUMS.txt` from
[envmux/envmux releases](https://github.com/envmux/envmux/releases). Check its
SHA-256 with `Get-FileHash`, extract it, and run `envmux.exe` from a terminal.
Release binaries are compiled with Native AOT, trimmed and optimized for size; using it needs neither .NET nor Node on the host.
Docker Desktop must be running. Git must be available. Each archive contains a
`README.md` with platform-specific checks and getting-started instructions.
In the extracted directory run `.\envmux.exe install` on Windows, or
`./envmux install` on Unix. This copies the executable to `~/.envmux/bin`, adds
it to your user PATH and checks Git and the Linux Docker engine. Open a new
terminal, then check `envmux --version`. `install --check` checks prerequisites
without writing anything; `install --no-path` leaves PATH management to you.

For Linux or Apple Silicon macOS, choose the matching `.tar.gz`, verify its
SHA-256, extract with `tar -xzf <archive>`, then run `./envmux`. Linux needs
a local Docker Engine socket; macOS needs Docker Desktop. These archives
are experimental pending live session checks; macOS builds are unsigned.

In a git repository with an initial commit:

```powershell
& C:\tools\envmux\envmux.exe init --skills both
& C:\tools\envmux\envmux.exe config validate
& C:\tools\envmux\envmux.exe --dry-run
& C:\tools\envmux\envmux.exe first-session
```

Edit `.envmux.json` to declare your install and development tasks. The bundled
setup skill can help Claude Code or Codex do that. `init --skills claude` and
`init --skills codex` install only one agent's skills. Existing customized skills
are preserved; an existing config is preserved unless you use `--force`.

Press **b** for the session browser, **p** for the portal, **e** for VS Code,
and **c** for a shell. Install VS Code's Dev Containers extension for the default
editor attach. `envmux code --print` prints its attach URI. `envmux logs <session>
<task>` reads a task's saved output.

The first session builds the embedded golden Docker image and downloads its
packages and Claude Code. Later sessions reuse the cache. Tasks run inside the
container; host Node installations do not supply its toolchain.

## A chef and a kitchen

Set `"chef": true` in `.envmux.json`, enable the tool credentials you intend
workers to use, and start `envmux chef`. The bundled `envmux-chef` skill explains
how the agent inside that session dispatches up to three Claude workers through
a separate bearer capability. Workers get branches and chat access, without
chef dispatch access. Codex can act as chef; the worker runner currently uses
Claude Code. The existing `.context/chatroom/` files remain the durable room.

## Beta boundaries

Docker containers share a kernel and the envmux Docker network. This is a tool
for your trusted development work, not a boundary for hostile tenants. No host
Docker socket or repository bind mount is placed inside a session.

Enabling `tools` copies selected sign-in state into the container. Kept sessions
and named volumes retain that state and uncommitted work. Disabling a tool in
config does not revoke copies already made. Keep project secrets in ignored
`.env` files and opt into `envFile` explicitly. Never post portal URLs containing
tokens, transcripts, credentials or whole environment dumps to Discord.

`prune --dry-run` previews repository-scoped cleanup. Unreadable and dirty
workspaces are kept unless `--force` is explicit; running sessions are kept
unless `--all` is explicit. Docker volumes are preserved by container removal.
SSH configuration selects one backend; run `envmux ssh --backend incus` when
using Incus instead of Docker. It updates your SSH config, so review `--print`
first.

## Contribute and ship

Build with .NET 10 and Node 24. See
[development](docs/pages/development.md) for build, test, Docker verification,
secret auditing and release archive scripts. The release checklist is in
[beta launch](docs/pages/beta.md). Publish immutable beta versions after verifying
the archive against Docker. The source export helper prepares a fresh tree;
it does not rewrite this repository's history.

For Discord reports, include the envmux version, Windows version, Docker Desktop
version, command and a redacted error. Say whether Docker uses Linux containers
and whether a session has uncommitted work. Keep the original work until a fix
has been verified.
