import type { DifficultyCounts, MasterclassStatus } from "@/lib/domain";
import type {
  AnswerCount,
  AnswerOutcome,
  AnswerResult,
  LeaderboardEntry,
  QuestionRevealed,
  QuestionStarted,
  SessionEnded,
  SessionLeaderboard,
} from "./types";

// The wire contract with the backend's Quiz hub (backend Live/QuizHub.cs, QuizHub.Recovery.cs,
// MasterclassNotifier.cs; Quiz/CurrentState.cs), and the seam the module's two adapters plug into:
// signalr-hub.ts (the real one) and fake-hub.ts (in memory, for tests). Nothing here leaves the module.

/** Server → clients broadcasts to a Masterclass, by name. Every message names its Masterclass. */
export type HubEvents = {
  /** Someone joined. */
  ParticipantCountUpdated: { masterclassId: number; participants: number };
  QuestionStarted: QuestionStarted;
  /** After each accepted Answer. */
  AnswerCountUpdated: AnswerCount;
  QuestionRevealed: QuestionRevealed;
  SessionEnded: SessionEnded;
  /** After each reveal, and once more (final) when the Session ends. */
  LeaderboardUpdated: SessionLeaderboard;
  /** The Host closed the Masterclass. */
  MasterclassClosed: { masterclassId: number };
};

/**
 * The asking Participant's Answer to the Question shown: `canAnswer` false when they joined after it
 * started; `option` they chose (null: not answered or Timed out); `outcome` only once revealed.
 */
export type OwnAnswer = { questionId: number; canAnswer: boolean; option: number | null; outcome: AnswerOutcome | null };

/**
 * GetCurrentState's reply: what to show now. Only the part named by `step` is set; `leaderboard` with
 * Revealed and Ended; `participant` only for a Participant's phone. `masterclassStatus` is "Closed" once
 * the Host closed the Masterclass, for a screen that missed the MasterclassClosed broadcast.
 */
export type CurrentState = {
  masterclassId: number;
  masterclassStatus: MasterclassStatus;
  step: "Waiting" | "Question" | "Revealed" | "Ended";
  question: QuestionStarted | null;
  count: AnswerCount | null;
  revealed: QuestionRevealed | null;
  ended: SessionEnded | null;
  participant: { participantId: number; nickname: string; answer: OwnAnswer | null; standing: LeaderboardEntry | null } | null;
  leaderboard: SessionLeaderboard | null;
};

/** Client → server calls, by name: their arguments and reply. */
export type HubMethods = {
  /** Host only: follow the Masterclass's broadcasts; the Masterclass now (null: none). */
  WatchMasterclass: { args: [masterclassId: number]; reply: { participants: number } | null };
  /** Everyone, on each connect and reconnect (SignalR doesn't replay missed messages): follow and say what to show now. */
  GetCurrentState: { args: [masterclassId: number]; reply: CurrentState | null };
  /** Host only: the backend timer follows the running Session again. */
  ResumeSession: { args: [masterclassId: number]; reply: CurrentState | null };
  /** Host only. */
  StartSession: { args: [masterclassId: number, workshopId: number, mix: DifficultyCounts]; reply: { problems: string[] } };
  /** Participant only (known by cookie). */
  SubmitAnswer: { args: [questionId: number, option: number]; reply: { result: AnswerResult } };
};

export type HubEvent = keyof HubEvents;
export type HubMethod = keyof HubMethods;

/** One not yet started connection to the Quiz hub, which reconnects by itself after it first started. */
export interface Hub {
  on<E extends HubEvent>(event: E, handler: (message: HubEvents[E]) => void): void;
  invoke<M extends HubMethod>(method: M, ...args: HubMethods[M]["args"]): Promise<HubMethods[M]["reply"]>;
  /** Rejects if the connection could not be made (automatic reconnects only cover later drops). */
  start(): Promise<void>;
  stop(): Promise<void>;
  /** The connection dropped; it is trying to reconnect. */
  onReconnecting(callback: () => void): void;
  /** Reconnected: broadcasts missed meanwhile are lost. */
  onReconnected(callback: () => void): void;
  /** Closed for good (stopped, or reconnecting gave up). */
  onClose(callback: () => void): void;
}

/** Makes a new Hub; called once per start of a live screen. */
export type ConnectToHub = () => Hub;
