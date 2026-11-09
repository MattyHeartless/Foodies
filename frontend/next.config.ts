import type { NextConfig } from 'next';
const config: NextConfig = {
  output: process.env.FOODIES_OUTPUT === 'standalone' ? 'standalone' : undefined,
  async redirects() {
    const origin = process.env.LOCAL_CANONICAL_ORIGIN ?? 'http://127.0.0.1:3002';
    return origin ? [{ source: '/:path*', has: [{ type: 'host', value: 'localhost' }], destination: `${origin.replace(/\/$/, '')}/:path*`, permanent: false }] : [];
  },
  async rewrites() { return [{ source: '/api/:path*', destination: `${(process.env.API_ORIGIN || 'http://127.0.0.1:5088').replace(/\/$/, '')}/api/:path*` }]; },
};
export default config;
