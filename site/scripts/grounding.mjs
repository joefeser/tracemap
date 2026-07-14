import { readFile } from "node:fs/promises";
import { resolve } from "node:path";

import {
  decodeHtmlEntities,
  fileExists,
  normalizeBaseUrl,
  normalizeRenderedText,
  readSitemapLocSet
} from "./validate-utils.mjs";

export const groundingRoute = "/grounding/";

const pageArtifact = "grounding/index.html";
const requiredText = [
  "Public claim level: concept",
  "The language model is yours and lives outside TraceMap",
  "No LLM, embedding, vector database, or prompt classification runs in the scanner or reducer",
  "Grounding constrains the model; it does not certify it",
  "a grounded answer is still a draft for human review",
  "TraceMap does not prove runtime behavior",
  "TraceMap does not validate, score, or approve any model"
];

const forbiddenClaims = [
  /TraceMap (?:performs|provides|uses|runs|offers) (?:AI|LLM) (?:impact )?analysis/i,
  /grounding (?:guarantees|certifies|proves) (?:correctness|accuracy|safety)/i,
  /(?:approved|safe|ready) to (?:merge|ship|release)/i,
  /(?:proves|confirms) runtime (?:behavior|reachability|impact)/i
];

const forbiddenPrivateText = ["/Users/", "C:\\", "file://", "localhost", "127.0.0.1"];

export async function validateGroundingDist({ baseUrl = "https://tracemap.tools", dist, errors }) {
  const localErrors = [];
  const cleanBaseUrl = normalizeBaseUrl(baseUrl, localErrors);
  const pagePath = resolve(dist, "grounding", "index.html");

  if (!(await fileExists(pagePath))) {
    localErrors.push(withEvidence(`Grounding page is missing required public route: ${groundingRoute}`, pageArtifact));
    errors.push(...localErrors);
    return;
  }

  if (cleanBaseUrl) {
    const sitemapPath = resolve(dist, "sitemap.xml");
    if (await fileExists(sitemapPath)) {
      const urls = await readSitemapLocSet(sitemapPath);
      if (!urls.has(`${cleanBaseUrl}${groundingRoute}`)) {
        localErrors.push(withEvidence(`Grounding sitemap is missing required route: ${cleanBaseUrl}${groundingRoute}`, "sitemap.xml"));
      }
    }
  }

  await validateRoutesIndex({ dist, errors: localErrors });
  await validatePage({ pagePath, errors: localErrors });
  errors.push(...localErrors);
}

async function validateRoutesIndex({ dist, errors }) {
  const path = resolve(dist, "routes-index.json");
  if (!(await fileExists(path))) return;

  let parsed;
  try {
    parsed = JSON.parse(await readFile(path, "utf8"));
  } catch (error) {
    errors.push(withEvidence(`Grounding could not parse routes-index.json: ${error.message}`, "routes-index.json"));
    return;
  }

  const entry = Array.isArray(parsed?.entries)
    ? parsed.entries.find((candidate) => candidate?.path === groundingRoute)
    : null;
  if (!entry) {
    errors.push(withEvidence(`Grounding routes-index.json is missing required route: ${groundingRoute}`, "routes-index.json"));
    return;
  }

  for (const [field, expected] of Object.entries({
    publicClaimLevel: "concept",
    sourceType: "site-page",
    hintCategory: "use-case",
    preferredProofPath: "/evidence/"
  })) {
    if (entry[field] !== expected) {
      errors.push(withEvidence(`Grounding routes-index.json expected ${field} ${expected}, got ${String(entry[field])}`, "routes-index.json"));
    }
  }

  if (!Array.isArray(entry.limitations) || entry.limitations.length < 2 || !Array.isArray(entry.nonClaims) || entry.nonClaims.length < 2) {
    errors.push(withEvidence("Grounding routes-index.json must preserve limitations and nonClaims metadata.", "routes-index.json"));
  }
}

async function validatePage({ pagePath, errors }) {
  const html = await readFile(pagePath, "utf8");
  const decoded = decodeHtmlEntities(html);
  const text = normalizeRenderedText(html);

  for (const phrase of requiredText) {
    if (!text.toLowerCase().includes(phrase.toLowerCase())) {
      errors.push(withEvidence(`Grounding page is missing required boundary text: ${phrase}`, pageArtifact));
    }
  }

  for (const pattern of forbiddenClaims) {
    const match = `${decoded} ${text}`.match(pattern);
    if (match) errors.push(withEvidence(`Grounding page contains forbidden positive claim: ${match[0]}`, pageArtifact));
  }

  for (const value of forbiddenPrivateText) {
    if (`${decoded} ${text}`.toLowerCase().includes(value.toLowerCase())) {
      errors.push(withEvidence(`Grounding page contains forbidden private text: ${value}`, pageArtifact));
    }
  }
}

function withEvidence(message, artifact) {
  return `${message} Evidence: ${artifact}.`;
}
