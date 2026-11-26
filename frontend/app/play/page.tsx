import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { Suspense } from "react";
import { Eyebrow, Page, Stack } from "@/components/page";
import { currentMasterclassRank, currentParticipant } from "@/lib/participant";
import { inMasterclassOpen } from "@/lib/participant-cookie";
import { currentRecapToken } from "@/lib/recaps";
import styles from "../participant.module.scss";
import { LiveParticipant } from "./live-participant";
import { RankCard } from "./rank-card";
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
// the Masterclass is Masterclass Closed (the Host closed it, or its last Workshop's Session ended), the
// Masterclass Closed screen: the Participant's rank on the Masterclass leaderboard, then their Recap link.
async function ParticipantScreen() {
  const participant = await currentParticipant();
  if (!participant) redirect("/");
  const open = inMasterclassOpen(participant);
  const [recapToken, rank] = await Promise.all([currentRecapToken(), open ? null : currentMasterclassRank()]);

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
            This Masterclass is closed.
          </p>
          {rank && (
            <RankCard
              testId="my-masterclass-score"
              points={rank.points}
              rank={rank.rank}
              of={rank.of}
              where="in this Masterclass"
            />
          )}
          <RecapLink recapToken={recapToken} />
        </Stack>
      )}
    </Stack>
  );
}
