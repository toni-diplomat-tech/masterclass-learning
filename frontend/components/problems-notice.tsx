import type { HTMLAttributes } from "react";
import { Notice } from "./notice";
import styles from "./problems-notice.module.scss";

/**
 * The reasons an action was refused, one per line, as an error {@link Notice} that screen readers announce.
 * Renders nothing when there are none.
 */
export function ProblemsNotice({ problems, ...props }: HTMLAttributes<HTMLElement> & { problems?: string[] }) {
  if (!problems?.length) return null;
  return (
    <Notice tone="error" role="alert" {...props}>
      <ul className={styles.problems}>
        {problems.map((problem, index) => (
          <li key={index}>{problem}</li>
        ))}
      </ul>
    </Notice>
  );
}
