"use client";

import { useActionState } from "react";
import { join, type JoinState } from "./actions";

/** The Join code form; a QR code link fills in the code (`/?code=123456`). */
export function JoinForm({ code }: { code?: string }) {
  const [state, formAction, pending] = useActionState(join, { joinCode: code } satisfies JoinState);

  return (
    <form action={formAction}>
      <p>
        <label htmlFor="join-code">Join code</label>
      </p>
      <p>
        <input
          id="join-code"
          name="joinCode"
          defaultValue={state.joinCode}
          key={state.joinCode}
          inputMode="numeric"
          autoComplete="off"
          pattern="[0-9]{6}"
          maxLength={6}
          placeholder="123456"
          required
          autoFocus
          style={{ fontSize: "2rem", width: "8ch", letterSpacing: "0.2em", textAlign: "center" }}
        />
      </p>
      {state.error && (
        <p role="alert" data-testid="join-error">
          {state.error}
        </p>
      )}
      <button type="submit" disabled={pending} style={{ fontSize: "1.25rem" }}>
        {pending ? "Joining…" : "Join"}
      </button>
    </form>
  );
}
