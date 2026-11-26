import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { HOST_COOKIE } from "./host-cookie";
import { openMasterclass } from "./masterclasses";

// Through the module's interface only: the backend is a stubbed fetch, and the Host session cookie comes
// from a mocked next/headers (there is no request outside Next).

vi.mock("next/headers", () => ({
  cookies: async () => ({ get: (name: string) => (name === HOST_COOKIE ? { value: "session" } : undefined) }),
}));
vi.mock("next/navigation", () => ({
  redirect: (path: string) => {
    throw new Error(`redirect to ${path}`);
  },
}));

const DRAFT = 12;

function answer(status: number, body: unknown) {
  vi.stubGlobal("fetch", vi.fn(async () => Response.json(body, { status })));
}

describe("opening a Masterclass", () => {
  beforeEach(() => vi.unstubAllGlobals());
  afterEach(() => vi.unstubAllGlobals());

  it("opens a Draft when no other Masterclass is Open", async () => {
    answer(200, { id: DRAFT, name: "QA team 2026", status: "Open", joinCode: "123456" });

    expect(await openMasterclass(DRAFT)).toEqual({ problems: [] });
  });

  it("says which Masterclass to close first when another one is Open", async () => {
    answer(409, { title: "The Masterclass was not opened.", status: 409, problems: ['Close "Dev team 2026" first.'] });

    expect(await openMasterclass(DRAFT)).toEqual({ problems: ['Close "Dev team 2026" first.'] });
  });

  it("fails on any other answer from the backend", async () => {
    answer(500, { title: "Server error" });

    await expect(openMasterclass(DRAFT)).rejects.toThrow("HTTP 500");
  });
});
