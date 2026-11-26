import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { currentMasterclassRank, PARTICIPANT_COOKIE } from "./participant";

// Through the module's interface only: the backend is a stubbed fetch, and the Participant cookie comes
// from a mocked next/headers (there is no request outside Next).

let cookie: string | undefined = "participant-token";

vi.mock("next/headers", () => ({
  cookies: async () => ({ get: (name: string) => (name === PARTICIPANT_COOKIE && cookie ? { value: cookie } : undefined) }),
}));

function answer(status: number, body?: unknown) {
  vi.stubGlobal(
    "fetch",
    vi.fn(async () => (body === undefined ? new Response(null, { status }) : Response.json(body, { status }))),
  );
}

describe("the Participant's rank on the Masterclass leaderboard", () => {
  beforeEach(() => {
    vi.unstubAllGlobals();
    cookie = "participant-token";
  });
  afterEach(() => vi.unstubAllGlobals());

  it("is their rank and points out of everyone on the leaderboard", async () => {
    answer(200, { rank: 2, points: 875, of: 14 });

    expect(await currentMasterclassRank()).toEqual({ rank: 2, points: 875, of: 14 });
    expect(fetch).toHaveBeenCalledWith(
      expect.stringMatching(/\/api\/participants\/me\/rank$/),
      expect.objectContaining({ headers: { cookie: `${PARTICIPANT_COOKIE}=participant-token` } }),
    );
  });

  it("is none while they are not on the leaderboard", async () => {
    answer(404);

    expect(await currentMasterclassRank()).toBeNull();
  });

  it("is none for a browser with no known Participant", async () => {
    answer(401);
    expect(await currentMasterclassRank()).toBeNull();

    cookie = undefined;
    expect(await currentMasterclassRank()).toBeNull();
  });

  it("fails on any other answer from the backend", async () => {
    answer(500, { title: "Server error" });

    await expect(currentMasterclassRank()).rejects.toThrow("HTTP 500");
  });
});
