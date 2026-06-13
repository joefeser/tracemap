export interface NormalizedEndpointRoute {
  pathTemplate: string;
  pathKey: string;
  parameterNames: string[];
  optionalParameterNames: string[];
  routeConstraints: string[];
  queryParameterNames: string[];
  hasQueryParameters: boolean;
  staticMatchQuality: string;
}

export function normalizeEndpointRoute(routeTemplate: string, basePathPrefix = ""): NormalizedEndpointRoute {
  let value = stripSchemeHost(stripFragment(routeTemplate.trim()));
  const query = splitQuery(value);
  value = query.path;
  if (basePathPrefix && !startsWithPathPrefix(value, basePathPrefix)) {
    value = combinePath(basePathPrefix, value);
  }
  value = normalizeSlashes(value);

  const parameterNames = new Set<string>();
  const optionalParameterNames = new Set<string>();
  const routeConstraints = new Set<string>();
  const templateSegments: string[] = [];
  const keySegments: string[] = [];

  for (const rawSegment of value.split("/").filter(Boolean)) {
    const segment = decodeURIComponentSafe(rawSegment);
    const parsed = parseAspNetParameter(segment);
    if (parsed) {
      parameterNames.add(parsed.name);
      if (parsed.optional) {
        optionalParameterNames.add(parsed.name);
      }
      if (parsed.constraint) {
        routeConstraints.add(`${parsed.name}:${parsed.constraint}`);
      }
      templateSegments.push(`{${parsed.name}${parsed.optional ? "?" : ""}}`);
      keySegments.push(parsed.optional ? "{?}" : "{}");
      continue;
    }

    const clientTemplate = segment.replace(/\{([A-Za-z_][A-Za-z0-9_]*)\}/g, (_match, name: string) => {
      parameterNames.add(name);
      return `{${name}}`;
    });
    templateSegments.push(clientTemplate);
    keySegments.push(clientTemplate.replace(/\{[A-Za-z_][A-Za-z0-9_]*\??\}/g, "{}").toLowerCase());
  }

  const pathTemplate = `/${templateSegments.join("/")}`;
  const pathKey = pathTemplate === "/" ? "/" : `/${keySegments.join("/")}`;
  return {
    pathTemplate,
    pathKey,
    parameterNames: [...parameterNames].sort(),
    optionalParameterNames: [...optionalParameterNames].sort(),
    routeConstraints: [...routeConstraints].sort(),
    queryParameterNames: query.queryParameterNames,
    hasQueryParameters: query.queryParameterNames.length > 0,
    staticMatchQuality: optionalParameterNames.size > 0 ? "OptionalSegments" : "Exact"
  };
}

function stripSchemeHost(value: string): string {
  try {
    const url = new URL(value);
    return `${url.pathname}${url.search}`;
  } catch {
    return value;
  }
}

function stripFragment(value: string): string {
  const index = value.indexOf("#");
  return index >= 0 ? value.slice(0, index) : value;
}

function splitQuery(value: string): { path: string; queryParameterNames: string[] } {
  const index = findQueryStart(value);
  if (index < 0) {
    return { path: value, queryParameterNames: [] };
  }
  const queryParameterNames = value
    .slice(index + 1)
    .split("&")
    .map((part) => part.split("=")[0])
    .filter(Boolean)
    .map(decodeURIComponentSafe)
    .sort();
  return { path: value.slice(0, index), queryParameterNames: [...new Set(queryParameterNames)] };
}

function findQueryStart(value: string): number {
  let braceDepth = 0;
  for (let index = 0; index < value.length; index++) {
    const current = value[index];
    if (current === "{") {
      braceDepth++;
    } else if (current === "}" && braceDepth > 0) {
      braceDepth--;
    } else if (current === "?" && braceDepth === 0) {
      return index;
    }
  }
  return -1;
}

function startsWithPathPrefix(value: string, basePathPrefix: string): boolean {
  const path = normalizeSlashes(value).replace(/\/$/, "");
  const prefix = normalizeSlashes(basePathPrefix).replace(/\/$/, "");
  return path.toLowerCase() === prefix.toLowerCase() || path.toLowerCase().startsWith(`${prefix.toLowerCase()}/`);
}

function combinePath(left: string, right: string): string {
  if (!left) {
    return right;
  }
  if (!right) {
    return left;
  }
  return `${left.replace(/\/+$/, "")}/${right.replace(/^\/+/, "")}`;
}

function normalizeSlashes(value: string): string {
  let normalized = value.replace(/\\/g, "/").replace(/\/+/g, "/");
  if (!normalized.startsWith("/")) {
    normalized = `/${normalized}`;
  }
  return normalized.length > 1 ? normalized.replace(/\/+$/, "") : normalized;
}

function parseAspNetParameter(segment: string): { name: string; optional: boolean; constraint: string } | null {
  const match = segment.match(/^\{(?<name>\*?[A-Za-z_][A-Za-z0-9_]*)(:(?<constraint>[^}?]+))?(?<optional>\?)?\}$/);
  if (!match?.groups) {
    return null;
  }
  const name = match.groups.name.replace(/^\*/, "");
  return {
    name,
    optional: Boolean(match.groups.optional),
    constraint: match.groups.constraint ?? ""
  };
}

function decodeURIComponentSafe(value: string): string {
  try {
    return decodeURIComponent(value);
  } catch {
    return value;
  }
}
