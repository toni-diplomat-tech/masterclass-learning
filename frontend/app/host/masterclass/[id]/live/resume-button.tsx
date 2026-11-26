"use client";

import { useState } from "react";
import { Button } from "@/components/button";
import styles from "./projector.module.scss";

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
      <Button variant="ghost" data-testid="resume-session" onClick={resume} loading={status === "resuming"}>
        Resume
      </Button>
      {status === "failed" && <small className={styles.block}>Could not resume. Check the connection and try again.</small>}
    </p>
  );
}
