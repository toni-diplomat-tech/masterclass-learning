"use client";

import { useActionState } from "react";
import { openMasterclassAction, type MasterclassActionState } from "@/app/host/actions";
import { Button } from "@/components/button";
import { Notice } from "@/components/notice";
import { ProblemsNotice } from "@/components/problems-notice";
import styles from "./projector.module.scss";

const initialState: MasterclassActionState = {};

/**
 * The Host's Open Masterclass button on the projector of a Draft, for when the meeting starts. Only one
 * Masterclass can be Open at a time, so it may say which Masterclass to close first.
 */
export function OpenMasterclassForm({ masterclassId }: { masterclassId: number }) {
  const [state, formAction, pending] = useActionState(openMasterclassAction.bind(null, masterclassId), initialState);

  return (
    <form action={formAction} className={styles.openForm}>
      <p className={styles.muted}>Open the Masterclass when the meeting starts. Participants can then join with its Join code.</p>
      <Button type="submit" size="lg" loading={pending} data-testid="open-masterclass">
        {pending ? "Opening…" : "Open Masterclass"}
      </Button>
      <ProblemsNotice problems={state.problems} data-testid="open-masterclass-problems" />
      {state.error && (
        <Notice tone="error" role="alert">
          {state.error}
        </Notice>
      )}
    </form>
  );
}
