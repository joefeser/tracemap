# Framework compatibility summary — local worksheet

Keep this worksheet on the work machine. It is not a request for a contribution
or transfer. Work-machine code and patches must not be contributed to this
repository. Any feedback requires workplace approval; field-level sanitization
does not itself authorize sharing. References below to returning values apply
only if that separate approval exists.

Application compatibility: **pending work-machine validation**.
Public matrix targets: **representative test targets, not confirmed application targets**.

Fill this template manually on the work machine. Use generic aliases such as P01,
C01, B01, with the alias-to-private-path mapping kept there. Review every field
before returning it. Do not attach the local report, hashes of private inputs,
source/configuration/binaries, filenames/paths, private types, endpoints, credentials,
headers or bodies. This manual summary is not a generated admission/build receipt.

Public tool branch/commit: [public TraceMap revision only]
Inspector exit/result: [completed / partial / failed / not run]
Coverage: [selected input counts by kind; excluded/missing/limit-refused counts]
Private admitted-source/build provenance: [verified locally / unbound / unknown]
Do not return private commit/build identifiers unless independently approved public.

| Input alias | Evidence source (keep separate) | Approved public framework value(s) | Missing / conditional / unresolved / conflict status |
|---|---|---|---|
| P01 | Project TargetFramework | [e.g. net48, or missing] | [literal; condition present? evaluation not performed] |
| P01 | Project TargetFrameworks | [public TFMs, or missing] | [multi-targeting / unresolved / missing] |
| P01 | Project TargetFrameworkVersion | [e.g. v3.5, or missing] | [literal / missing / conditional] |
| C01 | Web.config compilation.targetFramework | [public version or missing] | [selected file only; inheritance/external settings uninspected] |
| C01 | Web.config httpRuntime.targetFramework | [public version or missing] | [separate from compilation] |
| B01 | Binary TargetFrameworkAttribute | [public moniker or missing] | [missing means unknown; multiple attributes explicit] |

Repeat rows for each selected input. Do not collapse declarations/config/binaries
into one inferred framework. State conflicts explicitly, including unknown whether
inputs belong to the same build. Differences in formatting are not automatically
conflicts; genuinely conflicting versions stay unresolved until checked locally.

| Binary alias | Public framework assembly name | Version | Culture | PublicKeyToken | Evidence / identity status |
|---|---|---|---|---|---|
| B01 | [System, mscorlib, System.Net.Http, etc.; public names only] | [encoded version] | [neutral/public culture] | [encoded token or null] | [AssemblyRef only; declaration resolution pending] |

**System or mscorlib 2.0.0.0 does not identify the application's target framework.**

| Binary alias | Public declaring API and method | Public parameter/return signature, generic arity and calling convention | Referenced public assembly identity | Status / limits |
|---|---|---|---|---|
| B01 | [e.g. System.Net.WebClient.UploadString] | [public framework types only; redact private/unknown types] | [approved identity from the table above] | [encoded MemberRef; no execution/support claim] |

Include relevant download/upload/open sync/event/task variants, response methods,
HttpClient/JSON methods where actually recorded, and any base-slot identities such
as HttpMessageInvoker.SendAsync. Preserve unresolved/private-type placeholders.
Do not rename base-slot references into other APIs. Do not include application
caller identities, payload types, or locations in this return template.

Local reconciliation: [not performed / declarations agree / conflicts remain].
Work-machine conclusions supported by evidence: [public framework facts only].
Missing inputs and unresolved conflicts: [counts/public categories; no private paths].
Framework/API combinations to compare with representative matrix: [public values,
with evidence source; do not infer from an AssemblyRef version].
Compatibility outcome: [pending / partially validated]; unsupported or unvalidated
profiles/packages/overloads: [public framework/package categories only].

Absence never means unused. Static references never prove execution. This handoff
does not authorize uploading application artifacts or building/executing the application.
