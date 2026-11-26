// The live Masterclass, browser-side: given a Masterclass and a role (the Host's projector or a
// Participant's phone), it owns the connection to the Quiz hub, every subscription, the resync after each
// (re)connect, merging what arrives out of order, close detection and the wire → domain mapping. Screens
// get plain state and the role's commands; they never see the hub, its method or event names, or wire types.
//
// This file is the module's interface; everything else in the folder is private to it.

export { useLivePhone, useLiveProjector } from "./use-live-masterclass";
export { createLivePhone, createLiveProjector, type LiveStore, type PhoneStore, type ProjectorStore } from "./store";
export type { ConnectToHub } from "./hub";
export type {
  AnswerCount,
  AnswerOutcome,
  AnswerResult,
  LeaderboardEntry,
  LiveSession,
  LiveView,
  MyAnswer,
  MyResult,
  PhoneCommands,
  PhoneView,
  ProjectorCommands,
  ProjectorView,
  QuestionRevealed,
  QuestionStarted,
  SessionEnded,
  SessionLeaderboard,
} from "./types";
