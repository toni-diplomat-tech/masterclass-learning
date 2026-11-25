import Link from "next/link";

/** The Participant's link to their Recap, shown once a Session has ended; it keeps working after the Masterclass closes. */
export function RecapLink({ recapToken }: { recapToken: string | null }) {
  if (!recapToken) return null;
  return (
    <p data-testid="recap-link">
      <Link href={`/recap/${recapToken}`}>Your Recap: the Lessons to revisit</Link>
      <br />
      <small>Keep this link: it keeps working after the Masterclass is closed.</small>
    </p>
  );
}
