"use client";

import { useActionState } from "react";
import { Button } from "@/components/button";
import { Notice } from "@/components/notice";
import { login, type LoginState } from "../actions";
import styles from "../host.module.scss";

const initialState: LoginState = {};

export function LoginForm() {
  const [state, formAction, pending] = useActionState(login, initialState);

  return (
    <form action={formAction} className={styles.inlineForm}>
      <label htmlFor="password" className={styles.field}>
        Password
        <input id="password" name="password" type="password" autoComplete="current-password" required autoFocus />
      </label>
      {state.error && (
        <Notice tone="error" role="alert" data-testid="login-error">
          {state.error}
        </Notice>
      )}
      <Button type="submit" block loading={pending}>
        {pending ? "Logging in…" : "Log in"}
      </Button>
    </form>
  );
}
