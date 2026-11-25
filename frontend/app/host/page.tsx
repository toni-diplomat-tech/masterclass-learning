import { Suspense } from "react";
import { requireHost } from "@/lib/host-session";
import { logout } from "./actions";

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
  return (
    <>
      <p>Courses and Masterclasses will appear here.</p>
      <form action={logout}>
        <button type="submit">Log out</button>
      </form>
    </>
  );
}
