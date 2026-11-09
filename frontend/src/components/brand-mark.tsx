import mark from '@/lib/brand-mark.json';

export function BrandMark({ className = '' }: { className?: string }) {
  return <svg className={`brand-mark ${className}`} viewBox={mark.viewBox} xmlns="http://www.w3.org/2000/svg" fill="currentColor" aria-hidden="true" focusable="false"><path d={mark.path}/></svg>;
}
