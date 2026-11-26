import { Card } from "@/components/card";
import type { MasterclassPlace } from "@/lib/participant";
import styles from "../participant.module.scss";

/** The Participant's place on the Masterclass leaderboard, on the phone's Closed screen; nothing when they are not on it. */
export function MyMasterclassPlace({ place }: { place: MasterclassPlace | null }) {
  if (!place) return null;
  return (
    <Card data-testid="my-masterclass-place" className={styles.score}>
      <p className={styles.points}>
        {place.points} {place.points === 1 ? "point" : "points"}
      </p>
      <p data-testid="my-masterclass-rank" className={styles.label}>
        Place {place.rank} of {place.of} in this Masterclass
      </p>
    </Card>
  );
}
