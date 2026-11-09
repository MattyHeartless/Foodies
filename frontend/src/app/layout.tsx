import type { Metadata } from 'next';
import './globals.css';
import './motion-tokens.css';
import './motion-recipes.css';
import './motion.css';
import './store-hours.css';
import './brand-intro.css';
import { SiteShell } from '@/components/site-shell';
export const metadata: Metadata = { title: 'Foodies · Tu comida, a tu tiempo', description: 'Elige tu tienda favorita, pide con anticipación y recoge tu comida cuando esté lista.' };
export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="es"><body><SiteShell>{children}</SiteShell></body></html>;
}
