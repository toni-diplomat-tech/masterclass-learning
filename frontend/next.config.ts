import type { NextConfig } from "next";
import { backendUrl } from "./lib/backend";

const nextConfig: NextConfig = {
  cacheComponents: true,
  partialPrefetching: true,
  output: "standalone",
  experimental: {
    // Course import files are uploaded through a Server Action; the default limit is 1 MB.
    serverActions: { bodySizeLimit: "5mb" },
  },
  // The browser reaches the backend's SignalR hub on this site's origin, so its cookies go along.
  // Next proxies it (WebSockets included) to the backend. Rewrites are fixed at build time, so the
  // Docker build sets BACKEND_URL. In production Caddy sends /hubs/* to the backend directly.
  async rewrites() {
    return [{ source: "/hubs/:path*", destination: `${backendUrl()}/hubs/:path*` }];
  },
};

export default nextConfig;
