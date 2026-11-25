import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Masterclass Quiz",
  description: "Live quiz for a Masterclass",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
