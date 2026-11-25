// The backend's Host session cookie (HostSession.CookieName). The frontend relays it to the browser
// at login and forwards it on its server-side calls to Host endpoints. Kept apart from host-session.ts
// so the proxy can import it without pulling in next/headers.
export const HOST_COOKIE = "mq_host";

export const HOST_LOGIN_PATH = "/host/login";
