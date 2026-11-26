import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { Suspense } from "react";
import { currentParticipant } from "@/lib/participant";
import { currentRecapToken } from "@/lib/recaps";
import { LiveParticipant } from "./live-participant";
import { RecapLink } from "./recap-link";

export const metadata: Metadata = { title: "Play · Masterclass Quiz" };

export default function PlayPage() {
  return (
    <main style={{ padding: "2rem", textAlign: "center" }}>
      <Suspense fallback={<p>Loading…</p>}>
        <ParticipantScreen />
      </Suspense>
    </main>
  );
}

// The phone follows the server (LiveParticipant): waiting screen, then each Question as it starts.
async function ParticipantScreen() {
  const participant = await currentParticipant();
  if (!participant) redirect("/");
  const recapToken = await currentRecapToken();

  return (
    <>
      <p>{participant.masterclassName}</p>
      <p>You are</p>
      <h1 data-testid="nickname" style={{ fontSize: "2.5rem" }}>
        {participant.nickname}
      </h1>
      {participant.masterclassStatus === "Closed" ? (
        <>
          <p>This Masterclass has ended.</p>
          <RecapLink recapToken={recapToken} />
        </>
      ) : (
        <LiveParticipant masterclassId={participant.masterclassId} recapToken={recapToken} />
      )}
    </>
  );
}
