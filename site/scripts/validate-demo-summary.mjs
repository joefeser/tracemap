import { readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

import { defaultFixturePath, validateDemoSummaryFixture } from "./refresh-demo-summary.mjs";

const defaultRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const affectedPages = [
  "demo/result/index.html",
  "demo/proof-upgrades/index.html",
  "demo/proof-assets/index.html",
  "packets/index.html",
  "manager-packet/index.html",
  "capabilities/index.html"
];

const upgradedSectionNames = [
  "combine-and-dependency-report",
  "paths-and-reverse",
  "portfolio",
  "diff",
  "impact",
  "release-review"
];

const proofUpgradeCountChecks = [
  {
    section: "combine-and-dependency-report",
    labels: [
      ["sources", "sources"],
      ["endpoint findings", "endpointFindings"],
      ["dependency surfaces", "dependencySurfaces"],
      ["dependency edges", "dependencyEdges"],
      ["gaps", "gaps"]
    ]
  },
  {
    section: "paths-and-reverse",
    labels: [
      ["paths", "paths"],
      ["path gaps", "pathGaps"],
      ["reverse paths", "reversePaths"],
      ["reverse roots", "reverseRoots"],
      ["reverse gaps", "reverseGaps"],
      ["selected surfaces", "selectedSurfaces"]
    ]
  },
  {
    section: "portfolio",
    labels: [
      ["portfolio inputs", "portfolioInputs"],
      ["sources", "portfolioSources"],
      ["dependency surfaces", "dependencySurfaces"],
      ["dependency edges", "dependencyEdges"],
      ["endpoint findings", "endpointFindings"],
      ["gaps", "gaps"]
    ]
  },
  {
    section: "diff",
    labels: [
      ["diff rows", "diffRows"],
      ["surface diffs", "surfaceDiffs"],
      ["endpoint diffs", "endpointDiffs"],
      ["edge diffs", "edgeDiffs"],
      ["gaps", "gaps"]
    ]
  },
  {
    section: "impact",
    labels: [
      ["diff rows considered", "diffRows"],
      ["impact items", "impactItems"],
      ["surface impacts", "surfaceImpacts"],
      ["endpoint impacts", "endpointImpacts"],
      ["edge impacts", "edgeImpacts"],
      ["gaps", "gaps"]
    ]
  },
  {
    section: "release-review",
    labels: [
      ["findings", "findings"],
      ["top changed surfaces", "topChangedSurfaces"],
      ["contract findings", "contractFindings"],
      ["gaps", "gaps"],
      ["checklist items", "checklistItems"]
    ]
  }
];

export async function validateDemoSummary({ root = defaultRoot } = {}) {
  const errors = [];
  const fixture = await readFixture(errors, root);

  if (fixture) {
    errors.push(...validateDemoSummaryFixture(fixture));
    await validateAffectedPages({ errors, fixture, root });
  }

  if (errors.length > 0) {
    throw new Error(`Demo summary validation failed:\n- ${errors.join("\n- ")}`);
  }

  return {
    affectedPageCount: affectedPages.length,
    fixtureSectionCount: fixture.sections.length
  };
}

async function readFixture(errors, root) {
  const fixturePath = resolve(root, "src", "_data", "demo-public-summary.json");
  try {
    return JSON.parse(await readFile(fixturePath, "utf8"));
  } catch (error) {
    errors.push(`Unable to read demo summary fixture at ${relativeSitePath(root, fixturePath)}: ${error.message}`);
    return null;
  }
}

async function validateAffectedPages({ errors, fixture, root }) {
  const sections = new Map(fixture.sections.map((section) => [section.id, section]));
  const pages = new Map();

  for (const page of affectedPages) {
    const path = resolve(root, "src", page);
    try {
      pages.set(page, await readFile(path, "utf8"));
    } catch (error) {
      errors.push(`Unable to read affected page ${page}: ${error.message}`);
    }
  }

  if (pages.has("demo/result/index.html")) {
    validateResultPage(pages.get("demo/result/index.html"), sections, errors);
  }
  if (pages.has("demo/proof-upgrades/index.html")) {
    validateProofUpgradesPage(pages.get("demo/proof-upgrades/index.html"), sections, errors);
  }
  if (pages.has("demo/proof-assets/index.html")) {
    validateProofAssetsPage(pages.get("demo/proof-assets/index.html"), sections, errors);
  }
  if (pages.has("packets/index.html")) {
    validatePacketsPage(pages.get("packets/index.html"), sections, errors);
  }
  if (pages.has("manager-packet/index.html")) {
    validateManagerPacketPage(pages.get("manager-packet/index.html"), sections, errors);
  }
  if (pages.has("capabilities/index.html")) {
    validateCapabilitiesPage(pages.get("capabilities/index.html"), sections, errors);
  }
}

function validateResultPage(html, sections, errors) {
  const page = "demo/result/index.html";
  for (const section of sections.values()) {
    assertContains(html, section.name, page, `section name ${section.name}`, errors);
  }

  for (const id of ["toolchains", "build", "sample-scans"]) {
    const section = mustSection(sections, id, errors);
    if (section) {
      assertContains(html, `Status: ${section.status}`, page, `${id} status`, errors);
    }
  }

  for (const section of upgradedSections(sections, errors)) {
    assertContains(html, `${section.name}</strong><span>Available as demo evidence`, page, `${section.id} available label`, errors);
  }

  assertContains(html, "public.demo.summary.v1", page, "demo summary rule ID", errors);
  assertContains(html, "Tier2Structural", page, "available evidence tier", errors);
  assertContains(html, "Tier4Unknown", page, "unknown evidence tier", errors);
  assertContains(html, "demo-summary.*", page, "public-safe summary family", errors);
  assertContains(html, "scans/**/facts.ndjson", page, "local-only facts family", errors);
}

function validateProofUpgradesPage(html, sections, errors) {
  const page = "demo/proof-upgrades/index.html";

  for (const check of proofUpgradeCountChecks) {
    const section = mustSection(sections, check.section, errors);
    if (!section) {
      continue;
    }

    assertContains(html, `<h3>${section.name}</h3>`, page, `${section.id} card`, errors);
    assertContains(html, `Coverage: ${section.coverage}`, page, `${section.id} coverage`, errors);
    assertContains(html, `Tier: ${section.evidenceTier}`, page, `${section.id} tier`, errors);

    const cardText = extractArticleText(html, section.name);
    for (const [label, key] of check.labels) {
      const expected = section.counts[key];
      const actual = extractCount(cardText, label);
      if (actual !== expected) {
        errors.push(`${page} ${section.id} count ${key} must be ${expected}; found ${actual ?? "<missing>"}.`);
      }
    }
  }

  for (const family of publicReportFamilies(sections)) {
    assertContains(html, family, page, `public report family ${family}`, errors);
  }
}

function validateProofAssetsPage(html, sections, errors) {
  const page = "demo/proof-assets/index.html";
  const paths = mustSection(sections, "paths-and-reverse", errors);
  const impact = mustSection(sections, "impact", errors);
  const releaseReview = mustSection(sections, "release-review", errors);

  assertContains(html, "public.demo.summary.v1", page, "demo summary rule ID", errors);
  assertContains(html, "Tier2Structural", page, "available evidence tier", errors);
  assertContains(html, "Coverage: PartialAnalysis", page, "partial coverage label", errors);

  if (paths) {
    const pathsText = extractArticleText(html, "Paths and reverse");
    for (const [label, key] of [
      ["paths", "paths"],
      ["reverse paths", "reversePaths"],
      ["path gaps", "pathGaps"],
      ["reverse gaps", "reverseGaps"]
    ]) {
      const expected = paths.counts[key];
      const actual = extractCount(pathsText, label);
      if (actual !== expected) {
        errors.push(`${page} paths visual count ${key} must be ${expected}; found ${actual ?? "<missing>"}.`);
      }
    }
  }

  if (impact) {
    const impactText = extractArticleText(html, "Diff and impact");
    for (const [label, key] of [
      ["diff rows considered", "diffRows"],
      ["surface impacts", "surfaceImpacts"],
      ["endpoint impacts", "endpointImpacts"],
      ["edge impacts", "edgeImpacts"],
      ["gaps", "gaps"]
    ]) {
      const expected = impact.counts[key];
      const actual = extractCount(impactText, label);
      if (actual !== expected) {
        errors.push(`${page} impact visual count ${key} must be ${expected}; found ${actual ?? "<missing>"}.`);
      }
    }
  }

  if (releaseReview) {
    assertContains(html, `<strong>${releaseReview.counts.findings}</strong><span>static findings</span>`, page, "release-review findings visual", errors);
    assertContains(html, `<strong>${releaseReview.counts.checklistItems}</strong><span>checklist items</span>`, page, "release-review checklist visual", errors);
    assertContains(html, `<strong>${releaseReview.counts.gaps}</strong><span>gaps</span>`, page, "release-review gaps visual", errors);
  }

  for (const family of publicReportFamilies(sections)) {
    assertContains(html, family, page, `public report family ${family}`, errors);
  }
}

function validatePacketsPage(html, sections, errors) {
  const page = "packets/index.html";
  assertContains(html, "demo-summary.md", page, "public-safe summary artifact", errors);
  assertContains(html, "rule IDs", page, "rule ID framing", errors);
  assertContains(html, "evidence tiers", page, "evidence tier framing", errors);
  assertContains(html, "coverage labels", page, "coverage label framing", errors);
  assertContains(html, "facts.ndjson", page, "local-only facts boundary", errors);
  assertContains(html, "index.sqlite", page, "local-only SQLite boundary", errors);
  assertContains(html, sections.get("paths-and-reverse")?.evidenceTier ?? "Tier2Structural", page, "example evidence tier", errors);
}

function validateManagerPacketPage(html, sections, errors) {
  const page = "manager-packet/index.html";
  for (const section of upgradedSections(sections, errors)) {
    assertContains(html, managerProofLabel(section.name), page, `${section.id} proof reference`, errors);
  }
  assertContains(html, "demo-summary.md", page, "public-safe summary artifact", errors);
  assertContains(html, "PartialAnalysis", page, "coverage label", errors);
  assertContains(html, "facts.ndjson", page, "local-only facts boundary", errors);
  assertContains(html, "index.sqlite", page, "local-only SQLite boundary", errors);
}

function validateCapabilitiesPage(html, sections, errors) {
  const page = "capabilities/index.html";
  assertContains(html, "Status:</strong> demo", page, "scan demo status", errors);
  assertContains(html, "scripts/demo-public.sh", page, "demo script proof path", errors);
  assertContains(html, "demo-summary.md", page, "public summary Markdown", errors);
  assertContains(html, "demo-summary.json", page, "public summary JSON", errors);
  assertContains(html, "reports/**/*.md", page, "public report family", errors);
  assertContains(html, "index.sqlite", page, "local-only SQLite boundary", errors);
  assertContains(html, sections.get("sample-scans")?.status ?? "available", page, "sample scan status source", errors);
}

function upgradedSections(sections, errors) {
  return upgradedSectionNames.map((id) => mustSection(sections, id, errors)).filter(Boolean);
}

function publicReportFamilies(sections) {
  const families = new Set();
  for (const section of upgradedSections(sections, [])) {
    for (const artifact of section.artifacts) {
      const match = artifact.match(/^(reports\/[^/]+)\/.+/);
      if (match) {
        families.add(`${match[1]}/**`);
      } else if (artifact === "portfolio-manifest.json") {
        families.add("portfolio-manifest.json");
      }
    }
  }
  return [...families].sort();
}

function mustSection(sections, id, errors) {
  const section = sections.get(id);
  if (!section) {
    errors.push(`Fixture is missing section ${id}.`);
  }
  return section;
}

function assertContains(html, needle, page, label, errors) {
  if (!needle || !html.includes(needle)) {
    errors.push(`${page} is missing ${label}: ${needle}`);
  }
}

function extractArticleText(html, heading) {
  const escapedHeading = escapeRegExp(heading);
  const match = html.match(new RegExp(`<h3>${escapedHeading}</h3>([\\s\\S]*?)<\\/article>`));
  return match ? htmlText(match[1]) : "";
}

function extractCount(text, label) {
  const normalized = htmlText(text);
  const escapedLabel = escapeRegExp(label);
  const match = normalized.match(new RegExp(`(?:^|[^0-9])([0-9]+) ${escapedLabel}(?:[^a-zA-Z]|$)`, "i"));
  return match ? Number(match[1]) : null;
}

function htmlText(value) {
  return String(value)
    .replace(/<[^>]+>/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

function managerProofLabel(sectionName) {
  const labels = new Map([
    ["combine-and-dependency-report", "combined reports"],
    ["paths-and-reverse", "paths, reverse lookup"],
    ["release-review", "release review"]
  ]);
  return labels.get(sectionName) ?? sectionName.replaceAll("-", " ");
}

function escapeRegExp(value) {
  return String(value).replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function relativeSitePath(root, path) {
  return path.replace(`${root}/`, "");
}

async function main() {
  await validateDemoSummary();
  console.log(`Validated demo summary fixture: ${defaultFixturePath.replace(`${defaultRoot}/`, "")}`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => {
    console.error(error.message);
    process.exit(1);
  });
}
