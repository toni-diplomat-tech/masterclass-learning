import { Suspense } from "react";

type BackendHealth = {
  backend: string;
  database: string;
  databaseCheckedAtUtc: string | null;
};

// Server-side only: inside Docker the backend is reached on the compose network.
const backendUrl = () => process.env.BACKEND_URL ?? "http://localhost:5088";

async function fetchHealth(): Promise<BackendHealth | null> {
  try {
    const response = await fetch(`${backendUrl()}/health`, { cache: "no-store" });
    return (await response.json()) as BackendHealth;
  } catch {
    return null;
  }
}

async function HealthStatus() {
  const health = await fetchHealth();
  return (
    <dl>
      <dt>Backend</dt>
      <dd data-testid="backend-status">{health ? health.backend : "unreachable"}</dd>
      <dt>Database</dt>
      <dd data-testid="database-status">{health ? health.database : "unknown"}</dd>
      {health?.databaseCheckedAtUtc && (
        <>
          <dt>Server time (UTC)</dt>
          <dd>{health.databaseCheckedAtUtc}</dd>
        </>
      )}
    </dl>
  );
}

export default function HealthPage() {
  return (
    <main style={{ padding: "2rem" }}>
      <h1>System health</h1>
      <Suspense fallback={<p>Checking…</p>}>
        <HealthStatus />
      </Suspense>
    </main>
  );
}
