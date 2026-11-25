import { Suspense } from "react";
import { listCourses, type CourseOverview } from "@/lib/courses";
import { requireHost } from "@/lib/host-session";
import { logout } from "./actions";
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
  const courses = await listCourses();
  return (
    <>
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
