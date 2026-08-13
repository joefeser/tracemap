import { createHash } from "node:crypto";

export function hash(value: string, length = 20): string {
  return createHash("sha256").update(value, "utf8").digest("hex").slice(0, length);
}

export function hashObject(value: unknown, length = 20): string {
  return hash(JSON.stringify(value), length);
}

export function hashBytes(value: Uint8Array, length = 64): string {
  return createHash("sha256").update(value).digest("hex").slice(0, length);
}
