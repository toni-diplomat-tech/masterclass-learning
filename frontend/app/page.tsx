import { Suspense } from "react";
import { Page } from "@/components/page";
import { JoinForm } from "./join-form";
import styles from "./participant.module.scss";

// A phone already in a Masterclass Open never sees this: proxy.ts redirects it to /play first.
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

async function JoinFromLink({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const { code } = await searchParams;
  return <JoinForm code={typeof code === "string" && /^[0-9]{6}$/.test(code) ? code : undefined} />;
}
