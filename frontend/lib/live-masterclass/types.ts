import type { Difficulty, DifficultyCounts } from "@/lib/domain";

// What the live screens render: a Masterclass's Session as the server last said, plus each role's view
// and commands. Times are UTC ISO strings.

/** A Question as it starts. Never says which option is correct. */
export type QuestionStarted = {
  masterclassId: number;
  sessionId: number;
  workshopTitle: string;
  questionId: number;
  number: number;
  questionCount: number;
  difficulty: Difficulty;
  text: string;
  options: { position: number; text: string }[];
  startedAt: string;
  deadline: string;
};

/** How many of those who can answer the running Question have. */
export type AnswerCount = {
  masterclassId: number;
  sessionId: number;
  questionId: number;
  answered: number;
  participants: number;
};

/** The running Question, revealed: the correct option and the counts. */
export type QuestionRevealed = {
  masterclassId: number;
  sessionId: number;
  workshopTitle: string;
  questionId: number;
  number: number;
  questionCount: number;
  difficulty: Difficulty;
  text: string;
  correctOption: number;
  options: { position: number; text: string; answers: number }[];
  answered: number;
  timedOut: number;
  revealedAt: string;
  /** No Question follows: the Session ends after the reveal pause. */
  lastQuestion: boolean;
};

/** The Session is over (after the pause following its last reveal). */
export type SessionEnded = { masterclassId: number; sessionId: number; endedAt: string };

/** One Participant on a leaderboard. Equal points share a rank (1, 1, 3). */
export type LeaderboardEntry = {
  rank: number;
  participantId: number;
  nickname: string;
  /** The Session's points so far (revealed Questions only). */
  points: number;
  /** Points scored on the latest revealed Question. */
  questionPoints: number;
};

/** A Session's leaderboard after each reveal, and final (`ended`) once the Session ends. Highest points first. */
export type SessionLeaderboard = {
  masterclassId: number;
  sessionId: number;
  workshopTitle: string;
  ended: boolean;
  entries: LeaderboardEntry[];
};

/** Where the live Session stands, as the server last said. */
export type LiveSession =
  | { step: "waiting" }
  | { step: "question"; question: QuestionStarted; count: AnswerCount | null }
  | { step: "revealed"; revealed: QuestionRevealed }
  | { step: "ended"; ended: SessionEnded };

/** What the server made of an Answer; only "Accepted" records it. */
export type AnswerResult =
  | "Accepted"
  | "TooLate"
  | "AlreadyAnswered"
  | "JoinedAfterStart"
  | "UnknownOption"
  | "NoSuchQuestion"
  | "UnknownParticipant";

/** An Answer's outcome once its Question is revealed. */
export type AnswerOutcome = "Correct" | "Incorrect" | "TimedOut";

/**
 * This phone's Answer to the Question shown: being sent, locked in by the server, or refused (`result`
 * says why). `option` is the one tapped; null when there was none (joined after the Question started).
 */
export type MyAnswer = { option: number | null; status: "sending" | "locked" | "refused"; result: AnswerResult | null };

/**
 * This phone's result on the revealed Question: its Answer's outcome, or why it has none of its own
 * (joined during the Question; or the tap was refused because an earlier Answer counts).
 */
export type MyResult = AnswerOutcome | "JoinedAfterStart" | "AlreadyAnswered";

/** What both screens show. `leaderboard` is the shown Session's (null while none, or while a Question runs). */
export type LiveView = {
  session: LiveSession;
  leaderboard: SessionLeaderboard | null;
  /** Following live updates now (false while connecting or reconnecting). */
  connected: boolean;
  /** The Host closed the Masterclass: the page should render again as closed. */
  closed: boolean;
};

/** The projector (Host): plus the number of Participants who joined. */
export type ProjectorView = LiveView & { participants: number };

/** A Participant's phone: plus who they are and their Answer to the Question shown. */
export type PhoneView = LiveView & {
  participantId: number | null;
  /** This phone's Answer to the shown Question (running or revealed); null without one. */
  myAnswer: MyAnswer | null;
  /** Set only while a Question is revealed. */
  myResult: MyResult | null;
};

export type ProjectorCommands = {
  /** Starts a Workshop's Session. Resolves to every reason it was refused (empty: it started). */
  startSession: (workshopId: number, mix: DifficultyCounts) => Promise<string[]>;
  /** The backend timer follows the running Session again (restart recovery). Rejects if it failed. */
  resume: () => Promise<void>;
};

export type PhoneCommands = {
  /** Answers a running Question with the option at `option`; `myAnswer` follows its progress. */
  answer: (questionId: number, option: number) => Promise<void>;
};
