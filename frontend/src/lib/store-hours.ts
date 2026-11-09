import type { Hour } from './api';

export const WEEK_DAYS = ['Domingo', 'Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado'];
export const WEEK_ORDER = [1, 2, 3, 4, 5, 6, 0];

export function storeWeekday(timeZone: string, now: Date): number | null {
  try {
    const day = new Intl.DateTimeFormat('en-US', { timeZone, weekday: 'short' }).format(now);
    const index = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'].indexOf(day);
    return index >= 0 ? index : null;
  } catch { return null; }
}

export function dayIntervals(hours: Hour[], day: number): string[] {
  return hours.filter(h => h.dayOfWeek === day && !h.isClosed)
    .sort((a, b) => a.openingTime.localeCompare(b.openingTime))
    .map(h => {
      const opening = h.openingTime.slice(0, 5), closing = h.closingTime.slice(0, 5);
      return `${opening} – ${closing}${closing < opening ? ' (+1 día)' : ''}`;
    });
}

export function localTimeLabel(timeZone: string, now: Date): string {
  try {
    const offset = new Intl.DateTimeFormat('es-MX', { timeZone, timeZoneName: 'shortOffset' })
      .formatToParts(now).find(part => part.type === 'timeZoneName')?.value;
    return offset ? `Hora local de la tienda · ${offset}` : 'Hora local de la tienda';
  } catch { return 'Hora local de la tienda'; }
}
