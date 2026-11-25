"use client";

import { useEffect, useState } from "react";
import { connectToQuizHub } from "@/lib/quiz-hub";

type MasterclassSnapshot = { participants: number } | null;
type ParticipantCountUpdate = { masterclassId: number; participants: number };

/**
 * The live number of Participants. Watches the Masterclass over SignalR, and asks again for the
 * current count on every (re)connect, since SignalR doesn't replay messages missed while away.
 */
export function ParticipantCount({ masterclassId, initial }: { masterclassId: number; initial: number }) {
  const [count, setCount] = useState(initial);
  const [connected, setConnected] = useState(false);

  useEffect(() => {
    const connection = connectToQuizHub();
    // The count only grows; an update that overtook a later one must not lower it.
    const show = (participants: number) => setCount((shown) => Math.max(shown, participants));

    const watch = async () => {
      const snapshot = await connection.invoke<MasterclassSnapshot>("WatchMasterclass", masterclassId);
      if (snapshot) show(snapshot.participants);
      setConnected(true);
    };

    connection.on("ParticipantCountUpdated", (update: ParticipantCountUpdate) => {
      if (update.masterclassId === masterclassId) show(update.participants);
    });
    connection.onreconnecting(() => setConnected(false));
    connection.onreconnected(() => void watch().catch(() => undefined));
    connection.onclose(() => setConnected(false));

    // Automatic reconnects only cover a connection that once started, so retry the first start too.
    let stopped = false;
    let retry: ReturnType<typeof setTimeout> | undefined;
    const start = () =>
      connection
        .start()
        .then(() => (stopped ? undefined : watch()))
        .catch(() => {
          setConnected(false);
          if (!stopped) retry = setTimeout(start, 5000);
        });
    void start();

    return () => {
      stopped = true;
      clearTimeout(retry);
      void connection.stop();
    };
  }, [masterclassId]);

  return (
    <p style={{ fontSize: "2rem" }}>
      <span data-testid="participant-count">{count}</span> {count === 1 ? "Participant" : "Participants"} joined
      {!connected && <small style={{ display: "block", fontSize: "1rem" }}>Reconnecting to live updates…</small>}
    </p>
  );
}
