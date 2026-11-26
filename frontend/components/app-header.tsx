import Link from "next/link";
import { ThemeToggle } from "./theme-toggle";
import styles from "./app-header.module.scss";

/** The frosted top bar on every screen: the wordmark (home) and the theme toggle. */
export function AppHeader() {
  return (
    <header className={styles.header}>
      <Link href="/" className={styles.brand} aria-label="Masterclass Quiz, home">
        <span aria-hidden="true" className={styles.prompt}>
          &gt;
        </span>
        masterclass<span className={styles.dot}>.</span>quiz
        <span aria-hidden="true" className={styles.cursor} />
      </Link>
      <ThemeToggle />
    </header>
  );
}
