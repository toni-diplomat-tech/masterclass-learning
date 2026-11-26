import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { Suspense } from "react";
import { requireHost } from "@/lib/host-session";
import { getMasterclassResults, type ParticipantResult, type SessionResults } from "@/lib/results";

export const metadata: Metadata = { title: "Results · Masterclass Quiz", robots: { index: false, follow: false } };

// Host-only: per-Session and per-Question results, and every Participant's Recap by Nickname.
export default function ResultsPage({ params }: PageProps<"/host/masterclass/[id]/results">) {
  return (
    <main style={{ padding: "2rem", maxWidth: "60rem", margin: "0 auto" }}>
      <Suspense fallback={<p>Loading…</p>}>
        <Results params={params} />
      </Suspense>
    </main>
  );
}

async function Results({ params }: { params: Promise<{ id: string }> }) {
  const id = Number((await params).id);
  if (!Number.isInteger(id) || id <= 0) notFound();
  await requireHost();
  const results = await getMasterclassResults(id);
  if (!results) notFound();

  return (
    <>
      <p>
        <Link href="/host">← Back office</Link>
      </p>
      <h1>Results · {results.masterclassName}</h1>
      <p>
        {results.courseTitle} · {results.masterclassStatus}
      </p>
      <section>
        <h2>Sessions</h2>
        {results.sessions.length === 0 ? (
          <p data-testid="no-sessions">No Session has run yet.</p>
        ) : (
          results.sessions.map((session) => <Session key={session.sessionId} session={session} />)
        )}
      </section>
      <Participants participants={results.participants} />
    </>
  );
}

const cell = { padding: "0.25rem 0.75rem", textAlign: "right" } as const;

function Session({ session }: { session: SessionResults }) {
  return (
    <article data-testid="session-results" style={{ marginBottom: "2rem" }}>
      <h3>
        {String(session.workshopNumber).padStart(2, "0")} {session.workshopTitle}
        {!session.ended && " (running)"}
      </h3>
      {session.questions.length === 0 ? (
        <p>No Question revealed yet.</p>
      ) : (
        <table style={{ borderCollapse: "collapse", width: "100%" }}>
          <thead>
            <tr>
              <th scope="col" style={{ ...cell, textAlign: "left" }}>
                Question
              </th>
              <th scope="col" style={cell}>
                Correct
              </th>
              <th scope="col" style={cell}>
                Incorrect
              </th>
              <th scope="col" style={cell}>
                Timed out
              </th>
              <th scope="col" style={cell}>
                % correct
              </th>
            </tr>
          </thead>
          <tbody>
            {session.questions.map((question) => (
              <tr key={question.number} data-testid="question-result">
                <th scope="row" style={{ ...cell, textAlign: "left", fontWeight: "normal" }}>
                  Q{question.number} · {question.difficulty} · {question.text}
                </th>
                <td style={cell}>{question.correct}</td>
                <td style={cell}>{question.incorrect}</td>
                <td style={cell}>{question.timedOut}</td>
                <td style={cell}>{question.percentCorrect === null ? "–" : `${question.percentCorrect}%`}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </article>
  );
}

function Participants({ participants }: { participants: ParticipantResult[] }) {
  return (
    <section>
      <h2>Participants</h2>
      {participants.length === 0 ? (
        <p data-testid="no-participants">Nobody has joined.</p>
      ) : (
        <table style={{ borderCollapse: "collapse" }}>
          <thead>
            <tr>
              <th scope="col" style={{ ...cell, textAlign: "left" }}>
                Nickname
              </th>
              <th scope="col" style={cell}>
                Points
              </th>
            </tr>
          </thead>
          <tbody>
            {participants.map((participant) => (
              <tr key={participant.participantId} data-testid="participant-result">
                <th scope="row" style={{ ...cell, textAlign: "left", fontWeight: "normal" }}>
                  <Link href={`/recap/${participant.recapToken}`} target="_blank">
                    {participant.nickname}
                  </Link>
                </th>
                <td style={cell}>{participant.points}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
