import type { Metadata, Viewport } from "next";
import { JetBrains_Mono } from "next/font/google";
import { AppHeader } from "@/components/app-header";
import { THEME_SCRIPT } from "@/lib/theme";
import "./globals.scss";

// Codes, timers and scores; served from this site's origin. Text uses the system font stack.
const mono = JetBrains_Mono({ subsets: ["latin"], variable: "--font-mono", display: "swap" });

export const metadata: Metadata = {
  title: "Masterclass Quiz",
  description: "Live quiz for a Masterclass",
};

export const viewport: Viewport = {
  themeColor: [
    { media: "(prefers-color-scheme: dark)", color: "#0b0b0d" },
    { media: "(prefers-color-scheme: light)", color: "#f3f2ee" },
  ],
};

// The theme script sets data-theme before first paint (no flash), so <html> may differ from the server's.
export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en" className={mono.variable} suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: THEME_SCRIPT }} />
      </head>
      <body>
        <AppHeader />
        {children}
      </body>
    </html>
  );
}
