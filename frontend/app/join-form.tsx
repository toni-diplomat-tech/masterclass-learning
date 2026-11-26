"use client";

import { useActionState } from "react";
import { Button } from "@/components/button";
import { Card } from "@/components/card";
import { Notice } from "@/components/notice";
import { join, type JoinState } from "./actions";
import styles from "./participant.module.scss";

/** The Join code form; a QR code link fills in the code (`/?code=123456`). */
export function JoinForm({ code }: { code?: string }) {
  const [state, formAction, pending] = useActionState(join, { joinCode: code } satisfies JoinState);

  return (
    <Card>
      <form action={formAction} className={styles.form}>
        <label htmlFor="join-code" className={styles.label}>
          Join code
        </label>
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
          className={styles.codeInput}
        />
        {state.error && (
          <Notice tone="error" role="alert" data-testid="join-error">
            {state.error}
          </Notice>
        )}
        <Button type="submit" size="lg" block loading={pending}>
          {pending ? "Joining…" : "Join"}
        </Button>
      </form>
    </Card>
  );
}
