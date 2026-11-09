import type { NextConfig } from 'next';
const config: NextConfig = {
  async redirects() { return [{ source: '/:path*', has: [{ type: 'host', value: 'localhost' }], destination: 'http://127.0.0.1:3002/:path*', permanent: false }]; },
  async rewrites() { return [{ source: '/api/:path*', destination: `${process.env.API_ORIGIN || 'http://127.0.0.1:5088'}/api/:path*` }]; },
};
export default config;
