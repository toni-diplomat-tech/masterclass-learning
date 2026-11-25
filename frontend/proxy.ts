import { NextResponse, type NextRequest } from "next/server";
import { HOST_COOKIE, HOST_LOGIN_PATH } from "./lib/host-cookie";

// Optimistic check only: a /host page without a session cookie goes straight to the login page.
// Each Host page still verifies the session with the backend (requireHost / hostFetch).
export function proxy(request: NextRequest) {
  if (request.nextUrl.pathname === HOST_LOGIN_PATH || request.cookies.has(HOST_COOKIE)) {
    return NextResponse.next();
  }
  return NextResponse.redirect(new URL(HOST_LOGIN_PATH, request.url));
}

export const config = {
  matcher: ["/host", "/host/:path*"],
};
