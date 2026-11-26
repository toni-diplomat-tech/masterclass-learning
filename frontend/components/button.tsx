import type { ButtonHTMLAttributes } from "react";
import styles from "./button.module.scss";

export type ButtonVariant = "primary" | "secondary" | "danger" | "ghost";
export type ButtonSize = "md" | "lg";

/** The class names of a pill button, for a link that looks like one. */
export function buttonClass({ variant = "primary", size = "md", block = false }: { variant?: ButtonVariant; size?: ButtonSize; block?: boolean } = {}) {
  return [styles.button, styles[variant], styles[size], block && styles.block].filter(Boolean).join(" ");
}

/**
 * The pill button. `loading` disables it and shows a spinner next to the label (pass the "…ing" label).
 * `block` makes it full width, as on the phone.
 */
export function Button({
  variant,
  size,
  block,
  loading = false,
  disabled,
  children,
  type = "button",
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: ButtonVariant; size?: ButtonSize; block?: boolean; loading?: boolean }) {
  return (
    <button {...props} type={type} disabled={disabled || loading} aria-busy={loading || undefined} className={buttonClass({ variant, size, block })}>
      {loading && <span className={styles.spinner} aria-hidden="true" />}
      {children}
    </button>
  );
}
