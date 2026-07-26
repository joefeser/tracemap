# Access Parallels Source Runner Implementation State

Status: implementation in progress

Branch: `codex/access-parallels-runner`

Base: `origin/dev` at `20f1a26ee0e71fad8e66d7131cda072a2b5b5333`

## Scope

Build a thin source-only orchestration layer over the already-reviewed Access
CLI, smoke harness, and local review bundle. Add no extraction, COM metadata,
fact, rule, or consumer behavior.

## Proven local environment

- Parallels VM: `Windows 11 - Access Isolated`
- Windows: ARM64, Microsoft Access 16 registered
- VM network device: disabled
- scoped host shares: `access_input` read-only, `access_output` read/write
- Parallels Tools guest command execution: available
- guest source root: operator-local and uncommitted
- guest Git remote: absent

## Baseline evidence

At exact `20f1a26ee0e71fad8e66d7131cda072a2b5b5333`:

- offline guest-local clone completed from a Git bundle;
- exact upstream hashes were verified for .NET SDK 10.0.302 Windows ARM64,
  MinGit 2.55.0.3 ARM64, and Git LFS 3.7.1 ARM64;
- full solution build passed with 0 warnings and 0 errors in the guest;
- Access-focused Windows tests passed 65/65;
- the existing synthetic Access harness completed;
- Phase 9 consumer contracts and the local review bundle contract passed;
- the sanitized local review bundle was retained;
- networking was not required;
- the guest source checkout remained clean.

## Implementation

- macOS host runner validates the Parallels isolation posture without mutating
  VM configuration;
- Windows guest runner validates exact source identity, builds, tests, and
  delegates synthetic validation to the existing harness;
- guest output is captured and reduced to one allowlisted categorical line;
- representative inputs and richer extraction are deliberately absent;
- offline toolchain provisioning remains operator-local and outside Git.

## Deferred

- representative database selection and authorization;
- form/report internals;
- VBA source or flow;
- macro identities or bodies;
- public artifact publication;
- remote physical-Windows Codex control.
