"use client";

import { useActionState } from "react";
import { closeMasterclassAction, type CloseMasterclassState } from "@/app/host/actions";

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
      style={{ marginTop: "2rem" }}
    >
      <button type="submit" disabled={pending} data-testid="close-masterclass">
        {pending ? "Closing…" : "Close Masterclass"}
      </button>
      {state.problems && (
        <ul role="alert" data-testid="close-masterclass-problems" style={{ listStyle: "none", padding: 0 }}>
          {state.problems.map((problem, index) => (
            <li key={index}>{problem}</li>
          ))}
        </ul>
      )}
      {state.error && <p role="alert">{state.error}</p>}
    </form>
  );
}
