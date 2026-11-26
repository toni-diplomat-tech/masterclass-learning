import Link from "next/link";
import { Suspense } from "react";
import { Button } from "@/components/button";
import { Card } from "@/components/card";
import { Page, Stack } from "@/components/page";
import { listCourses, type CourseOverview } from "@/lib/courses";
import { requireHost } from "@/lib/host-session";
import { listMasterclasses, type MasterclassOverview } from "@/lib/masterclasses";
import { logout } from "./actions";
import { CreateMasterclassForm } from "./create-masterclass-form";
import styles from "./host.module.scss";
import { ImportForm } from "./import-form";

export default function HostPage() {
  return (
    <Page width="wide">
      <Stack gap="lg">
        <h1>Back office</h1>
        <Suspense fallback={<p className={styles.muted}>Checking your session…</p>}>
          <BackOffice />
        </Suspense>
      </Stack>
    </Page>
  );
}

async function BackOffice() {
  await requireHost();
  const [courses, masterclasses] = await Promise.all([listCourses(), listMasterclasses()]);
  return (
    <>
      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>Masterclasses</h2>
        <Card>
          {courses.length === 0 ? (
            <p className={styles.muted}>Import a Course first, then create a Masterclass for it.</p>
          ) : (
            <CreateMasterclassForm courses={courses.map(({ id, title }) => ({ id, title }))} />
          )}
        </Card>
        {masterclasses.length > 0 && <Masterclasses masterclasses={masterclasses} />}
      </section>
      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>Courses</h2>
        <Card>
          <ImportForm />
        </Card>
        {courses.length === 0 ? (
          <p className={styles.muted}>No Courses yet. Import one above.</p>
        ) : (
          courses.map((course) => <Course key={course.id} course={course} />)
        )}
      </section>
      <form action={logout}>
        <Button type="submit" variant="secondary">
          Log out
        </Button>
      </form>
    </>
  );
}

function Course({ course }: { course: CourseOverview }) {
  return (
    <Card as="article" data-testid="course" className={styles.section}>
      <h3>{course.title}</h3>
      <div className={styles.tableWrap}>
        <table>
          <thead>
            <tr>
              <th scope="col">Workshop</th>
              <th scope="col" className={styles.num}>
                Easy
              </th>
              <th scope="col" className={styles.num}>
                Medium
              </th>
              <th scope="col" className={styles.num}>
                Hard
              </th>
            </tr>
          </thead>
          <tbody>
            {course.workshops.map((workshop) => (
              <tr key={workshop.id}>
                <th scope="row">
                  {String(workshop.number).padStart(2, "0")} {workshop.title}
                </th>
                <td className={styles.num}>{workshop.questions.easy}</td>
                <td className={styles.num}>{workshop.questions.medium}</td>
                <td className={styles.num}>{workshop.questions.hard}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  );
}

function Masterclasses({ masterclasses }: { masterclasses: MasterclassOverview[] }) {
  return (
    <Card padded={false} className={styles.tableWrap}>
      <table data-testid="masterclasses">
        <thead>
          <tr>
            <th scope="col">Masterclass</th>
            <th scope="col">Course</th>
            <th scope="col">Status</th>
            <th scope="col">Join code</th>
            <th scope="col" className={styles.num}>
              Participants
            </th>
            <th scope="col">Links</th>
          </tr>
        </thead>
        <tbody>
          {masterclasses.map((masterclass) => (
            <tr key={masterclass.id}>
              <th scope="row">{masterclass.name}</th>
              <td>{masterclass.courseTitle}</td>
              <td>
                <span className={[styles.status, masterclass.status === "Open" && styles.statusOpen].filter(Boolean).join(" ")}>
                  {masterclass.status}
                </span>
              </td>
              <td className={styles.code}>{masterclass.joinCode ?? "–"}</td>
              <td className={styles.num}>{masterclass.participants}</td>
              <td>
                <span className={styles.links}>
                  <Link href={`/host/masterclass/${masterclass.id}/live`}>Projector view</Link>
                  {masterclass.status !== "Draft" && <Link href={`/host/masterclass/${masterclass.id}/results`}>Results</Link>}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </Card>
  );
}
