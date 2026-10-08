import { describe, expect, it } from 'vitest'
import { demoDataset, localCatalogReader } from '../../data/localCatalogReader'
import { formatWeeklyHours, weekdays } from './scheduleFormat'

describe('branch reads and weekly hours', () => {
  it('resolves all three canonical branches asynchronously without copies', async () => {
    const read = localCatalogReader.listBranches()
    expect(read).toBeInstanceOf(Promise)
    expect(await read).toBe(demoDataset.branches)
    expect(await read).toHaveLength(3)
  })
  it.each(['aster-commons', 'willow-arcade', 'lantern-court'])(
    'resolves the exact branch for %s',
    async (slug) => {
      expect(await localCatalogReader.getBranch(slug)).toBe(
        demoDataset.branches.find((branch) => branch.slug === slug),
      )
    },
  )
  it('returns null for an unknown branch', async () => {
    expect(await localCatalogReader.getBranch('unknown')).toBeNull()
  })
  it('formats every weekday in order and preserves closed days', () => {
    const result = formatWeeklyHours(
      demoDataset.branches[0]!.openingHours,
      'en',
    )
    expect(result.map((day) => day.day)).toEqual(weekdays)
    expect(result[0]?.label).toBe('Monday')
    expect(result[0]?.intervals[0]?.label).toBe('9:00 AM – 5:00 PM')
    expect(result[5]?.intervals[0]?.label).toBe('10:00 AM – 1:00 PM')
    expect(result[6]?.intervals).toEqual([])
  })
  it('supports split intervals without mutating the schedule', () => {
    const hours = {
      ...demoDataset.branches[0]!.openingHours,
      monday: [
        { opensAt: '09:00', closesAt: '12:00' },
        { opensAt: '14:00', closesAt: '17:00' },
      ],
    }
    expect(
      formatWeeklyHours(hours, 'en')[0]?.intervals.map(
        (interval) => interval.label,
      ),
    ).toEqual(['9:00 AM – 12:00 PM', '2:00 PM – 5:00 PM'])
    expect(hours.monday).toHaveLength(2)
    expect(demoDataset.branches[0]!.openingHours.monday).toHaveLength(1)
  })
  it('uses locale formatting without the current date or system timezone', () => {
    const result = formatWeeklyHours(
      demoDataset.branches[0]!.openingHours,
      'en-GB',
    )
    expect(result[0]?.intervals[0]?.label).toBe('9:00 – 17:00')
    expect(result).toEqual(
      formatWeeklyHours(demoDataset.branches[0]!.openingHours, 'en-GB'),
    )
  })
})
