import { expect, it } from "vitest";
import worker from "../fixtures/LanguageProfile/dist/worker.js";

it("runs typed queries, composite joins and grouping, continuations, and native string ordering", async () => {
  const response = await worker.fetch(new Request("https://worker.test/queries"), {}, {});
  await expect(response.json()).resolves.toEqual({
    named: [2, 4],
    conditional: [18],
    nativeEnums: ["json", "v8"],
    typedGroups: [1, 2],
    typedOrdered: [1, 2, 3],
    groups: [{ odd: 1, values: [10, 30] }, { odd: 0, values: [20, 40] }],
    joined: [8, 6, 6, 6],
    groupedJoin: [{ a: 3, count: 1 }, { a: 4, count: 1 }],
    words: ["aa", "A", "a", "b"],
  });
});
