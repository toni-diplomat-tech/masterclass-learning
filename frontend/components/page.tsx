import type { ReactNode } from "react";
import styles from "./page.module.scss";

export type PageWidth = "phone" | "text" | "wide" | "projector";

/** The <main> of a screen: its width (phone column, reading text, back office, projector) and padding. */
export function Page({ width, center = false, children }: { width: PageWidth; center?: boolean; children: ReactNode }) {
  return <main className={[styles.page, styles[width], center && styles.center].filter(Boolean).join(" ")}>{children}</main>;
}

/** A row of meta chips (Workshop · Question 2 of 5 · Medium). */
export function Chips({ items, testId }: { items: ReactNode[]; testId?: string }) {
  return (
    <p className={styles.chips} data-testid={testId}>
      {items.map((item, index) => (
        <span key={index} className={styles.chip}>
          {item}
        </span>
      ))}
    </p>
  );
}

/** A small muted label above a heading ("You are", the Course title). */
export function Eyebrow({ children }: { children: ReactNode }) {
  return <p className={styles.eyebrow}>{children}</p>;
}

/** A vertical stack with even gaps. */
export function Stack({ gap = "md", children }: { gap?: "sm" | "md" | "lg"; children: ReactNode }) {
  return <div className={`${styles.stack} ${styles[gap]}`}>{children}</div>;
}
