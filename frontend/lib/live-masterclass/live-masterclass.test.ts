import { afterEach, describe, expect, it, vi } from "vitest";
import { createFakeServer, type FakeServer } from "./fake-hub";
import type { CurrentState } from "./hub";
import { createLivePhone, createLiveProjector, type QuestionRevealed, type QuestionStarted, type SessionLeaderboard } from "./index";

// Through the module's interface only: a store from createLiveProjector/createLivePhone over the fake hub.

const MASTERCLASS = 7;
const OTHER_MASTERCLASS = 8;
const ME = 41;

const settle = () => new Promise<void>((resolve) => setTimeout(resolve, 0));

function question(sessionId: number, number: number, questionId = sessionId * 100 + number): QuestionStarted {
  return {
    masterclassId: MASTERCLASS,
    sessionId,
    workshopTitle: "Unit tests",
    questionId,
    number,
    questionCount: 3,
    difficulty: "Easy",
    text: `Question ${number} on writing your first test`,
    options: [
      { position: 1, text: "Arrange, act, assert" },
      { position: 2, text: "Assert, act, arrange" },
    ],
    startedAt: "2026-01-15T09:00:00Z",
    deadline: "2026-01-15T09:00:20Z",
  };
}

function revealed(started: QuestionStarted, correctOption = 1): QuestionRevealed {
  return {
    masterclassId: MASTERCLASS,
    sessionId: started.sessionId,
    workshopTitle: started.workshopTitle,
    questionId: started.questionId,
    number: started.number,
    questionCount: started.questionCount,
    difficulty: started.difficulty,
    text: started.text,
    correctOption,
    options: started.options.map((option) => ({ ...option, answers: 1 })),
    answered: 2,
    timedOut: 0,
    revealedAt: "2026-01-15T09:00:15Z",
    lastQuestion: false,
  };
}

function leaderboard(sessionId: number, ended = false, points = 10): SessionLeaderboard {
  return {
    masterclassId: MASTERCLASS,
    sessionId,
    workshopTitle: "Unit tests",
    ended,
    entries: [{ rank: 1, participantId: ME, nickname: "Curious Otter", points, questionPoints: points }],
  };
}

function state(change: Partial<CurrentState> = {}): CurrentState {
  return {
    masterclassId: MASTERCLASS,
    masterclassStatus: "Open",
    step: "Waiting",
    question: null,
    count: null,
    revealed: null,
    ended: null,
    participant: null,
    leaderboard: null,
    ...change,
  };
}

const me = (answer: NonNullable<CurrentState["participant"]>["answer"]) => ({ participantId: ME, nickname: "Curious Otter", answer, standing: null });

const count = (started: QuestionStarted, answered: number) => ({
  masterclassId: MASTERCLASS,
  sessionId: started.sessionId,
  questionId: started.questionId,
  answered,
  participants: 5,
});

const stops: (() => void)[] = [];
afterEach(() => {
  for (const stop of stops.splice(0)) stop();
  vi.useRealTimers();
});

function phone(server: FakeServer) {
  const store = createLivePhone(MASTERCLASS, server.connect);
  stops.push(store.start());
  return store;
}

function projector(server: FakeServer, initialParticipants = 0) {
  const store = createLiveProjector(MASTERCLASS, initialParticipants, server.connect);
  stops.push(store.start());
  return store;
}

function serverWith(current: CurrentState | null = state()): FakeServer {
  const server = createFakeServer();
  server.handle("GetCurrentState", () => current);
  server.handle("WatchMasterclass", () => ({ participants: 0 }));
  return server;
}

