import assert from "node:assert/strict";
import { mkdir, mkdtemp, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { createDiscoveryOutputs, splitLlmsSections } from "./discovery.mjs";

test("createDiscoveryOutputs writes valid empty discovery outputs", async () => {
  const outputs = await createDiscoveryOutputs([]);

  assert.deepEqual(JSON.parse(outputs.docsIndexJson).entries, []);
  assert.deepEqual(JSON.parse(outputs.routesIndexJson).entries, []);
  assert.match(outputs.llmsText, /^# TraceMap/);
  assert.match(outputs.llmsText, /## Non-Claims/);
  assert.match(outputs.llmsText, /No public conclusion without evidence\./);
});

test("createDiscoveryOutputs emits deterministic JSON order by public path or URL", async () => {
  const first = await createDiscoveryOutputs([siteEntry("/zeta/"), repoEntry("docs/VALIDATION.md"), siteEntry("/")]);
  const second = await createDiscoveryOutputs([siteEntry("/"), siteEntry("/zeta/"), repoEntry("docs/VALIDATION.md")]);

  assert.equal(first.routesIndexJson, second.routesIndexJson);
  assert.equal(first.docsIndexJson, second.docsIndexJson);
  assert.deepEqual(
    JSON.parse(first.routesIndexJson).entries.map((entry) => entry.path),
    ["/", "/zeta/"]
  );
});

test("createDiscoveryOutputs enforces sourceType, hintCategory, and required schema fields", async () => {
  await assert.rejects(
    createDiscoveryOutputs([{ ...siteEntry("/"), sourceType: "page" }]),
    /invalid sourceType: page/
  );
  await assert.rejects(
    createDiscoveryOutputs([{ ...siteEntry("/"), hintCategory: "promo" }]),
    /invalid hintCategory: promo/
  );
  await assert.rejects(createDiscoveryOutputs([{ ...siteEntry("/"), title: "" }]), /required string field: title/);
});

test("createDiscoveryOutputs validates preferredProofPath states", async () => {
  const dist = await createDistWithRoutes(["/", "/proof/"]);

  await createDiscoveryOutputs([{ ...siteEntry("/"), preferredProofPath: "/proof/" }], {
    dist,
    resolveInternalPaths: true
  });
  await createDiscoveryOutputs([{ ...siteEntry("/"), preferredProofPath: "https://tracemap.tools/proof/" }], {
    dist,
    resolveInternalPaths: true
  });
  await createDiscoveryOutputs([siteEntry("/")], { dist, resolveInternalPaths: true });

  await assert.rejects(
    createDiscoveryOutputs([{ ...siteEntry("/"), preferredProofPath: " " }], {
      dist,
      resolveInternalPaths: true
    }),
    /preferredProofPath must be a non-empty string when present/
  );
  await assert.rejects(
    createDiscoveryOutputs([{ ...siteEntry("/"), preferredProofPath: "/missing/" }], {
      dist,
      resolveInternalPaths: true
    }),
    /references missing preferredProofPath: \/missing\//
  );
  await assert.rejects(
    createDiscoveryOutputs([{ ...siteEntry("/"), preferredProofPath: "https://tracemap.tools/missing/" }], {
      dist,
      resolveInternalPaths: true
    }),
    /references missing preferredProofPath: \/missing\//
  );
});

test("createDiscoveryOutputs allows denied phrases only as direct nonClaims strings", async () => {
  await createDiscoveryOutputs([
    {
      ...siteEntry("/"),
      nonClaims: ["No AI impact analysis or vector database feature."]
    }
  ]);

  await assert.rejects(
    createDiscoveryOutputs([{ ...siteEntry("/"), title: "AI impact analysis" }]),
    /title contains denied phrase outside nonClaims: AI impact analysis/
  );
  await assert.rejects(
    createDiscoveryOutputs([{ ...siteEntry("/"), limitations: ["No facts.ndjson publication."] }]),
    /limitations\[0\] contains denied phrase outside nonClaims: facts\.ndjson/
  );
  await assert.rejects(
    createDiscoveryOutputs([{ ...siteEntry("/"), nonClaims: [["No AI impact analysis."]] }]),
    /nonClaims\[0\] must be a non-empty string/
  );
});

test("createDiscoveryOutputs renders llms.txt headings, hint order, and planned labels", async () => {
  const outputs = await createDiscoveryOutputs([
    { ...siteEntry("/demo/"), hintCategory: "demo", title: "Demo" },
    { ...siteEntry("/roadmap/"), publicClaimLevel: "planned", hintCategory: "roadmap", title: "Future Route" },
    { ...siteEntry("/limitations/"), hintCategory: "limitations", title: "Limitations" },
    { ...siteEntry("/evidence/"), hintCategory: "evidence", title: "Evidence" },
    siteEntry("/"),
    repoEntry("README.md")
  ]);

  const headings = [...outputs.llmsText.matchAll(/^## (.+)$/gm)].map((match) => match[1]);
  assert.deepEqual(headings, [
    "Start Here",
    "Evidence And Proof",
    "Limitations",
    "Demo",
    "Repository Docs",
    "Non-Claims"
  ]);

  assert.ok(outputs.llmsText.indexOf("Evidence") < outputs.llmsText.indexOf("Future Route"));
  assert.ok(outputs.llmsText.indexOf("Limitations") < outputs.llmsText.indexOf("Future Route"));
  assert.match(outputs.llmsText, /Public claim level: planned\. planned:/);
  assert.doesNotMatch(outputs.llmsText.split("## Non-Claims", 1)[0], /\b(?:available|shipped|released|deployed)\b/i);

  const nonClaims = splitLlmsSections(outputs.llmsText).get("Non-Claims");
  assert.match(nonClaims, /AI impact analysis/);
});

test("createDiscoveryOutputs rejects shipped wording for non-shipped claim levels", async () => {
  await assert.rejects(
    createDiscoveryOutputs([
      {
        ...siteEntry("/roadmap/"),
        publicClaimLevel: "dev-only",
        summary: "This shipped route is ready."
      }
    ]),
    /uses shipped wording for dev-only content/
  );
  await assert.rejects(
    createDiscoveryOutputs([
      {
        ...siteEntry("/roadmap/"),
        publicClaimLevel: "planned",
        limitations: ["This planned route is not shipped yet."]
      }
    ]),
    /uses shipped wording for planned content/
  );
});

test("createDiscoveryOutputs requires stable public refs for repo docs", async () => {
  await assert.rejects(
    createDiscoveryOutputs([
      {
        ...repoEntry("README.md"),
        url: "https://github.com/joefeser/tracemap/blob/codex/site-next-phase-20260616/README.md"
      }
    ]),
    /must pin repository docs to main or a release tag/
  );

  await assert.rejects(
    createDiscoveryOutputs([
      {
        ...repoEntry("README.md"),
        url: "https://tracemap.tools/docs/"
      }
    ]),
    /has non-public url: https:\/\/tracemap\.tools\/docs\//
  );
});

async function createDistWithRoutes(routes) {
  const dist = await mkdtemp(join(tmpdir(), "tracemap-discovery-test-"));

  for (const route of routes) {
    await mkdir(join(dist, `.${route}`), { recursive: true });
    await writeFile(join(dist, `.${route}`, "index.html"), "<!doctype html><html></html>", "utf8");
  }

  return dist;
}

function siteEntry(path) {
  return {
    path,
    title: `Route ${path}`,
    summary: "Deterministic static evidence route with bounded public wording.",
    publicClaimLevel: "demo",
    sourceType: "site-page",
    hintCategory: "start",
    limitations: ["Static evidence is bounded by coverage labels."],
    nonClaims: ["No runtime behavior or production usage proof."]
  };
}

function repoEntry(path) {
  return {
    url: `https://github.com/joefeser/tracemap/blob/main/${path}`,
    title: `Repo ${path}`,
    summary: "Source-of-truth repository document for public-safe discovery.",
    publicClaimLevel: "main",
    sourceType: "repo-doc",
    hintCategory: "repo-doc",
    limitations: ["Repository docs should be read with validation context."],
    nonClaims: ["No release approval proof."]
  };
}
