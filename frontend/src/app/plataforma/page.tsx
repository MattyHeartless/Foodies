import type { Metadata } from 'next';
import { PlatformConsole } from '@/components/platform-console';

export const metadata: Metadata = { title: 'Foodies · Administración de plataforma', robots: { index: false, follow: false } };
export default function Page() { return <PlatformConsole/>; }
