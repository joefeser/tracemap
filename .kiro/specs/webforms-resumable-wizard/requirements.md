# Resumable Web Forms configuration wizard

## Acceptance contract

1. A shared, UI-independent state/validation service drives a terminal wizard.
   The user chooses a configuration root containing root.config.json, an explicit
   project roster, per-project project.config.json/native.config.json/forms.txt,
   and separate immutable runs. No twenty-variable shell setup is required.
2. Accept a website directory, .sln, .csproj or .vbproj. Resolve and validate the
   web root explicitly when a solution is ambiguous. Detect projectless sites;
   do not silently register every project or assume every build target works.
3. Validate toolchain and build target. Show commands and require explicit
   consent before a build; no consent means no execution. Manual projectless
   ASP.NET publication is a first-class v1 path. Builds are not source-to-binary
   provenance, and existing native proof/attestation gates remain intact.
4. All versus selected pages is explicit. Selected forms are web-root-relative
   .aspx paths. Accept absolute in-root paths and either separator, normalize,
   reject escapes/missing/duplicate/case-ambiguous paths. Missing or blank
   forms.txt is populated deterministically and pauses for human editing.
   --continue resumes that step, not every preceding question. Empty never
   silently means all.
5. Validate root and project configurations on continue. Recheck relevant
   source/publication files and hashes; saved valid flags are not authority.
   Preserve valid projects, identify affected invalid projects, and offer only
   explicit supported repairs with confirmation. Never reset or overwrite a
   previous run silently. Persist progress atomically and reject concurrent use.
6. Add another project and DLL dependencies explicitly. Respect build project
   references without automatically adding independent TraceMap projects.
7. Delegate scanning, combination and reporting to existing native/CLI services.
   Never launch customer websites or execute their database code. Config/run
   writes must not overlap source/publication roots. Revalidate before reuse.
8. Reproduce fresh/resume/subset/invalid-config/repair/build-consent/publication/
   multiple-project/failure boundaries locally, including restart without saved
   shell variables. Retain provenance on derived machine-readable artifacts.
9. Document the command, files, typed errors, repair boundaries and limitations;
   deliver a scoped PR stacked on #798 until its dependency lands. Do not mutate
   #798's head or send another Codex review request (owner tagged that head).

## Deferred backlog (not silently implemented in v1)

- Automatic solution-wide web/backend project discovery and registration.
- Automated ASP.NET publication/toolchain installation and richer build recovery.
- Optional shared-DLL source/PDB/source-server acquisition and mapping.
- Microservice setup and evidenced click -> HTTP client -> backend -> database
  connections. Configuration alone does not prove a cross-service edge.

No runtime, complete customer coverage, build authenticity or merge approval
claim follows from wizard completion.
