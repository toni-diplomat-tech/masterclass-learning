import Link from "next/link";
import { Suspense } from "react";
import { listCourses, type CourseOverview } from "@/lib/courses";
import { requireHost } from "@/lib/host-session";
import { listMasterclasses, type MasterclassOverview } from "@/lib/masterclasses";
import { logout } from "./actions";
import { CreateMasterclassForm } from "./create-masterclass-form";
import { ImportForm } from "./import-form";

export default function HostPage() {
  return (
    <main style={{ padding: "2rem" }}>
      <h1>Back office</h1>
      <Suspense fallback={<p>Checking your session…</p>}>
        <BackOffice />
      </Suspense>
    </main>
  );
}

async function BackOffice() {
  await requireHost();
  const [courses, masterclasses] = await Promise.all([listCourses(), listMasterclasses()]);
  return (
    <>
      <section>
        <h2>Masterclasses</h2>
        {courses.length === 0 ? (
          <p>Import a Course first, then create a Masterclass for it.</p>
        ) : (
          <CreateMasterclassForm courses={courses.map(({ id, title }) => ({ id, title }))} />
        )}
        {masterclasses.length > 0 && <Masterclasses masterclasses={masterclasses} />}
      </section>
      <section>
        <h2>Courses</h2>
        <ImportForm />
        {courses.length === 0 ? <p>No Courses yet. Import one above.</p> : courses.map((course) => <Course key={course.id} course={course} />)}
      </section>
      <form action={logout}>
        <button type="submit">Log out</button>
      </form>
    </>
  );
}

function Course({ course }: { course: CourseOverview }) {
  return (
    <article data-testid="course">
      <h3>{course.title}</h3>
      <table>
        <thead>
          <tr>
            <th scope="col">Workshop</th>
            <th scope="col">Easy</th>
            <th scope="col">Medium</th>
            <th scope="col">Hard</th>
          </tr>
        </thead>
        <tbody>
          {course.workshops.map((workshop) => (
            <tr key={workshop.id}>
              <th scope="row">
                {String(workshop.number).padStart(2, "0")} {workshop.title}
              </th>
              <td>{workshop.questions.easy}</td>
              <td>{workshop.questions.medium}</td>
              <td>{workshop.questions.hard}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </article>
  );
}

function Masterclasses({ masterclasses }: { masterclasses: MasterclassOverview[] }) {
  return (
    <table data-testid="masterclasses">
      <thead>
        <tr>
          <th scope="col">Masterclass</th>
          <th scope="col">Course</th>
          <th scope="col">Status</th>
          <th scope="col">Join code</th>
          <th scope="col">Participants</th>
          <th scope="col">Links</th>
        </tr>
      </thead>
      <tbody>
        {masterclasses.map((masterclass) => (
          <tr key={masterclass.id}>
            <th scope="row">{masterclass.name}</th>
            <td>{masterclass.courseTitle}</td>
            <td>{masterclass.status}</td>
            <td>{masterclass.joinCode ?? "–"}</td>
            <td>{masterclass.participants}</td>
            <td>
              <Link href={`/host/masterclass/${masterclass.id}/live`}>Projector view</Link>
              {masterclass.status !== "Draft" && (
                <>
                  {" · "}
                  <Link href={`/host/masterclass/${masterclass.id}/results`}>Results</Link>
                </>
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
