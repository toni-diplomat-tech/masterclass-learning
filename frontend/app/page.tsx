import { redirect } from "next/navigation";
import { Suspense } from "react";
import { Page } from "@/components/page";
import { belongsOnPlay, currentParticipant } from "@/lib/participant";
import { JoinForm } from "./join-form";
import styles from "./participant.module.scss";

export default function Home({ searchParams }: PageProps<"/">) {
  return (
    <Page width="phone">
      <div className={styles.hero}>
        <h1 className={styles.title}>
          Masterclass Quiz<span className={styles.accent}>.</span>
        </h1>
        <p className={styles.label}>Enter the Join code on the screen.</p>
      </div>
      <Suspense fallback={<JoinForm />}>
        <JoinFromLink searchParams={searchParams} />
      </Suspense>
    </Page>
  );
}

// A phone that has already joined a Masterclass Open goes back to /play (QR link `/?code=…` too),
// decided here on the server: `/` never connects to the hub.
async function JoinFromLink({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  // An unreachable backend shows the form; joining then reports it.
  const participant = await currentParticipant().catch(() => null);
  if (belongsOnPlay(participant)) redirect("/play");
  const { code } = await searchParams;
  return <JoinForm code={typeof code === "string" && /^[0-9]{6}$/.test(code) ? code : undefined} />;
}
