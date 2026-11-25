import { HubConnectionBuilder, LogLevel, type HubConnection } from "@microsoft/signalr";

// Browser-side: the backend's SignalR hub (QuizHub). It is reached on the site's own origin, so the
// browser's cookies go along: Caddy routes /hubs/* to the backend in production, and next.config.ts
// rewrites it to BACKEND_URL everywhere else.
export const QUIZ_HUB_PATH = "/hubs/quiz";

/** A new, not yet started connection that reconnects by itself (callers re-sync in onreconnected). */
export function connectToQuizHub(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(QUIZ_HUB_PATH, { withCredentials: true })
    .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (retry) => Math.min(1000 * 2 ** retry.previousRetryCount, 10000) })
    .configureLogging(LogLevel.Warning)
    .build();
}
