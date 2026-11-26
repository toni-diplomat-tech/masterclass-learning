import { describe, expect, it } from "vitest";
import { belongsOnPlay, type Participant } from "./participant";

// The homepage sends a phone that has already joined straight to /play, but only while its Masterclass is Open.

function participant(masterclassStatus: Participant["masterclassStatus"]): Participant {
  return {
    id: 41,
    masterclassId: 7,
    masterclassName: "Dev team 2026",
    masterclassStatus,
    nickname: "Curious Otter",
    joinedAt: "2026-01-15T09:00:00Z",
  };
}

describe("belongsOnPlay", () => {
  it("sends a Participant of a Masterclass Open to /play", () => {
    expect(belongsOnPlay(participant("Open"))).toBe(true);
  });

  it("keeps a Participant of a Masterclass Closed on the Join code form", () => {
    expect(belongsOnPlay(participant("Closed"))).toBe(false);
  });

  it("keeps a browser without a known Participant on the Join code form", () => {
    expect(belongsOnPlay(null)).toBe(false);
  });
});
