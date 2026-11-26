import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { Suspense } from "react";
import { Eyebrow, Page, Stack } from "@/components/page";
import { currentMasterclassPlace, currentParticipant } from "@/lib/participant";
import { inMasterclassOpen } from "@/lib/participant-cookie";
import { currentRecapToken } from "@/lib/recaps";
import styles from "../participant.module.scss";
import { LiveParticipant } from "./live-participant";
import { MyMasterclassPlace } from "./my-masterclass-place";
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

// The phone follows the server (LiveParticipant): waiting screen, then each Question as it starts. Once
// the Masterclass is Closed (the Host closed it, or its last Workshop's Session ended), the Closed screen:
// the Participant's place on the Masterclass leaderboard, then their Recap link.
async function ParticipantScreen() {
  const participant = await currentParticipant();
  if (!participant) redirect("/");
  const open = inMasterclassOpen(participant);
  const [recapToken, place] = await Promise.all([currentRecapToken(), open ? null : currentMasterclassPlace()]);

  return (
    <Stack gap="lg">
      <div className={styles.identity}>
        <Eyebrow>{participant.masterclassName}</Eyebrow>
        <p className={styles.label}>You are</p>
        <h1 data-testid="nickname" className={styles.nickname}>
          {participant.nickname}
        </h1>
      </div>
      {open ? (
        <LiveParticipant masterclassId={participant.masterclassId} recapToken={recapToken} />
      ) : (
        <Stack>
          <p data-testid="masterclass-closed" className={styles.waiting}>
            This Masterclass has ended.
          </p>
          <MyMasterclassPlace place={place} />
          <RecapLink recapToken={recapToken} />
        </Stack>
      )}
    </Stack>
  );
}
