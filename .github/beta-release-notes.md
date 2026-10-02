Windows x64 beta, MIT licensed. Requires Git and Docker Desktop running Linux
containers. Download the zip and SHA256SUMS.txt, verify the archive, and extract.
The executable carries its runtime and portal; host .NET and Node are not needed.

In a committed git repository, run `envmux init --skills both`, edit and validate
`.envmux.json`, then `envmux first-session`. Press b for the session browser,
p for the portal, e for VS Code (Dev Containers extension), and c for a shell.

Chef dispatch is opt-in (`"chef": true`, then `envmux chef`). Workers currently
run Claude Code and consume the tool account you explicitly enable. Credentials
and uncommitted work persist in kept Docker volumes. Containers share a kernel
and network; use them for trusted development work.

Maintainer draft gate: attach successful local Docker and extracted-archive
verification evidence, finish the clean second-machine and real kitchen
rehearsals, and review the source/secret audit before making this release public.
CI unit tests alone do not establish these gates.

For Discord support, include versions and redacted errors. Never share portal
token URLs, sign-in state, transcripts, or environment dumps.
