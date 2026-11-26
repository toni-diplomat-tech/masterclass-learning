import type { HTMLAttributes } from "react";
import styles from "./card.module.scss";

/** A raised surface. `glass` makes it frosted (sheets over content); `padded={false}` for tables. */
export function Card({
  glass = false,
  padded = true,
  as: Tag = "section",
  className,
  ...props
}: HTMLAttributes<HTMLElement> & { glass?: boolean; padded?: boolean; as?: "section" | "article" | "div" }) {
  const classes = [styles.card, glass && styles.glass, padded && styles.padded, className].filter(Boolean).join(" ");
  return <Tag {...props} className={classes} />;
}
