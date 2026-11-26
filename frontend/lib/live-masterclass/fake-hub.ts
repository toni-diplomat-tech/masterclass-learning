import type { ConnectToHub, Hub, HubEvent, HubEvents, HubMethod, HubMethods } from "./hub";

// The in-memory Hub, for tests: plays the server. Tests script its replies, send broadcasts, drop and
// restore the connection, and read which calls arrived.

type Handlers = { [M in HubMethod]?: (...args: HubMethods[M]["args"]) => HubMethods[M]["reply"] | Promise<HubMethods[M]["reply"]> };

export type Call = { method: HubMethod; args: unknown[] };

export type FakeServer = {
  /** Pass to a store: each start opens a new fake connection. */
  connect: ConnectToHub;
  /** Scripts the reply to a hub method; unscripted methods reject. */
  handle<M extends HubMethod>(method: M, handler: NonNullable<Handlers[M]>): void;
  /** Sends a broadcast to the connected client. */
  broadcast<E extends HubEvent>(event: E, message: HubEvents[E]): void;
  /** The connection drops; automatic reconnect is under way. */
  drop(): void;
  /** The dropped connection is back. */
  reconnect(): void;
  /** The next `times` starts fail. */
  failStarts(times: number): void;
  /** Every call made on any connection so far, in order. */
  calls: Call[];
  /** Whether the latest connection is connected. */
  readonly connected: boolean;
};

export function createFakeServer(): FakeServer {
  const handlers: Handlers = {};
  const calls: Call[] = [];
  let failingStarts = 0;
  let latest: FakeConnection | null = null;

  type FakeConnection = Hub & {
    state: "new" | "connected" | "reconnecting" | "stopped";
    listeners: Map<HubEvent, ((message: never) => void)[]>;
    reconnecting: (() => void)[];
    reconnected: (() => void)[];
    closed: (() => void)[];
  };

  const connect = (): Hub => {
    const connection: FakeConnection = {
      state: "new",
      listeners: new Map(),
      reconnecting: [],
      reconnected: [],
      closed: [],
      on(event, handler) {
        connection.listeners.set(event, [...(connection.listeners.get(event) ?? []), handler]);
      },
      async invoke(method, ...args) {
        calls.push({ method, args });
        if (connection.state !== "connected") throw new Error("Cannot send data if the connection is not in the 'Connected' State.");
        const handler = handlers[method] as ((...args: unknown[]) => unknown) | undefined;
        if (!handler) throw new Error(`No reply scripted for ${method}`);
        return (await handler(...args)) as HubMethods[typeof method]["reply"];
      },
      async start() {
        await Promise.resolve();
        if (failingStarts > 0) {
          failingStarts--;
          throw new Error("Failed to start the connection");
        }
        if (connection.state === "stopped") throw new Error("Stopped");
        connection.state = "connected";
      },
      async stop() {
        if (connection.state === "stopped") return;
        connection.state = "stopped";
        for (const callback of connection.closed) callback();
      },
      onReconnecting: (callback) => void connection.reconnecting.push(callback),
      onReconnected: (callback) => void connection.reconnected.push(callback),
      onClose: (callback) => void connection.closed.push(callback),
    };
    latest = connection;
    return connection;
  };

  const live = () => {
    if (!latest) throw new Error("No connection was made");
    return latest;
  };

  return {
    connect,
    handle(method, handler) {
      (handlers as Record<HubMethod, unknown>)[method] = handler;
    },
    broadcast(event, message) {
      const connection = live();
      if (connection.state !== "connected") return;
      for (const listener of connection.listeners.get(event) ?? []) (listener as (message: HubEvents[typeof event]) => void)(message);
    },
    drop() {
      const connection = live();
      connection.state = "reconnecting";
      for (const callback of connection.reconnecting) callback();
    },
    reconnect() {
      const connection = live();
      connection.state = "connected";
      for (const callback of connection.reconnected) callback();
    },
    failStarts(times) {
      failingStarts = times;
    },
    calls,
    get connected() {
      return latest?.state === "connected";
    },
  };
}
