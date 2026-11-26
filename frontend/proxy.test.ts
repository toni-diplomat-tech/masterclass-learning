import { NextRequest } from "next/server";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { MasterclassStatus } from "./lib/domain";
import { proxy } from "./proxy";

// The homepage sends a phone that has already joined straight to /play, before the Join code form is
// rendered, but only while its Masterclass is Masterclass Open. The backend (`/api/participants/me`)
// is faked at its HTTP boundary.

const backend = { participants: new Map<string, MasterclassStatus>(), calls: 0, unreachable: false, failing: false };

function participant(masterclassStatus: MasterclassStatus) {
  return {
    id: 41,
    masterclassId: 7,
    masterclassName: "Dev team 2026",
    masterclassStatus,
    nickname: "Curious Otter",
    joinedAt: "2026-01-15T09:00:00Z",
  };
}

async function fakeBackend(input: string, init?: RequestInit): Promise<Response> {
  backend.calls++;
  if (backend.unreachable) throw new TypeError("fetch failed");
  if (backend.failing) return new Response(null, { status: 500 });
  expect(input).toMatch(/\/api\/participants\/me$/);
  const token = /mq_participant=([^;]+)/.exec(new Headers(init?.headers).get("cookie") ?? "")?.[1];
  const status = token ? backend.participants.get(token) : undefined;
  return status ? Response.json(participant(status)) : new Response(null, { status: 401 });
}

beforeEach(() => {
  backend.participants.clear();
  backend.calls = 0;
  backend.unreachable = false;
  backend.failing = false;
  vi.stubGlobal("fetch", fakeBackend);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

function openHomepage(path: string, participantToken?: string) {
  const headers = participantToken ? { cookie: `mq_participant=${participantToken}` } : undefined;
  return proxy(new NextRequest(new URL(path, "http://quiz.test"), { headers }));
}

/** Where the browser is sent: the redirect target, or null when the homepage (the Join code form) renders. */
async function sentTo(response: Response | Promise<Response>): Promise<string | null> {
  const location = (await response).headers.get("location");
  return location ? new URL(location).pathname : null;
}

describe("the homepage", () => {
  it("redirects a Participant of a Masterclass Open to /play", async () => {
    backend.participants.set("otter", "Open");

    const response = await openHomepage("/", "otter");

    expect(response.status).toBe(307);
    expect(await sentTo(response)).toBe("/play");
  });

  it("redirects a Participant of a Masterclass Open who scans a QR link to /play", async () => {
    backend.participants.set("otter", "Open");

    expect(await sentTo(openHomepage("/?code=123456", "otter"))).toBe("/play");
  });

  it("shows the Join code form to a Participant of a Masterclass Closed", async () => {
    backend.participants.set("otter", "Closed");

    expect(await sentTo(openHomepage("/?code=123456", "otter"))).toBeNull();
  });

  it("shows the Join code form for a cookie the backend doesn't know", async () => {
    expect(await sentTo(openHomepage("/", "stranger"))).toBeNull();
  });

  it("shows the Join code form without asking the backend when there is no cookie", async () => {
    expect(await sentTo(openHomepage("/"))).toBeNull();
    expect(backend.calls).toBe(0);
  });

  it("shows the Join code form when the backend can't be reached", async () => {
    backend.unreachable = true;

    expect(await sentTo(openHomepage("/", "otter"))).toBeNull();
  });

  it("doesn't hide a backend error behind the Join code form", async () => {
    backend.failing = true;

    await expect(openHomepage("/", "otter")).rejects.toThrow(/HTTP 500/);
  });
});
