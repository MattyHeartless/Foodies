'use client';
import { BrandMark } from '@/components/brand-mark';

import { useEffect, useRef, useState } from 'react';

// Root-layout lifetime: runs on a full load, never on client-side navigation.
export function BrandIntro() {
  const [visible, setVisible] = useState(true);
  const overlay = useRef<HTMLDivElement>(null);
  const word = useRef<HTMLSpanElement>(null);
  const symbolRef = useRef<HTMLSpanElement>(null);

  useEffect(() => {
    const surface = overlay.current;
    const lettering = word.current;
    const symbol = symbolRef.current;
    const shell = document.querySelector<HTMLElement>('[data-app-shell]');
    const media = window.matchMedia('(prefers-reduced-motion: reduce)');
    if (!surface || !lettering || !symbol || media.matches || !symbol.animate) {
      setVisible(false);
      return;
    }

    const animations: Animation[] = [];
    const timers: ReturnType<typeof setTimeout>[] = [];
    let stopped = false;
    const previousOverflow = document.body.style.overflow;
    const previousInert = shell?.inert ?? false;
    document.body.style.overflow = 'hidden';
    if (shell) shell.inert = true;
    const release = () => {
      document.body.style.overflow = previousOverflow;
      if (shell) shell.inert = previousInert;
    };
    const finish = () => {
      if (stopped) return;
      stopped = true;
      animations.forEach(animation => animation.cancel());
      timers.forEach(clearTimeout);
      release();
      setVisible(false);
    };
    const wait = (ms: number) => new Promise<void>(resolve => timers.push(setTimeout(resolve, ms)));
    const tokens = getComputedStyle(document.documentElement);
    const duration = (name: string, fallback: number) => {
      const value = tokens.getPropertyValue(name).trim();
      const parsed = parseFloat(value);
      return Number.isFinite(parsed) ? parsed * (value.endsWith('ms') ? 1 : 1000) : fallback;
    };
    const ease = tokens.getPropertyValue('--ease-smooth-out').trim() || 'cubic-bezier(0.22, 1, 0.36, 1)';
    const play = async (element: HTMLElement, frames: Keyframe[], ms: number, easing = ease) => {
      if (stopped) return;
      const animation = element.animate(frames, { duration: ms, easing, fill: 'forwards' });
      animations.push(animation);
      await animation.finished;
    };
    const onKey = (event: KeyboardEvent) => { if (event.key === 'Escape') finish(); };
    document.addEventListener('keydown', onKey);
    media.addEventListener('change', finish);
    surface.addEventListener('click', finish);
    // Recover even if font loading or an animation is interrupted unexpectedly.
    timers.push(setTimeout(finish, 4000));
    void (async () => {
      await Promise.race([document.fonts?.ready, wait(450)]);
      if (stopped) return;
      const distance = symbol.getBoundingClientRect().left - lettering.getBoundingClientRect().left;
      const start = `translate(${-distance}px, calc(-50dvh - 100px))`;
      const landed = `translate(${-distance}px, 0)`;
      surface.dataset.phase = 'fall';
      await play(symbol, [{ transform: start }, { transform: landed }], duration('--duration-very-slow', 500), 'cubic-bezier(0.55, 0.055, 0.675, 0.19)');
      if (stopped) return;
      surface.dataset.phase = 'travel';
      await play(symbol, [{ transform: landed }, { transform: 'translate(0, 0)' }], duration('--duration-slow', 400));
      if (stopped) return;
      surface.dataset.phase = 'reveal';
      await play(lettering, [{ opacity: 0, transform: 'translateY(var(--distance-medium))', filter: 'blur(var(--blur-medium))' }, { opacity: 1, transform: 'translateY(0)', filter: 'blur(0)' }], duration('--duration-very-slow', 500));
      await wait(duration('--duration-quick', 150));
      if (stopped) return;
      surface.dataset.phase = 'exit';
      await play(surface, [{ opacity: 1 }, { opacity: 0 }], duration('--duration-fast', 250));
      finish();
    })().catch(finish);

    return () => {
      stopped = true;
      animations.forEach(animation => animation.cancel());
      timers.forEach(clearTimeout);
      release();
      document.removeEventListener('keydown', onKey);
      media.removeEventListener('change', finish);
      surface.removeEventListener('click', finish);
    };
  }, []);

  if (!visible) return null;
  return <div ref={overlay} className="brand-intro" data-phase="prepare" aria-hidden="true">
    <div className="brand-intro-mark"><span ref={word} className="brand-intro-word">foodies</span><span ref={symbolRef} className="brand-intro-symbol"><BrandMark/></span></div>
  </div>;
}
