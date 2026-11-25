import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { Suspense } from "react";
import { getRecap, lessonLabel, type RecapLesson, type RecapWorkshop } from "@/lib/recaps";

export const metadata: Metadata = { title: "Recap · Masterclass Quiz", robots: { index: false, follow: false } };

// Anyone with the link can read it: no login, no expiry, still there after the Masterclass is closed.
export default function RecapPage({ params }: PageProps<"/recap/[token]">) {
  return (
    <main style={{ padding: "2rem", maxWidth: "42rem", margin: "0 auto" }}>
      <Suspense fallback={<p>Loading…</p>}>
        <RecapView params={params} />
      </Suspense>
    </main>
  );
}

async function RecapView({ params }: { params: Promise<{ token: string }> }) {
  const recap = await getRecap((await params).token);
  if (!recap) notFound();

  return (
    <>
      <p>
        {recap.masterclassName} · {recap.courseTitle}
      </p>
      <h1>Recap for {recap.nickname}</h1>
      {recap.workshops.length === 0 ? (
        <p data-testid="recap-empty">Nothing here yet. Each Workshop appears once its Session has ended.</p>
      ) : (
        recap.workshops.map((workshop) => <Workshop key={workshop.number} workshop={workshop} />)
      )}
    </>
  );
}

function Workshop({ workshop }: { workshop: RecapWorkshop }) {
  return (
    <section data-testid="recap-workshop" style={{ marginTop: "2rem" }}>
      <h2>{workshop.title}</h2>
      {workshop.outcome === "PerfectScore" && (
        <p data-testid="perfect-score" style={{ fontSize: "1.5rem", fontWeight: "bold" }}>
          ★ Perfect score
        </p>
      )}
      {workshop.outcome === "NotAttended" && (
        <>
          <p data-testid="not-attended">Not attended. These are the Workshop&apos;s Lessons to revisit:</p>
          <ul>
            {workshop.lessons.map((lesson) => (
              <li key={lesson.number}>
                <LessonLink workshopTitle={workshop.title} lesson={lesson} />
              </li>
            ))}
          </ul>
        </>
      )}
      {workshop.outcome === "MissedQuestions" && (
        <>
          <p>Lessons to revisit:</p>
          {workshop.lessons.map((lesson) => (
            <MissedLesson key={lesson.number} workshopTitle={workshop.title} lesson={lesson} />
          ))}
        </>
      )}
    </section>
  );
}

function MissedLesson({ workshopTitle, lesson }: { workshopTitle: string; lesson: RecapLesson }) {
  return (
    <article data-testid="missed-lesson" style={{ marginBottom: "1.5rem" }}>
      <h3>
        <LessonLink workshopTitle={workshopTitle} lesson={lesson} />
      </h3>
      <ul>
        {lesson.questions.map((question, index) => (
          <li key={index} style={{ marginBottom: "0.75rem" }}>
            <p style={{ margin: 0 }}>{question.text}</p>
            <p style={{ margin: 0 }}>Your Answer: {question.timedOut ? "Timed out" : question.answer}</p>
            <p style={{ margin: 0 }}>Correct: {question.correctOption}</p>
          </li>
        ))}
      </ul>
    </article>
  );
}

function LessonLink({ workshopTitle, lesson }: { workshopTitle: string; lesson: RecapLesson }) {
  return (
    <a href={lesson.url} target="_blank" rel="noopener noreferrer">
      {lessonLabel(workshopTitle, lesson)}
    </a>
  );
}