describe("connecting", () => {
  it("restores where the Session stands from GetCurrentState", async () => {
    const q = question(1, 2);
    const server = serverWith(
      state({ step: "Question", question: q, count: count(q, 3), participant: me(null) }),
    );
    const store = phone(server);
    expect(store.getSnapshot()).toMatchObject({ session: { step: "waiting" }, connected: false });

    await settle();

    expect(store.getSnapshot()).toMatchObject({
      session: { step: "question", question: q, count: { answered: 3 } },
      connected: true,
      closed: false,
      participantId: ME,
      myAnswer: null,
      myResult: null,
    });
    expect(server.calls).toEqual([{ method: "GetCurrentState", args: [MASTERCLASS] }]);
  });

  it("gives the projector the Participant count from WatchMasterclass, before the current state", async () => {
    const server = serverWith();
    server.handle("WatchMasterclass", () => ({ participants: 12 }));
    const store = projector(server, 10);
    expect(store.getSnapshot().participants).toBe(10);

    await settle();

    expect(store.getSnapshot()).toMatchObject({ participants: 12, connected: true });
    expect(server.calls.map((call) => call.method)).toEqual(["WatchMasterclass", "GetCurrentState"]);
  });

  it("retries a first connect that failed", async () => {
    vi.useFakeTimers();
    const server = serverWith();
    server.failStarts(1);
    const store = phone(server);

    await vi.advanceTimersByTimeAsync(0);
    expect(store.getSnapshot().connected).toBe(false);

    await vi.advanceTimersByTimeAsync(5000);
    expect(store.getSnapshot().connected).toBe(true);
  });

  it("notifies subscribers and ignores everything once stopped", async () => {
    const server = serverWith();
    const store = createLivePhone(MASTERCLASS, server.connect);
    const listener = vi.fn();
    store.subscribe(listener);
    const stop = store.start();
    await settle();
    expect(listener).toHaveBeenCalled();

    stop();
    server.broadcast("QuestionStarted", question(1, 1));

    expect(store.getSnapshot()).toMatchObject({ session: { step: "waiting" }, connected: false });
  });
});

describe("broadcasts", () => {
  it("move the Session through Question, answer counts, reveal, leaderboard and end", async () => {
    const server = serverWith();
    const store = projector(server);
    await settle();
    const q = question(1, 1);

    server.broadcast("QuestionStarted", q);
    expect(store.getSnapshot().session).toEqual({ step: "question", question: q, count: null });

    server.broadcast("AnswerCountUpdated", count(q, 1));
    expect(store.getSnapshot().session).toMatchObject({ step: "question", count: { answered: 1 } });

    server.broadcast("QuestionRevealed", revealed(q));
    server.broadcast("LeaderboardUpdated", leaderboard(1));
    expect(store.getSnapshot()).toMatchObject({ session: { step: "revealed" }, leaderboard: leaderboard(1) });

    server.broadcast("SessionEnded", { masterclassId: MASTERCLASS, sessionId: 1, endedAt: "2026-01-15T09:05:00Z" });
    server.broadcast("LeaderboardUpdated", leaderboard(1, true));
    expect(store.getSnapshot()).toMatchObject({ session: { step: "ended" }, leaderboard: { ended: true } });
  });

  it("ignore other Masterclasses", async () => {
    const server = serverWith();
    const store = projector(server, 3);
    await settle();

    server.broadcast("QuestionStarted", { ...question(1, 1), masterclassId: OTHER_MASTERCLASS });
    server.broadcast("ParticipantCountUpdated", { masterclassId: OTHER_MASTERCLASS, participants: 9 });
    server.broadcast("MasterclassClosed", { masterclassId: OTHER_MASTERCLASS });

    expect(store.getSnapshot()).toMatchObject({ session: { step: "waiting" }, participants: 3, closed: false });
  });

  it("only ever raise the answer count and the Participant count", async () => {
    const server = serverWith();
    const store = projector(server, 4);
    await settle();
    const q = question(1, 1);
    server.broadcast("QuestionStarted", q);

    server.broadcast("AnswerCountUpdated", count(q, 3));
    server.broadcast("AnswerCountUpdated", count(q, 2));
    server.broadcast("AnswerCountUpdated", count(question(1, 2), 4));
    server.broadcast("ParticipantCountUpdated", { masterclassId: MASTERCLASS, participants: 6 });
    server.broadcast("ParticipantCountUpdated", { masterclassId: MASTERCLASS, participants: 5 });

    expect(store.getSnapshot()).toMatchObject({ session: { count: { answered: 3 } }, participants: 6 });
  });

  it("show a leaderboard only with its own Session", async () => {
    const server = serverWith();
    const store = projector(server);
    await settle();

    server.broadcast("LeaderboardUpdated", leaderboard(1, true));
    server.broadcast("QuestionRevealed", revealed(question(2, 1)));

    expect(store.getSnapshot().leaderboard).toBeNull();
  });

  it("MasterclassClosed closes the screen", async () => {
    const server = serverWith();
    const store = phone(server);
    await settle();

    server.broadcast("MasterclassClosed", { masterclassId: MASTERCLASS });

    expect(store.getSnapshot().closed).toBe(true);
  });
});

