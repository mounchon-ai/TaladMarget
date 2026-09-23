import { describe, expect, it } from "vitest";
import { decideRoute, safeNext } from "@/lib/auth-routing";

describe("FE-talad-002 · AC-talad-036 · pages need a signed-in user", () => {
  it("sends someone who is not signed in from the sales page to sign-in, remembering where they were going", () => {
    expect(decideRoute("/", "", false)).toEqual({ kind: "redirect", to: "/login?next=%2F" });
  });

  it("keeps the query string of the page they opened", () => {
    expect(decideRoute("/history", "?page=2", false)).toEqual({ kind: "redirect", to: "/login?next=%2Fhistory%3Fpage%3D2" });
  });

  it("lets a signed-in user through", () => {
    expect(decideRoute("/", "", true)).toEqual({ kind: "pass" });
  });

  it("opens sign-in for someone who is not signed in", () => {
    expect(decideRoute("/login", "", false)).toEqual({ kind: "pass" });
  });

  it("sends someone already signed in away from sign-in to the sales page", () => {
    expect(decideRoute("/login", "", true)).toEqual({ kind: "redirect", to: "/" });
  });
});

describe("FE-talad-002 · AC-talad-036 · after sign-in, return only to a page of this app", () => {
  it.each([
    ["/", "/"],
    ["/history?page=2", "/history?page=2"],
    [null, "/"],
    ["", "/"],
    ["https://evil.example/", "/"],
    ["//evil.example/", "/"],
    ["/\\evil.example", "/"],
  ])("safeNext(%j) is %j", (input, expected) => {
    expect(safeNext(input)).toBe(expected);
  });
});
