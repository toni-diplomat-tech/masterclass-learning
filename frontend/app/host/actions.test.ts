import { afterEach, describe, expect, it, vi } from "vitest";
import { HOST_COOKIE } from "@/lib/host-cookie";
import { closeMasterclassAction, openMasterclassAction } from "./actions";

// The Host's Server Actions through their interface only: the backend is a stubbed fetch, the Host
// session cookie comes from a mocked next/headers, and Next's refresh and redirect are stand-ins (there
// is no request outside Next).

vi.mock("next/headers", () => ({
  cookies: async () => ({ get: (name: string) => (name === HOST_COOKIE ? { value: "session" } : undefined) }),
  headers: async () => new Headers(),
}));
vi.mock("next/cache", () => ({ refresh: () => undefined }));
vi.mock("next/navigation", () => ({
  redirect: (path: string) => {
    throw new Error(`redirect to ${path}`);
  },
  unstable_rethrow: () => undefined,
}));

const DRAFT = 12;

function answer(status: number, body: unknown) {
  vi.stubGlobal("fetch", vi.fn(async () => Response.json(body, { status })));
}

describe("the Host opening a Masterclass", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("opens a Draft when no other Masterclass is Open", async () => {
    answer(200, { id: DRAFT, name: "QA team 2026", status: "Open", joinCode: "123456" });

    expect(await openMasterclassAction(DRAFT)).toEqual({});
  });

  it("tells the Host which Masterclass to close first when another one is Open", async () => {
    answer(409, { title: "The Masterclass was not opened.", status: 409, problems: ['Close "Dev team 2026" first.'] });

    expect(await openMasterclassAction(DRAFT)).toEqual({ problems: ['Close "Dev team 2026" first.'] });
  });

  it("asks the Host to try again when the backend fails", async () => {
    answer(500, { title: "Server error" });

    expect(await openMasterclassAction(DRAFT)).toEqual({ error: "Opening the Masterclass failed. Try again." });
  });
});

describe("the Host closing a Masterclass", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("tells the Host why the Masterclass can't close yet", async () => {
    answer(409, {
      title: "The Masterclass was not closed.",
      status: 409,
      problems: ["A Session is still running; close the Masterclass once it has ended."],
    });

    expect(await closeMasterclassAction(DRAFT)).toEqual({
      problems: ["A Session is still running; close the Masterclass once it has ended."],
    });
  });
});
