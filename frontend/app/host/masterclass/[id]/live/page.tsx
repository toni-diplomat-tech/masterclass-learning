import type { Metadata } from "next";
import { headers } from "next/headers";
import Link from "next/link";
import { notFound } from "next/navigation";
import QRCode from "qrcode";
import { Suspense } from "react";
import { Eyebrow, Page } from "@/components/page";
import { listCourses } from "@/lib/courses";
import { requireHost } from "@/lib/host-session";
import { getMasterclass, getMasterclassLeaderboard, type MasterclassOverview } from "@/lib/masterclasses";
import { CloseMasterclassForm } from "./close-masterclass-form";
import { LiveMasterclass } from "./live-masterclass";
import { MasterclassLeaderboardTable } from "./masterclass-leaderboard";
import { OpenMasterclassForm } from "./open-masterclass-form";
import styles from "./projector.module.scss";

export const metadata: Metadata = { title: "Projector · Masterclass Quiz" };

export default function ProjectorPage({ params }: PageProps<"/host/masterclass/[id]/live">) {
  return (
    <Page width="projector" center>
      <Suspense fallback={<p className={styles.muted}>Loading…</p>}>
        <Projector params={params} />
      </Suspense>
    </Page>
  );
}

async function Projector({ params }: { params: Promise<{ id: string }> }) {
  const id = Number((await params).id);
  if (!Number.isInteger(id) || id <= 0) notFound();
  await requireHost();
  const masterclass = await getMasterclass(id);
  if (!masterclass) notFound();

  return (
    <>
      <div className={styles.top}>
        <nav className={styles.nav}>
          <Link href="/host">← Back office</Link>
          {masterclass.status !== "Draft" && (
            <Link href={`/host/masterclass/${masterclass.id}/results`} data-testid="results-link">
              Results
            </Link>
          )}
        </nav>
        <Eyebrow>{masterclass.courseTitle}</Eyebrow>
      </div>
      <div className={styles.screen}>
        <h1 className={styles.title}>{masterclass.name}</h1>
        {masterclass.status === "Draft" && <OpenMasterclassForm masterclassId={masterclass.id} />}
        {masterclass.status === "Open" && <OpenProjector masterclass={masterclass} />}
        {masterclass.status === "Closed" && <ClosedProjector masterclass={masterclass} />}
      </div>
    </>
  );
}

// The end of the meeting: the Masterclass leaderboard over all Sessions.
async function ClosedProjector({ masterclass }: { masterclass: MasterclassOverview }) {
  const leaderboard = await getMasterclassLeaderboard(masterclass.id);
  return (
    <>
      <p className={styles.muted}>This Masterclass is closed.</p>
      {leaderboard && <MasterclassLeaderboardTable leaderboard={leaderboard} />}
    </>
  );
}

// The live part: Join invitation and count, start-Session dialog for the Next Workshop, Questions.
async function OpenProjector({ masterclass }: { masterclass: MasterclassOverview }) {
  const course = (await listCourses()).find((c) => c.id === masterclass.courseId);
  return (
    <LiveMasterclass
      masterclassId={masterclass.id}
      initialParticipants={masterclass.participants}
      workshops={course?.workshops ?? []}
      nextWorkshopId={masterclass.nextWorkshopId}
      closeControl={<CloseMasterclassForm masterclassId={masterclass.id} />}
    >
      <JoinInvitation masterclass={masterclass} />
    </LiveMasterclass>
  );
}

async function JoinInvitation({ masterclass }: { masterclass: MasterclassOverview }) {
  const joinUrl = `${await siteOrigin()}/?code=${masterclass.joinCode}`;
  const qrSvg = await QRCode.toString(joinUrl, { type: "svg", margin: 1, errorCorrectionLevel: "M" });

  return (
    <section className={styles.invite}>
      <div>
        <p className={styles.joinAt}>
          Join at <strong className={styles.joinHost}>{joinUrl.replace(/^https?:\/\//, "").replace(/\/\?.*$/, "")}</strong> with the code
        </p>
        <p data-testid="join-code" className={styles.joinCode}>
          {masterclass.joinCode}
        </p>
      </div>
      <div
        data-testid="join-qr-code"
        role="img"
        aria-label={`QR code to join: ${joinUrl}`}
        className={styles.qr}
        dangerouslySetInnerHTML={{ __html: qrSvg }}
      />
    </section>
  );
}

// The address phones use: the browser's host as seen through Caddy (which sets x-forwarded-proto).
async function siteOrigin(): Promise<string> {
  const requestHeaders = await headers();
  const host = requestHeaders.get("x-forwarded-host") ?? requestHeaders.get("host") ?? "localhost:3000";
  const protocol = requestHeaders.get("x-forwarded-proto") ?? "http";
  return `${protocol}://${host}`;
}
