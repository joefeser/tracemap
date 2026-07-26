# Access Parallels Source Runner Design

## Shape

The workflow has two source-only scripts:

- `Invoke-AccessParallelsSource.ps1` runs on macOS with PowerShell and
  `prlctl`. It validates the immutable isolation posture and invokes one
  allowlisted guest action.
- `Invoke-AccessGuestSource.ps1` runs from the guest-local TraceMap clone. It
  validates source identity and performs `doctor`, `build`, or `synthetic`.

The offline SDK, MinGit, Git LFS, NuGet feed, Git bundle, VM configuration, and
retained local review bundles are operator-local assets. They are never
committed.

## Security boundary

The host script treats all guest output as untrusted. It captures raw output,
requires a zero exit code, and emits only one allowlisted sanitized status
line. Failure output is categorical.

The guest script requires:

- the expected commit SHA;
- a clean checkout;
- zero Git remotes;
- installed Microsoft Access discovered without launching it;
- guest-local source and toolchain paths.

The host preflight requires the configured VM to be running, `net0` disabled,
the `access_input` share read-only, and the `access_output` share read/write.
It never changes those settings.

## Operations

`doctor` validates the boundary and source/tool availability.

`build` runs the solution build with the already-restored offline cache and the
six exact Access test classes sequentially. Separate test processes avoid
cross-class SQLite pool races on Windows and prevent substring filters from
selecting unrelated tests.

`synthetic` delegates to `Invoke-AccessSmoke.ps1`, validates its highest
immutable checkpoint, and retains the generated hidden-local review bundle in
the guest. No representative input is accepted by this runner version.

## Limitations

- Initial offline toolchain/source staging remains an operator-local
  provisioning step.
- The runner targets Parallels on macOS and an ARM64 Windows guest but does not
  claim other hypervisor support.
- Representative database execution remains in the separately authorized
  `Invoke-AccessRepresentativeSmoke.ps1` workflow.
- The runner adds no form/report identity, VBA source, or macro-body support.
