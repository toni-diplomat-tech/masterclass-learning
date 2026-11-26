import type { DifficultyCounts } from "@/lib/domain";
import type { ConnectToHub, CurrentState, Hub, HubEvent, HubEvents } from "./hub";
import {
  newerLeaderboard,
  newerSession,
  restoredAnswer,
  resultOf,
  shownQuestionId,
  shownSessionId,
  withCount,
  type Answer,
} from "./merge";
import type { LiveSession, LiveView, PhoneCommands, PhoneView, ProjectorCommands, ProjectorView, SessionLeaderboard } from "./types";

// The framework-free core: one live Masterclass screen's state, kept up to date over a Hub.

/** A live screen's store: plain state to render (subscribe/getSnapshot) and the role's commands. */
export type LiveStore<View, Commands> = Commands & {
  subscribe: (listener: () => void) => () => void;
  getSnapshot: () => View;
  /**
   * Connects and follows the Masterclass until the returned function is called, retrying the first
   * connect and resyncing after every reconnect. Can be called again after stopping (a new connection).
   */
  start: () => () => void;
};

export type ProjectorStore = LiveStore<ProjectorView, ProjectorCommands>;
export type PhoneStore = LiveStore<PhoneView, PhoneCommands>;

export const NOT_CONNECTED = "Not connected to live updates yet. Try again in a moment.";

/** How long to wait before retrying a first connect that failed. */
const RETRY_MS = 5000;

type Role = "projector" | "phone";

type State = {
  session: LiveSession;
  leaderboard: SessionLeaderboard | null;
  connected: boolean;
  closed: boolean;
  participants: number;
  participantId: number | null;
  answer: Answer | null;
};

/** The projector (Host) of an Open Masterclass, starting from the Participant count the page had. */
export function createLiveProjector(masterclassId: number, initialParticipants: number, connect: ConnectToHub): ProjectorStore {
  const core = createCore("projector", masterclassId, initialParticipants, connect, (state) => ({
    ...viewOf(state),
    participants: state.participants,
  }));
  return { subscribe: core.subscribe, getSnapshot: core.getSnapshot, start: core.start, startSession: core.startSession, resume: core.resume };
}

/** A Participant's phone (the hub knows them by their cookie). */
export function createLivePhone(masterclassId: number, connect: ConnectToHub): PhoneStore {
  const core = createCore("phone", masterclassId, 0, connect, (state) => {
    const questionId = shownQuestionId(state.session);
    const answer = questionId !== null && state.answer?.questionId === questionId ? state.answer : null;
    return {
      ...viewOf(state),
      participantId: state.participantId,
      myAnswer: answer && { option: answer.option, status: answer.status, result: answer.result },
      myResult: state.session.step === "revealed" ? resultOf(state.session.revealed, answer) : null,
    };
  });
  return { subscribe: core.subscribe, getSnapshot: core.getSnapshot, start: core.start, answer: core.answer };
}

function viewOf(state: State): LiveView {
  const sessionId = shownSessionId(state.session);
  return {
    session: state.session,
    // Only the shown Session's: one from an earlier Session is stale.
    leaderboard: state.leaderboard !== null && state.leaderboard.sessionId === sessionId ? state.leaderboard : null,
    connected: state.connected,
    closed: state.closed,
  };
}

