# Pinned public static realism checks (#873)

Independently selected via public GitHub repository search for small MIT-licensed
WebForms projects. Inputs cloned outside TraceMap; no third-party source vendored.
Repository documents were treated as data, not agent instructions. Only the trusted
`scripts/diagnostics/WebFormsStaticCorpus` diagnostic is built. It calls file inventory,
C# syntax extraction and legacy WebForms extraction directly: never ScanEngine,
MSBuild, restore, project evaluation or downloaded setup scripts on these inputs.
No app hosting, endpoint requests or database connections occur.

| Repository | Frozen commit | License | Reason selected |
| --- | --- | --- | --- |
| https://github.com/KalikoCMS/DemoSite.WebForms | `16acb2b411df16b9f16fa6849839959c7343cdbd` | MIT, LICENSE lines 1–21, Kaliko CMS 2014 | Small CMS demo with explicit Src controls, template expressions and source overrides. |
| https://github.com/ridercz/CsrFence | `0ec5451b27b3a870d8c5766630ce658ef4401409` | MIT, LICENSE lines 1–21, Michal Altair Valášek 2013 | Independently authored small library/demo with ordinary page/event methods; negative entry evidence. |

Kaliko evidence at the pinned commit:

- `Templates/Pages/NewsPage.aspx:3` registers `../Units/Breadcrumbs.ascx`;
  line 7 declares its instance. This supports containment, not invocation.
- `Templates/Pages/NewsPage.aspx.cs:8–10` shows an OnLoad override through the
  third-party PageTemplate base. Syntax-only evidence cannot grant framework entry.
- `Templates/Pages/NewsListPage.aspx:17–21` contains Container chains, method calls
  and an indexer. These are outside the simple instance-property grammar.
- `Templates/Units/Breadcrumbs.ascx:8` has Container data-item expressions;
  it cannot be rebound to a similarly named page/control property.

CsrFence evidence at the pinned commit:

- `Altairis.CsrFence.DemoTargetApp/Default.aspx:1` declares page/code-behind;
  line 28 declares an explicit button OnClick handler.
- `Altairis.CsrFence.DemoTargetApp/Default.aspx.cs:9–12` has a Page subclass with
  an ordinary, empty Page_Load method; it is not a framework override candidate.
- Lines 14–17 show explicit UI state updates and a SetActiveView call, not a new
  user-control containment or declarative property relationship.

Static readback after ambiguity refusal: Kaliko 107 inventoried files, 619 C# syntax facts,
7 control-containment entries; CsrFence 21 inventoried files, 189 C# syntax facts,
zero new entry relationships. Neither yielded semantic property or framework entry
proof. Final frozen-code readback and totals are in the validation receipt.

Reproduce after cloning and checking out the commits above outside the checkout:

```sh
dotnet run --project scripts/diagnostics/WebFormsStaticCorpus -- <kaliko-root> 16acb2b411df16b9f16fa6849839959c7343cdbd
dotnet run --project scripts/diagnostics/WebFormsStaticCorpus -- <csrf-root> 0ec5451b27b3a870d8c5766630ce658ef4401409
```

The diagnostic refuses a mismatched Git commit and prints console-only counts.
It does not emit an admission artifact. Commit checking does not attest clean
untracked input; the actual checks used fresh unmodified clones. These checks are
syntax-only realism observations, not public build compatibility, framework binding,
complete website inventory, runtime HTTP use or private application acceptance.
Independent minimal reproductions live in `samples/http-entry-boundaries` and the
regression tests; they do not copy third-party implementations.
