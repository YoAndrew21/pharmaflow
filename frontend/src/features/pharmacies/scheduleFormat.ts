import type { PharmacyBranch, Weekday } from '../../domain/models'

export const weekdays: readonly Weekday[] = [
  'monday',
  'tuesday',
  'wednesday',
  'thursday',
  'friday',
  'saturday',
  'sunday',
]

/** Format local wall times against fixed UTC anchors, never the current clock. */
export function formatWeeklyHours(
  hours: PharmacyBranch['openingHours'],
  locale: string,
) {
  const dayFormat = new Intl.DateTimeFormat(locale, {
    weekday: 'long',
    timeZone: 'UTC',
  })
  const timeFormat = new Intl.DateTimeFormat(locale, {
    hour: 'numeric',
    minute: '2-digit',
    timeZone: 'UTC',
  })
  const formatTime = (value: string) => {
    const [hour, minute] = value.split(':').map(Number)
    return timeFormat.format(new Date(Date.UTC(2026, 7, 31, hour, minute)))
  }
  return weekdays.map((day, index) => ({
    day,
    label: dayFormat.format(new Date(Date.UTC(2026, 7, 31 + index))),
    intervals: hours[day].map((interval) => ({
      ...interval,
      label: `${formatTime(interval.opensAt)} – ${formatTime(interval.closesAt)}`,
    })),
  }))
}
