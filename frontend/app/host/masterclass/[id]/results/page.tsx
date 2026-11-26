import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { Suspense } from "react";
import styles from "@/app/host/host.module.scss";
import { Card } from "@/components/card";
import { Eyebrow, Page, Stack } from "@/components/page";
import { requireHost } from "@/lib/host-session";
import { getMasterclassResults, type ParticipantResult, type SessionResults } from "@/lib/results";

export const metadata: Metadata = { title: "Results · Masterclass Quiz", robots: { index: false, follow: false } };

// Host-only: per-Session and per-Question results, and every Participant's Recap by Nickname.
export default function ResultsPage({ params }: PageProps<"/host/masterclass/[id]/results">) {
  return (
    <Page width="wide">
      <Suspense fallback={<p className={styles.muted}>Loading…</p>}>
        <Results params={params} />
      </Suspense>
    </Page>
  );
}

async function Results({ params }: { params: Promise<{ id: string }> }) {
  const id = Number((await params).id);
  if (!Number.isInteger(id) || id <= 0) notFound();
  await requireHost();
  const results = await getMasterclassResults(id);
  if (!results) notFound();

  return (
    <Stack gap="lg">
      <nav className={styles.nav}>
        <Link href="/host">← Back office</Link>
      </nav>
      <header className={styles.titles}>
        <Eyebrow>
          {results.courseTitle} · {results.masterclassStatus}
        </Eyebrow>
        <h1>Results · {results.masterclassName}</h1>
      </header>
      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>Sessions</h2>
        {results.sessions.length === 0 ? (
          <p data-testid="no-sessions" className={styles.muted}>
            No Session has run yet.
          </p>
        ) : (
          results.sessions.map((session) => <Session key={session.sessionId} session={session} />)
        )}
      </section>
      <Participants participants={results.participants} />
    </Stack>
  );
}

function Session({ session }: { session: SessionResults }) {
  return (
    <Card as="article" data-testid="session-results" className={styles.section}>
      <h3>
        {String(session.workshopNumber).padStart(2, "0")} {session.workshopTitle}
        {!session.ended && " (running)"}
      </h3>
      {session.questions.length === 0 ? (
        <p className={styles.muted}>No Question revealed yet.</p>
      ) : (
        <div className={styles.tableWrap}>
          <table>
            <thead>
              <tr>
                <th scope="col">Question</th>
                <th scope="col" className={styles.num}>
                  Correct
                </th>
                <th scope="col" className={styles.num}>
                  Incorrect
                </th>
                <th scope="col" className={styles.num}>
                  Timed out
                </th>
                <th scope="col" className={styles.num}>
                  % correct
                </th>
              </tr>
            </thead>
            <tbody>
              {session.questions.map((question) => (
                <tr key={question.number} data-testid="question-result">
                  <th scope="row" className={styles.plainCell}>
                    Q{question.number} · {question.difficulty} · {question.text}
                  </th>
                  <td className={styles.num}>{question.correct}</td>
                  <td className={styles.num}>{question.incorrect}</td>
                  <td className={styles.num}>{question.timedOut}</td>
                  <td className={styles.num}>{question.percentCorrect === null ? "–" : `${question.percentCorrect}%`}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  );
}

function Participants({ participants }: { participants: ParticipantResult[] }) {
  return (
    <section className={styles.section}>
      <h2 className={styles.sectionTitle}>Participants</h2>
      {participants.length === 0 ? (
        <p data-testid="no-participants" className={styles.muted}>
          Nobody has joined.
        </p>
      ) : (
        <Card padded={false} className={styles.tableWrap}>
          <table>
            <thead>
              <tr>
                <th scope="col">Nickname</th>
                <th scope="col" className={styles.num}>
                  Points
                </th>
              </tr>
            </thead>
            <tbody>
              {participants.map((participant) => (
                <tr key={participant.participantId} data-testid="participant-result">
                  <th scope="row" className={styles.plainCell}>
                    <Link href={`/recap/${participant.recapToken}`} target="_blank">
                      {participant.nickname}
                    </Link>
                  </th>
                  <td className={styles.num}>{participant.points}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}
    </section>
  );
}
