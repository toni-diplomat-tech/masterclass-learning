import { NextResponse, type NextRequest } from "next/server";
import { HOST_COOKIE, HOST_LOGIN_PATH } from "./lib/host-cookie";
import { inMasterclassOpen, PARTICIPANT_COOKIE, participantByToken } from "./lib/participant-cookie";

export function proxy(request: NextRequest) {
  return request.nextUrl.pathname === "/" ? homepage(request) : hostPage(request);
}

// Optimistic check only: a /host page without a session cookie goes straight to the login page.
// Each Host page still verifies the session with the backend (requireHost / hostFetch).
function hostPage(request: NextRequest) {
  if (request.nextUrl.pathname === HOST_LOGIN_PATH || request.cookies.has(HOST_COOKIE)) {
    return NextResponse.next();
  }
  return NextResponse.redirect(new URL(HOST_LOGIN_PATH, request.url));
}

// A phone that has already joined a Masterclass Open goes back to /play (QR link `/?code=…` too),
// redirected before `/` renders the Join code form. Only a request with a Participant cookie asks the backend.
async function homepage(request: NextRequest) {
  const token = request.cookies.get(PARTICIPANT_COOKIE)?.value;
  if (!token) return NextResponse.next();
  try {
    const participant = await participantByToken(token);
    if (participant && inMasterclassOpen(participant)) return NextResponse.redirect(new URL("/play", request.url));
  } catch (error) {
    // An unreachable backend still shows the form, and joining then reports it; any other failure is a bug.
    if (!isBackendUnreachable(error)) throw error;
  }
  return NextResponse.next();
}

/** A fetch that failed to reach the backend (network error or timeout), as opposed to one it answered. */
function isBackendUnreachable(error: unknown): boolean {
  return error instanceof TypeError || (error instanceof DOMException && error.name === "TimeoutError");
}

export const config = {
  matcher: [
    "/host",
    "/host/:path*",
    // Must stay a literal (read at build time): PARTICIPANT_COOKIE.
    { source: "/", has: [{ type: "cookie", key: "mq_participant" }] },
  ],
};
