import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { Suspense } from "react";
import { Eyebrow, Page, Stack } from "@/components/page";
import { currentParticipant } from "@/lib/participant";
import { currentRecapToken } from "@/lib/recaps";
import styles from "../participant.module.scss";
import { LiveParticipant } from "./live-participant";
import { RecapLink } from "./recap-link";

export const metadata: Metadata = { title: "Play · Masterclass Quiz" };

export default function PlayPage() {
  return (
    <Page width="phone">
      <Suspense fallback={<p className={styles.waiting}>Loading…</p>}>
        <ParticipantScreen />
      </Suspense>
    </Page>
  );
}

// The phone follows the server (LiveParticipant): waiting screen, then each Question as it starts.
async function ParticipantScreen() {
  const participant = await currentParticipant();
  if (!participant) redirect("/");
  const recapToken = await currentRecapToken();

  return (
    <Stack gap="lg">
      <div className={styles.identity}>
        <Eyebrow>{participant.masterclassName}</Eyebrow>
        <p className={styles.label}>You are</p>
        <h1 data-testid="nickname" className={styles.nickname}>
          {participant.nickname}
        </h1>
      </div>
      {participant.masterclassStatus === "Closed" ? (
        <Stack>
          <p className={styles.waiting}>This Masterclass has ended.</p>
          <RecapLink recapToken={recapToken} />
        </Stack>
      ) : (
        <LiveParticipant masterclassId={participant.masterclassId} recapToken={recapToken} />
      )}
    </Stack>
  );
}
