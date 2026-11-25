import type { Metadata } from "next";
import { LoginForm } from "./login-form";

export const metadata: Metadata = { title: "Host login · Masterclass Quiz" };

export default function HostLoginPage() {
  return (
    <main style={{ padding: "2rem" }}>
      <h1>Host login</h1>
      <LoginForm />
    </main>
  );
}
