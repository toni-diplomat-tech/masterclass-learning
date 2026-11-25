// Server-side only: inside Docker the backend is reached on the compose network.
export const backendUrl = () => process.env.BACKEND_URL ?? "http://localhost:5088";
