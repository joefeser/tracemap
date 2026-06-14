import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { hash } from "../src/util/Hash";
import { queryShape, queryShapeProperties } from "../src/sql/SqlShape";

const repoRoot = path.resolve(process.cwd(), "../..");

interface Fixture {
  name: string;
  sql: string;
  sqlSourceKind: string;
  textHash: string;
  queryShapeHash: string;
  operationName?: string;
  tableNames?: string;
  columnNames?: string;
}

describe("SqlShape", () => {
  it("matches the Python v1 golden fixture", () => {
    for (const fixture of readFixtures()) {
      const shape = queryShape(fixture.sql);

      expect(hash(fixture.sql, 32)).toBe(fixture.textHash);
      expect(shape.queryShapeHash).toBe(fixture.queryShapeHash);
      expect(shape.operationName || undefined).toBe(fixture.operationName);
      expect(shape.tableNames.join(";") || undefined).toBe(fixture.tableNames);
      expect(shape.columnNames.join(";") || undefined).toBe(fixture.columnNames);
    }
  });

  it("keeps WITH CTE shapes hash-only", () => {
    const fixture = readFixtures().find((item) => item.name === "with-cte")!;
    const props = queryShapeProperties(fixture.sql, fixture.sqlSourceKind);

    expect(props.queryShapeHash).toBe(fixture.queryShapeHash);
    expect(props.sqlSourceKind).toBe("sql-file");
    expect(props.operationName).toBeUndefined();
    expect(props.tableName).toBeUndefined();
    expect(props.columnNames).toBeUndefined();
  });
});

function readFixtures(): Fixture[] {
  return JSON.parse(fs.readFileSync(path.join(repoRoot, "samples/sql-shape-fixtures/sql-shape-v1.json"), "utf8")).cases;
}
