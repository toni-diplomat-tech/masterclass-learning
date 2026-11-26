import type { Metadata } from "next";
import { Card } from "@/components/card";
import { Page } from "@/components/page";
import styles from "../host.module.scss";
import { LoginForm } from "./login-form";

export const metadata: Metadata = { title: "Host login · Masterclass Quiz" };

export default function HostLoginPage() {
  return (
    <Page width="phone">
      <Card className={styles.login}>
        <h1>Host login</h1>
        <LoginForm />
      </Card>
    </Page>
  );
}
