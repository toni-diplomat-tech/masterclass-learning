import type { HTMLAttributes } from "react";
import styles from "./notice.module.scss";

export type NoticeTone = "info" | "success" | "error";

/**
 * Status or alert text with a coloured edge: `error` for problems (red), `success` for done (green),
 * `info` for everything else. Keep the role (`alert`/`status`) on it where the screen reader needs it.
 */
export function Notice({ tone = "info", className, ...props }: HTMLAttributes<HTMLElement> & { tone?: NoticeTone }) {
  return <div {...props} className={[styles.notice, styles[tone], className].filter(Boolean).join(" ")} />;
}
