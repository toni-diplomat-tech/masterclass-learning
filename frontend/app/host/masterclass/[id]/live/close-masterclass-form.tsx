"use client";

import { useActionState } from "react";
import { closeMasterclassAction, type CloseMasterclassState } from "@/app/host/actions";
import { Button } from "@/components/button";
import { Notice } from "@/components/notice";
import styles from "./projector.module.scss";

const initialState: CloseMasterclassState = {};

/** The Host's Close Masterclass button on the projector, for when the meeting ends. Asks first: closing is final. */
export function CloseMasterclassForm({ masterclassId }: { masterclassId: number }) {
  const [state, formAction, pending] = useActionState(closeMasterclassAction.bind(null, masterclassId), initialState);

  return (
    <form
      action={formAction}
      onSubmit={(event) => {
        if (!window.confirm("Close the Masterclass? Nobody can join or play a Session after this.")) event.preventDefault();
      }}
      className={styles.closeForm}
    >
      <Button type="submit" variant="danger" loading={pending} data-testid="close-masterclass">
        {pending ? "Closing…" : "Close Masterclass"}
      </Button>
      {state.problems && (
        <Notice tone="error" role="alert" data-testid="close-masterclass-problems">
          <ul className={styles.problems}>
            {state.problems.map((problem, index) => (
              <li key={index}>{problem}</li>
            ))}
          </ul>
        </Notice>
      )}
      {state.error && (
        <Notice tone="error" role="alert">
          {state.error}
        </Notice>
      )}
    </form>
  );
}
