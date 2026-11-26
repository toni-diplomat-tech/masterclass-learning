import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { Suspense } from "react";
import { Card } from "@/components/card";
import { Notice } from "@/components/notice";
import { Eyebrow, Page, Stack } from "@/components/page";
import { getRecap, lessonLabel, type RecapLesson, type RecapWorkshop } from "@/lib/recaps";
import styles from "./recap.module.scss";

export const metadata: Metadata = { title: "Recap · Masterclass Quiz", robots: { index: false, follow: false } };

// Anyone with the link can read it: no login, no expiry, still there after the Masterclass is closed.
export default function RecapPage({ params }: PageProps<"/recap/[token]">) {
  return (
    <Page width="text">
      <Suspense fallback={<p>Loading…</p>}>
        <RecapView params={params} />
      </Suspense>
    </Page>
  );
}

async function RecapView({ params }: { params: Promise<{ token: string }> }) {
  const recap = await getRecap((await params).token);
  if (!recap) notFound();

  return (
    <Stack gap="lg">
      <header className={styles.header}>
        <Eyebrow>
          {recap.masterclassName} · {recap.courseTitle}
        </Eyebrow>
        <h1>Recap for {recap.nickname}</h1>
      </header>
      {recap.workshops.length === 0 ? (
        <Notice data-testid="recap-empty">Nothing here yet. Each Workshop appears once its Session has ended.</Notice>
      ) : (
        recap.workshops.map((workshop) => <Workshop key={workshop.number} workshop={workshop} />)
      )}
    </Stack>
  );
}

function Workshop({ workshop }: { workshop: RecapWorkshop }) {
  return (
    <Card data-testid="recap-workshop" className={styles.workshop}>
      <h2>{workshop.title}</h2>
      {workshop.outcome === "PerfectScore" && (
        <p data-testid="perfect-score" className={styles.perfect}>
          ★ Perfect score
        </p>
      )}
      {workshop.outcome === "NotAttended" && (
        <>
          <p data-testid="not-attended" className={styles.muted}>
            Not attended. These are the Workshop&apos;s Lessons to revisit:
          </p>
          <ul className={styles.lessons}>
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
          <p className={styles.muted}>Lessons to revisit:</p>
          {workshop.lessons.map((lesson) => (
            <MissedLesson key={lesson.number} workshopTitle={workshop.title} lesson={lesson} />
          ))}
        </>
      )}
    </Card>
  );
}

function MissedLesson({ workshopTitle, lesson }: { workshopTitle: string; lesson: RecapLesson }) {
  return (
    <article data-testid="missed-lesson" className={styles.lesson}>
      <h3>
        <LessonLink workshopTitle={workshopTitle} lesson={lesson} />
      </h3>
      <ul className={styles.questions}>
        {lesson.questions.map((question, index) => (
          <li key={index} className={styles.question}>
            <p>{question.text}</p>
            <p className={styles.answer}>
              Your Answer: <span className={styles.wrong}>{question.timedOut ? "Timed out" : question.answer}</span>
            </p>
            <p className={styles.answer}>
              Correct: <span className={styles.correct}>{question.correctOption}</span>
            </p>
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
