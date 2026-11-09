'use client';
import { BrandMark } from '@/components/brand-mark';

import { usePathname } from 'next/navigation';
import { AppProvider, Header } from './core';
import { BrandIntro } from './brand-intro';

export function SiteShell({ children }: { children: React.ReactNode }) {
  const path = usePathname();
  if (path === '/plataforma' || path.startsWith('/plataforma/')) return <>{children}</>;
  return <><BrandIntro/><div data-app-shell><AppProvider><Header/>{children}<footer><a className="brand" href="/">foodies<span><BrandMark/></span></a><p>Menos espera. Más tiempo para disfrutar.</p><span>Hecho para tu hora de comer.</span></footer></AppProvider></div></>;
}
