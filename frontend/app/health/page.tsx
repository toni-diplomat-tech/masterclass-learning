import { Suspense } from "react";

// Matches the backend's HealthStatus enum, serialized as lowercase strings.
type HealthStatus = "ok" | "unreachable";

type BackendHealth = {
  backend: HealthStatus;
  database: HealthStatus;
  databaseCheckedAtUtc: string | null;
};

type HealthCheck =
  | { kind: "reported"; health: BackendHealth }
  | { kind: "bad-response"; detail: string }
  | { kind: "unreachable" };

// Server-side only: inside Docker the backend is reached on the compose network.
const backendUrl = () => process.env.BACKEND_URL ?? "http://localhost:5088";

// A backend that hangs counts as unreachable instead of hanging the page.
// Kept above the 3 s the backend allows its own database check.
const BACKEND_TIMEOUT_MS = 5000;

const HEALTH_STATUSES: readonly string[] = ["ok", "unreachable"] satisfies HealthStatus[];

function isBackendHealth(body: unknown): body is BackendHealth {
  if (typeof body !== "object" || body === null) return false;
  const { backend, database, databaseCheckedAtUtc } = body as Record<string, unknown>;
  return (
    HEALTH_STATUSES.includes(backend as string) &&
    HEALTH_STATUSES.includes(database as string) &&
    (databaseCheckedAtUtc === null || typeof databaseCheckedAtUtc === "string")
  );
}

async function checkHealth(): Promise<HealthCheck> {
  let response: Response;
  try {
    response = await fetch(`${backendUrl()}/health`, {
      cache: "no-store",
      signal: AbortSignal.timeout(BACKEND_TIMEOUT_MS),
    });
  } catch {
    return { kind: "unreachable" };
  }

  // The backend answers 503 with a health body when the database is down, so read the body first.
  let body: unknown;
  try {
    body = await response.json();
  } catch {
    return { kind: "bad-response", detail: `HTTP ${response.status}, not JSON` };
  }
  if (isBackendHealth(body)) return { kind: "reported", health: body };
  return { kind: "bad-response", detail: `HTTP ${response.status}, unexpected body` };
}

async function HealthStatus() {
  const check = await checkHealth();
  const health = check.kind === "reported" ? check.health : null;
  const backendStatus =
    check.kind === "reported"
      ? check.health.backend
      : check.kind === "bad-response"
        ? `bad response (${check.detail})`
        : "unreachable";
  return (
    <dl>
      <dt>Backend</dt>
      <dd data-testid="backend-status">{backendStatus}</dd>
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
