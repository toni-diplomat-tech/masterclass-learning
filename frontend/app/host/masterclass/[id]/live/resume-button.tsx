"use client";

import { useState } from "react";

/**
 * Manual fallback for restart recovery: asks the backend to follow the running Session again, so an
 * overdue Question is revealed and the Session carries on. Harmless when the Session is moving anyway.
 */
export function ResumeButton({ onResume }: { onResume: () => Promise<void> }) {
  const [status, setStatus] = useState<"idle" | "resuming" | "failed">("idle");

  const resume = async () => {
    setStatus("resuming");
    try {
      await onResume();
      setStatus("idle");
    } catch {
      setStatus("failed");
    }
  };

  return (
    <p>
      <button type="button" data-testid="resume-session" onClick={resume} disabled={status === "resuming"}>
        Resume
      </button>
      {status === "failed" && <small style={{ display: "block" }}>Could not resume. Check the connection and try again.</small>}
    </p>
  );
}