describe("a current state that arrives late", () => {
  it("does not move the Session backwards past a newer broadcast", async () => {
    const q = question(1, 1);
    let reply!: (current: CurrentState) => void;
    const server = createFakeServer();
    server.handle("GetCurrentState", () => new Promise<CurrentState>((resolve) => (reply = resolve)));
    const store = phone(server);
    await settle();

    server.broadcast("QuestionRevealed", revealed(q));
    reply(state({ step: "Question", question: q, count: count(q, 1) }));
    await settle();

    expect(store.getSnapshot().session).toMatchObject({ step: "revealed" });
  });

  it("keeps the higher answer count of the same Question", async () => {
    const q = question(1, 1);
    let reply!: (current: CurrentState) => void;
    const server = createFakeServer();
    server.handle("GetCurrentState", () => new Promise<CurrentState>((resolve) => (reply = resolve)));
    const store = phone(server);
    await settle();

    server.broadcast("QuestionStarted", q);
    server.broadcast("AnswerCountUpdated", count(q, 4));
    reply(state({ step: "Question", question: q, count: count(q, 2) }));
    await settle();

    expect(store.getSnapshot().session).toMatchObject({ step: "question", count: { answered: 4 } });
  });

  it("keeps a final leaderboard over the same Session's earlier one", async () => {
    const q = question(1, 3);
    let reply!: (current: CurrentState) => void;
    const server = createFakeServer();
    server.handle("GetCurrentState", () => new Promise<CurrentState>((resolve) => (reply = resolve)));
    const store = phone(server);
    await settle();

    server.broadcast("SessionEnded", { masterclassId: MASTERCLASS, sessionId: 1, endedAt: "2026-01-15T09:05:00Z" });
    server.broadcast("LeaderboardUpdated", leaderboard(1, true, 30));
    reply(state({ step: "Revealed", revealed: revealed(q), leaderboard: leaderboard(1, false, 20) }));
    await settle();

    expect(store.getSnapshot()).toMatchObject({ session: { step: "ended" }, leaderboard: { ended: true, entries: [{ points: 30 }] } });
  });
});

describe("reconnecting", () => {
  it("shows the drop, then resyncs and merges what was missed", async () => {
    const q = question(1, 1);
    let current = state({ step: "Question", question: q, count: count(q, 1) });
    const server = createFakeServer();
    server.handle("GetCurrentState", () => current);
    const store = phone(server);
    await settle();

    server.drop();
    expect(store.getSnapshot().connected).toBe(false);

    current = state({ step: "Revealed", revealed: revealed(q), leaderboard: leaderboard(1) });
    server.reconnect();
    await settle();

    expect(store.getSnapshot()).toMatchObject({ connected: true, session: { step: "revealed" }, leaderboard: leaderboard(1) });
    expect(server.calls.filter((call) => call.method === "GetCurrentState")).toHaveLength(2);
  });

  it("closes the screen when the Masterclass was closed while away", async () => {
    const server = serverWith(state({ masterclassStatus: "Closed" }));
    const store = projector(server);
    await settle();

    expect(store.getSnapshot().closed).toBe(true);
  });

  it("restores the phone's own Answer and its outcome from the server", async () => {
    const q = question(1, 1);
    let current = state({ step: "Question", question: q, participant: me({ questionId: q.questionId, canAnswer: true, option: 2, outcome: null }) });
    const server = createFakeServer();
    server.handle("GetCurrentState", () => current);
    const store = phone(server);
    await settle();

    expect(store.getSnapshot().myAnswer).toEqual({ option: 2, status: "locked", result: "Accepted" });

    server.drop();
    current = state({
      step: "Revealed",
      revealed: revealed(q, 2),
      participant: me({ questionId: q.questionId, canAnswer: true, option: 2, outcome: "Incorrect" }),
    });
    server.reconnect();
    await settle();

    // The server's outcome counts, even where the reveal alone would say otherwise.
    expect(store.getSnapshot()).toMatchObject({ myAnswer: { option: 2, status: "locked" }, myResult: "Incorrect" });
  });

  it("restores a Question the Participant joined too late to answer", async () => {
    const q = question(1, 1);
    const server = serverWith(
      state({
        step: "Question",
        question: q,
        participant: me({ questionId: q.questionId, canAnswer: false, option: null, outcome: null }),
      }),
    );
    const store = phone(server);
    await settle();

    expect(store.getSnapshot().myAnswer).toEqual({ option: null, status: "refused", result: "JoinedAfterStart" });

    server.broadcast("QuestionRevealed", revealed(q));
    expect(store.getSnapshot().myResult).toBe("JoinedAfterStart");
  });
});

