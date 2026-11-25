"use client";

import { useActionState } from "react";
import { login, type LoginState } from "../actions";

const initialState: LoginState = {};

export function LoginForm() {
  const [state, formAction, pending] = useActionState(login, initialState);

  return (
    <form action={formAction}>
      <p>
        <label htmlFor="password">Password</label>{" "}
        <input id="password" name="password" type="password" autoComplete="current-password" required autoFocus />
      </p>
      {state.error && (
        <p role="alert" data-testid="login-error">
          {state.error}
        </p>
      )}
      <button type="submit" disabled={pending}>
        {pending ? "Logging in…" : "Log in"}
      </button>
    </form>
  );
}
