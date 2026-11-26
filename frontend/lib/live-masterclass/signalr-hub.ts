import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import type { Hub } from "./hub";

// The real Hub: the backend's SignalR hub (QuizHub). It is reached on the site's own origin, so the
// browser's cookies go along: Caddy routes /hubs/* to the backend in production, and next.config.ts
// rewrites it to BACKEND_URL everywhere else.
const QUIZ_HUB_PATH = "/hubs/quiz";

/** A new, not yet started connection that reconnects by itself. */
export function connectToQuizHub(): Hub {
  const connection = new HubConnectionBuilder()
    .withUrl(QUIZ_HUB_PATH, { withCredentials: true })
    .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (retry) => Math.min(1000 * 2 ** retry.previousRetryCount, 10000) })
    .configureLogging(LogLevel.Warning)
    .build();

  return {
    on: (event, handler) => connection.on(event, handler),
    invoke: (method, ...args) => connection.invoke(method, ...args),
    start: () => connection.start(),
    stop: () => connection.stop(),
    onReconnecting: (callback) => connection.onreconnecting(() => callback()),
    onReconnected: (callback) => connection.onreconnected(() => callback()),
    onClose: (callback) => connection.onclose(() => callback()),
  };
}