describe("the phone's answer command", () => {
  it("submits the Answer and locks it in; the reveal says whether it was Correct", async () => {
    const q = question(1, 1);
    const server = serverWith(state({ step: "Question", question: q }));
    server.handle("SubmitAnswer", () => ({ result: "Accepted" }));
    const store = phone(server);
    await settle();

    const answering = store.answer(q.questionId, 1);
    expect(store.getSnapshot().myAnswer).toEqual({ option: 1, status: "sending", result: null });
    await answering;

    expect(server.calls.at(-1)).toEqual({ method: "SubmitAnswer", args: [q.questionId, 1] });
    expect(store.getSnapshot().myAnswer).toEqual({ option: 1, status: "locked", result: "Accepted" });

    server.broadcast("QuestionRevealed", revealed(q, 1));
    expect(store.getSnapshot().myResult).toBe("Correct");
  });

  it("shows a refusal, which counts as Timed out once revealed", async () => {
    const q = question(1, 1);
    const server = serverWith(state({ step: "Question", question: q }));
    server.handle("SubmitAnswer", () => ({ result: "TooLate" }));
    const store = phone(server);
    await settle();

    await store.answer(q.questionId, 2);
    expect(store.getSnapshot().myAnswer).toEqual({ option: 2, status: "refused", result: "TooLate" });

    server.broadcast("QuestionRevealed", revealed(q, 2));
    expect(store.getSnapshot().myResult).toBe("TimedOut");
  });

  it("says an earlier Answer counts when the server already had one", async () => {
    const q = question(1, 1);
    const server = serverWith(state({ step: "Question", question: q }));
    server.handle("SubmitAnswer", () => ({ result: "AlreadyAnswered" }));
    const store = phone(server);
    await settle();

    await store.answer(q.questionId, 2);
    expect(store.getSnapshot().myAnswer).toMatchObject({ status: "locked", result: "AlreadyAnswered" });

    server.broadcast("QuestionRevealed", revealed(q, 2));
    expect(store.getSnapshot().myResult).toBe("AlreadyAnswered");
  });

  it("forgets an Answer that could not be sent, so the Participant can tap again", async () => {
    const q = question(1, 1);
    const server = serverWith(state({ step: "Question", question: q }));
    server.handle("SubmitAnswer", () => ({ result: "Accepted" }));
    const store = phone(server);
    await settle();

    server.drop();
    await store.answer(q.questionId, 1);

    expect(store.getSnapshot().myAnswer).toBeNull();
  });
});

describe("the projector's commands", () => {
  it("start a Session through the hub and pass on its problems", async () => {
    const server = serverWith();
    server.handle("StartSession", () => ({ problems: ["A Session is already running."] }));
    const store = projector(server);
    await settle();

    const mix = { easy: 2, medium: 2, hard: 1 };
    expect(await store.startSession(3, mix)).toEqual(["A Session is already running."]);
    expect(server.calls.at(-1)).toEqual({ method: "StartSession", args: [MASTERCLASS, 3, mix] });
  });

  it("refuse to start a Session while not connected, without calling the hub", async () => {
    const server = serverWith();
    const store = projector(server);
    await settle();
    server.drop();

    const problems = await store.startSession(3, { easy: 1, medium: 0, hard: 0 });

    expect(problems).toEqual(["Not connected to live updates yet. Try again in a moment."]);
    expect(server.calls.some((call) => call.method === "StartSession")).toBe(false);
  });

  it("resume the Session and show where it stands", async () => {
    const q = question(1, 2);
    const server = serverWith();
    server.handle("ResumeSession", () => state({ step: "Revealed", revealed: revealed(q) }));
    const store = projector(server);
    await settle();

    await store.resume();

    expect(server.calls.at(-1)).toEqual({ method: "ResumeSession", args: [MASTERCLASS] });
    expect(store.getSnapshot().session).toMatchObject({ step: "revealed", revealed: { questionId: q.questionId } });
  });

  it("fail to resume while disconnected", async () => {
    const server = serverWith();
    server.handle("ResumeSession", () => state());
    const store = projector(server);
    await settle();
    server.drop();

    await expect(store.resume()).rejects.toThrow();
  });
});
