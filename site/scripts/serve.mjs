import { createReadStream } from "node:fs";
import { stat } from "node:fs/promises";
import { extname, join, normalize, resolve, sep } from "node:path";
import { createServer } from "node:http";
import { dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { buildSite } from "./build.mjs";

await buildSite();

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..", "dist");
const port = Number.parseInt(process.env.PORT ?? "4173", 10);

const contentTypes = {
  ".css": "text/css; charset=utf-8",
  ".html": "text/html; charset=utf-8",
  ".ico": "image/x-icon",
  ".js": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".svg": "image/svg+xml; charset=utf-8",
  ".txt": "text/plain; charset=utf-8",
  ".xml": "application/xml; charset=utf-8"
};

const server = createServer(async (request, response) => {
  let requestedPath;

  try {
    const url = new URL(request.url ?? "/", "http://localhost");
    const safePath = normalize(decodeURIComponent(url.pathname)).replace(/^(\.\.[/\\])+/, "");
    requestedPath = resolve(root, `.${safePath}`);
  } catch (error) {
    response.writeHead(error instanceof URIError ? 400 : 500, {
      "content-type": "text/plain; charset=utf-8"
    });
    response.end(error instanceof URIError ? "Bad request" : "Internal server error");
    return;
  }

  const safeRoot = root.endsWith(sep) ? root : root + sep;
  if (requestedPath !== root && !requestedPath.startsWith(safeRoot)) {
    response.writeHead(403);
    response.end("Forbidden");
    return;
  }

  const filePath = await resolveFile(requestedPath);

  if (!filePath) {
    response.writeHead(404, { "content-type": "text/plain; charset=utf-8" });
    response.end("Not found");
    return;
  }

  const stream = createReadStream(filePath);

  stream.once("open", () => {
    response.writeHead(200, {
      "content-type": contentTypes[extname(filePath)] ?? "application/octet-stream"
    });
    stream.pipe(response);
  });

  stream.once("error", () => {
    if (!response.headersSent) {
      response.writeHead(500, { "content-type": "text/plain; charset=utf-8" });
      response.end("Internal server error");
      return;
    }

    response.destroy();
  });
});

server.listen(port, () => {
  console.log(`Serving TraceMap site at http://localhost:${port}`);
});

async function resolveFile(pathname) {
  try {
    const info = await stat(pathname);
    if (info.isDirectory()) {
      return resolveFile(join(pathname, "index.html"));
    }
    if (info.isFile()) {
      return pathname;
    }
  } catch {
    return null;
  }

  return null;
}