function createCore<View>(
  role: Role,
  masterclassId: number,
  initialParticipants: number,
  connect: ConnectToHub,
  view: (state: State) => View,
) {
  let state: State = {
    session: { step: "waiting" },
    leaderboard: null,
    connected: false,
    closed: false,
    participants: initialParticipants,
    participantId: null,
    answer: null,
  };
  let snapshot = view(state);
  const listeners = new Set<() => void>();
  /** The connection of the current start; null while stopped. */
  let hub: Hub | null = null;

  const update = (change: (state: State) => Partial<State>) => {
    const patch = change(state);
    const keys = Object.keys(patch) as (keyof State)[];
    if (keys.every((key) => patch[key] === state[key])) return;
    state = { ...state, ...patch };
    snapshot = view(state);
    for (const listener of listeners) listener();
  };

  // Where the screen stands after a GetCurrentState reply (on every connect and reconnect).
  const merge = (current: CurrentState) =>
    update((shown) => {
      const own = current.participant?.answer;
      return {
        // Closed while this screen was away (it missed the MasterclassClosed broadcast).
        closed: shown.closed || current.masterclassStatus === "Closed",
        leaderboard: newerLeaderboard(shown.leaderboard, current.leaderboard),
        session: newerSession(shown.session, current),
        ...(role === "phone" && {
          participantId: current.participant?.participantId ?? null,
          answer: own ? restoredAnswer(shown.answer, own) : shown.answer,
        }),
      };
    });

  const start = () => {
    const connection = connect();
    hub = connection;
    let stopped = false;
    let retry: ReturnType<typeof setTimeout> | undefined;
    // Ignores whatever a stopped connection still reports.
    const current = () => !stopped && hub === connection;

    const on = <E extends HubEvent>(event: E, handler: (message: HubEvents[E]) => void) =>
      connection.on(event, (message) => {
        if (current() && message.masterclassId === masterclassId) handler(message);
      });

    // Handlers go on before the connection starts, so no broadcast slips past between start and follow.
    on("QuestionStarted", (question) => update(() => ({ session: { step: "question", question, count: null } })));
    on("AnswerCountUpdated", (count) => update((shown) => ({ session: withCount(shown.session, count) })));
    on("QuestionRevealed", (revealed) => update(() => ({ session: { step: "revealed", revealed } })));
    on("SessionEnded", (ended) => update(() => ({ session: { step: "ended", ended } })));
    on("LeaderboardUpdated", (leaderboard) => update(() => ({ leaderboard })));
    on("MasterclassClosed", () => update(() => ({ closed: true })));
    if (role === "projector") {
      // The count only grows; an update that overtook a later one must not lower it.
      on("ParticipantCountUpdated", ({ participants }) =>
        update((shown) => ({ participants: Math.max(shown.participants, participants) })),
      );
    }

    // Follows the Masterclass and catches up on what was missed: SignalR doesn't replay broadcasts.
    const follow = async () => {
      if (role === "projector") {
        const watched = await connection.invoke("WatchMasterclass", masterclassId);
        if (watched && current()) update((shown) => ({ participants: Math.max(shown.participants, watched.participants) }));
      }
      const reply = await connection.invoke("GetCurrentState", masterclassId);
      if (reply && current()) merge(reply);
    };
    const resync = () =>
      follow()
        .then(() => {
          if (current()) update(() => ({ connected: true }));
        })
        .catch(() => undefined);
    const disconnected = () => {
      if (current()) update(() => ({ connected: false }));
    };

    connection.onReconnecting(disconnected);
    connection.onReconnected(() => void resync());
    connection.onClose(disconnected);

    // Automatic reconnects don't cover the first start, so it is retried here.
    const connectNow = (): Promise<void> =>
      connection
        .start()
        .then(() => (current() ? resync() : undefined))
        .catch(() => {
          disconnected();
          if (current()) retry = setTimeout(() => void connectNow(), RETRY_MS);
        });
    void connectNow();

    return () => {
      stopped = true;
      clearTimeout(retry);
      if (hub === connection) {
        hub = null;
        update(() => ({ connected: false }));
      }
      void connection.stop();
    };
  };

  const startSession = async (workshopId: number, mix: DifficultyCounts): Promise<string[]> => {
    if (!hub || !state.connected) return [NOT_CONNECTED];
    const reply = await hub.invoke("StartSession", masterclassId, workshopId, mix);
    return reply.problems;
  };

  const resume = async (): Promise<void> => {
    if (!hub) throw new Error("Not connected");
    const reply = await hub.invoke("ResumeSession", masterclassId);
    if (reply) update((shown) => ({ session: newerSession(shown.session, reply) }));
  };

  const answer = async (questionId: number, option: number): Promise<void> => {
    if (!hub) return;
    const base = { questionId, option, result: null, outcome: null };
    update(() => ({ answer: { ...base, status: "sending" } }));
    try {
      const { result } = await hub.invoke("SubmitAnswer", questionId, option);
      const locked = result === "Accepted" || result === "AlreadyAnswered";
      update(() => ({ answer: { ...base, status: locked ? "locked" : "refused", result } }));
    } catch {
      // Not delivered (e.g. reconnecting): let them tap again.
      update(() => ({ answer: null }));
    }
  };

  return {
    subscribe: (listener: () => void) => {
      listeners.add(listener);
      return () => void listeners.delete(listener);
    },
    getSnapshot: () => snapshot,
    start,
    startSession,
    resume,
    answer,
  };
}
