import assert from "node:assert/strict";
import { mkdir, mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { validateGroundingDist } from "./grounding.mjs";

test("validateGroundingDist accepts required boundary text", async (t) => {
  const dist = await fixture(t, requiredPage());
  const errors = [];

  await validateGroundingDist({ dist, errors });

  assert.deepEqual(errors, []);
});

test("validateGroundingDist rejects missing boundaries and positive claims", async (t) => {
  const dist = await fixture(t, "<main><p>TraceMap performs AI impact analysis.</p></main>");
  const errors = [];

  await validateGroundingDist({ dist, errors });

  assert.match(errors.join("\n"), /missing required boundary text/);
  assert.match(errors.join("\n"), /forbidden positive claim/);
});

async function fixture(t, html) {
  const root = await mkdtemp(join(tmpdir(), "tracemap-grounding-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  const dist = join(root, "dist");
  await mkdir(join(dist, "grounding"), { recursive: true });
  await writeFile(join(dist, "grounding", "index.html"), html);
  return dist;
}

function requiredPage() {
  return `<main>${[
    "Public claim level: concept",
    "The language model is yours and lives outside TraceMap",
    "No LLM, embedding, vector database, or prompt classification runs in the scanner or reducer",
    "Grounding constrains the model; it does not certify it",
    "a grounded answer is still a draft for human review",
    "TraceMap does not prove runtime behavior",
    "TraceMap does not validate, score, or approve any model"
  ].map((value) => `<p>${value}</p>`).join("")}</main>`;
}
