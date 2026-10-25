/** Utilitários de data em horário LOCAL (evita o deslocamento de UTC do toISOString). */

export function toLocalISODate(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

/** Interpreta "YYYY-MM-DD" como data local (meia-noite local). */
export function parseLocalISODate(iso: string): Date {
  const [y, m, d] = iso.split("-").map(Number);
  return new Date(y, m - 1, d);
}

export function addDaysISO(iso: string, days: number): string {
  const d = parseLocalISODate(iso);
  d.setDate(d.getDate() + days);
  return toLocalISODate(d);
}

/** Segunda-feira da semana que contém a data informada. */
export function mondayOfISO(iso: string): string {
  const d = parseLocalISODate(iso);
  const diff = (d.getDay() + 6) % 7;
  d.setDate(d.getDate() - diff);
  return toLocalISODate(d);
}

export function todayISO(): string {
  return toLocalISODate(new Date());
}

export function currentMondayISO(): string {
  return mondayOfISO(todayISO());
}

export function nextMondayISO(): string {
  return addDaysISO(currentMondayISO(), 7);
}

