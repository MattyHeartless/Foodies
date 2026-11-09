'use client';

import { useEffect, useId, useState } from 'react';
import { ChevronDown, Clock3 } from 'lucide-react';
import type { Store } from '@/lib/api';
import { dayIntervals, storeWeekday, WEEK_DAYS, WEEK_ORDER } from '@/lib/store-hours';

type HoursStore = Pick<Store, 'hours' | 'timeZoneId' | 'operationalState'>;

export function StoreHours({ store, referenceTime }: { store: HoursStore; referenceTime?: Date }) {
  const heading = useId();
  const panel = useId();
  const [expanded, setExpanded] = useState(false);
  const [clock, setClock] = useState(() => referenceTime || new Date());
  useEffect(() => {
    if (referenceTime) return;
    const timer = setInterval(() => setClock(new Date()), 60000);
    return () => clearInterval(timer);
  }, [referenceTime]);
  const now = referenceTime || clock;
  const today = storeWeekday(store.timeZoneId, now);
  const intervals = today === null ? [] : dayIntervals(store.hours, today);
  const published = store.hours.length > 0;
  const state = store.operationalState === 'Abierta' ? 'Abierto' : store.operationalState === 'Pausada' ? 'En pausa' : 'Cerrado';
  const sameEveryDay = published && intervals.length > 0 && WEEK_ORDER.every(day => JSON.stringify(dayIntervals(store.hours, day)) === JSON.stringify(intervals));
  const overview = <>
    <Clock3 className="store-hours-clock" size={21} aria-hidden="true"/>
    <span className="store-hours-overview">
      <span className="store-hours-heading"><span className="store-hours-title" id={heading} role="heading" aria-level={2}>Horario</span>
        <span className={`store-hours-state ${store.operationalState === 'Abierta' ? 'is-open' : ''}`}><span aria-hidden="true"/>{state}</span>
      </span>
      <span className="store-hours-today">
        <span className="store-hours-time">
          {!published ? <span>Por confirmar</span> : today === null ? <span>Ver horarios</span> : intervals.length ? intervals.map(interval => <span key={interval}>{interval.replace(' – ', '–')}</span>) : <span>Cerrado hoy</span>}
        </span>
        {published && today !== null && <span className="store-hours-day">{sameEveryDay ? 'Lun–Dom' : 'Hoy'}</span>}
      </span>
    </span>
  </>;

  return <section className="store-hours" aria-labelledby={heading}>
    {published ? <div className="store-hours-week t-acc" data-open={expanded}>
      <button type="button" className="store-hours-summary t-acc-head" aria-expanded={expanded} aria-controls={panel} onClick={() => setExpanded(value => !value)}>{overview}<ChevronDown className="store-hours-chevron t-acc-chevron" size={18} aria-hidden="true"/></button>
      <div id={panel} className="t-acc-panel" inert={!expanded} aria-hidden={!expanded}><div className="store-hours-week-body t-acc-panel-inner"><dl>
        {WEEK_ORDER.map(day => {
          const times = dayIntervals(store.hours, day);
          return <div className={`store-hours-row ${day === today ? 'is-today' : ''}`} key={day}>
            <dt>{WEEK_DAYS[day]}{day === today && <span className="store-hours-today-tag">Hoy</span>}</dt>
            <dd>{times.length ? times.map(time => <span key={time}>{time}</span>) : <span className="store-hours-closed">Cerrado</span>}</dd>
          </div>;
        })}
      </dl></div></div>
    </div> : <div className="store-hours-summary">{overview}</div>}
  </section>;
}
