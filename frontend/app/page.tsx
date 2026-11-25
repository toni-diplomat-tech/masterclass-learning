import { Suspense } from "react";
import { JoinForm } from "./join-form";

export default function Home({ searchParams }: PageProps<"/">) {
  return (
    <main style={{ padding: "2rem", textAlign: "center" }}>
      <h1>Masterclass Quiz</h1>
      <Suspense fallback={<JoinForm />}>
        <JoinFromLink searchParams={searchParams} />
      </Suspense>
    </main>
  );
}

async function JoinFromLink({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const { code } = await searchParams;
  return <JoinForm code={typeof code === "string" && /^[0-9]{6}$/.test(code) ? code : undefined} />;
}
