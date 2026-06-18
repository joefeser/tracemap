import { readFile, stat } from "node:fs/promises";

export async function fileExists(path) {
  try {
    return (await stat(path)).isFile();
  } catch {
    return false;
  }
}

export function normalizeBaseUrl(value) {
  return String(value).replace(/\/+$/, "");
}

export async function readSitemapLocSet(path) {
  const sitemap = await readFile(path, "utf8");
  return new Set(
    [...sitemap.matchAll(/<loc>\s*([^<]+?)\s*<\/loc>/g)].map((match) => decodeHtmlEntities(match[1].trim()))
  );
}

export function normalizeRenderedText(html) {
  return decodeHtmlEntities(html)
    .replace(/<[^>]+>/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

export function decodeHtmlEntities(value) {
  return String(value).replace(/&(#x[0-9a-f]+|#[0-9]+|[a-z][a-z0-9]+);/gi, (entity, token) => {
    const normalized = token.toLowerCase();
    if (normalized.startsWith("#x")) {
      return decodeCodePoint(Number.parseInt(normalized.slice(2), 16), entity);
    }

    if (normalized.startsWith("#")) {
      return decodeCodePoint(Number.parseInt(normalized.slice(1), 10), entity);
    }

    return (
      {
        amp: "&",
        apos: "'",
        backslash: "\\",
        bsol: "\\",
        colon: ":",
        gt: ">",
        lt: "<",
        nbsp: " ",
        quot: "\"",
        sol: "/"
      }[normalized] ?? entity
    );
  });
}

export function escapeRegExp(value) {
  return String(value).replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function decodeCodePoint(codePoint, fallback) {
  if (!Number.isFinite(codePoint)) {
    return fallback;
  }

  try {
    return String.fromCodePoint(codePoint);
  } catch {
    return fallback;
  }
}
