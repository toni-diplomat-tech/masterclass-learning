import Link from "next/link";
import { buttonClass } from "@/components/button";
import styles from "../participant.module.scss";

/** The Participant's link to their Recap, shown once a Session has ended; it keeps working after the Masterclass closes. */
export function RecapLink({ recapToken }: { recapToken: string | null }) {
  if (!recapToken) return null;
  return (
    <p data-testid="recap-link" className={styles.recap}>
      <Link href={`/recap/${recapToken}`} className={buttonClass({ size: "lg", block: true })}>
        Your Recap: the Lessons to revisit
      </Link>
      <small>Keep this link: it keeps working after the Masterclass is closed.</small>
    </p>
  );
}
