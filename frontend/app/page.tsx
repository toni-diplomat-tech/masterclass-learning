import Link from "next/link";

export default function Home() {
  return (
    <main style={{ padding: "2rem" }}>
      <h1>Masterclass Quiz</h1>
      <p>
        <Link href="/health">System health</Link>
      </p>
    </main>
  );
}
