import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  cacheComponents: true,
  partialPrefetching: true,
  output: "standalone",
  experimental: {
    // Course import files are uploaded through a Server Action; the default limit is 1 MB.
    serverActions: { bodySizeLimit: "5mb" },
  },
};

export default nextConfig;
