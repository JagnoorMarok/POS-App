// API Base URL - empty string for relative paths (same origin / proxy), or custom URL for Vercel -> Tunnel / Remote backend
export const API_BASE = import.meta.env.VITE_API_URL || '';
