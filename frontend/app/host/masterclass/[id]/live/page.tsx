import type { Metadata } from "next";
import { headers } from "next/headers";
import Link from "next/link";
import { notFound } from "next/navigation";
import QRCode from "qrcode";
import { Suspense } from "react";
import { openMasterclassAction } from "@/app/host/actions";
import { requireHost } from "@/lib/host-session";
import { getMasterclass, type MasterclassOverview } from "@/lib/masterclasses";
import { ParticipantCount } from "./participant-count";

export const metadata: Metadata = { title: "Projector · Masterclass Quiz" };

export default function ProjectorPage({ params }: PageProps<"/host/masterclass/[id]/live">) {
  return (
    <main style={{ padding: "2rem", textAlign: "center" }}>
      <Suspense fallback={<p>Loading…</p>}>
        <Projector params={params} />
      </Suspense>
    </main>
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
      <p style={{ textAlign: "left" }}>
        <Link href="/host">← Back office</Link>
      </p>
      <h1>{masterclass.name}</h1>
      <p>{masterclass.courseTitle}</p>
      {masterclass.status === "Draft" && <OpenMasterclass masterclass={masterclass} />}
      {masterclass.status === "Open" && <JoinInvitation masterclass={masterclass} />}
      {masterclass.status === "Closed" && <p>This Masterclass is closed.</p>}
    </>
  );
}

function OpenMasterclass({ masterclass }: { masterclass: MasterclassOverview }) {
  return (
    <form action={openMasterclassAction.bind(null, masterclass.id)}>
      <p>Open the Masterclass when the meeting starts. Participants can then join with its Join code.</p>
      <button type="submit" style={{ fontSize: "1.5rem" }}>
        Open Masterclass
      </button>
    </form>
  );
}

async function JoinInvitation({ masterclass }: { masterclass: MasterclassOverview }) {
  const joinUrl = `${await siteOrigin()}/?code=${masterclass.joinCode}`;
  const qrSvg = await QRCode.toString(joinUrl, { type: "svg", margin: 1, errorCorrectionLevel: "M" });

  return (
    <section>
      <p style={{ fontSize: "1.5rem" }}>
        Join at <strong>{joinUrl.replace(/^https?:\/\//, "").replace(/\/\?.*$/, "")}</strong> with the code
      </p>
      <p data-testid="join-code" style={{ fontSize: "6rem", fontWeight: "bold", letterSpacing: "0.2em", margin: "1rem 0" }}>
        {masterclass.joinCode}
      </p>
      <div
        data-testid="join-qr-code"
        role="img"
        aria-label={`QR code to join: ${joinUrl}`}
        style={{ width: "min(40vh, 80vw)", margin: "0 auto", background: "#fff" }}
        dangerouslySetInnerHTML={{ __html: qrSvg }}
      />
      <ParticipantCount masterclassId={masterclass.id} initial={masterclass.participants} />
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
