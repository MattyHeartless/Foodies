'use client';
import { useState } from 'react';
import { Utensils } from 'lucide-react';

export function StoreCover({ src, alt }: { src?: string | null; alt: string }) {
  const [failed, setFailed] = useState<string | null>(null);
  const managed = src && /^\/api\/v1\/media\/stores\/[a-f0-9]{32}\/[a-f0-9]{32}\.(jpg|png|webp)$/.test(src);
  if (!src || (!src.toLowerCase().startsWith('https://') && !managed) || src === failed) return <div className="store-cover-placeholder" role="img" aria-label={alt}><Utensils size={36} aria-hidden="true"/><span>{src ? 'Imagen no disponible' : 'Sin portada'}</span></div>;
  return <img src={src} alt={alt} loading="lazy" decoding="async" referrerPolicy="no-referrer" onError={() => setFailed(src)}/>;
}
