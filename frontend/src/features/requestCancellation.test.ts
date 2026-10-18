import { renderHook, waitFor } from '@testing-library/react'
import { expect, it, vi } from 'vitest'
import { localCatalogReader } from '../data/localCatalogReader'
import { useMedicationDetail } from './medication/useMedicationDetail'
import { useBranchDetail, useBranchDirectory } from './pharmacies/useBranches'

it('shares one medication lifecycle signal across its dependent reads and aborts on cleanup', async () => {
  const reader = {
    ...localCatalogReader,
    getMedication: vi.fn(localCatalogReader.getMedication),
    listBranches: vi.fn(localCatalogReader.listBranches),
    getAvailability: vi.fn(localCatalogReader.getAvailability),
  }
  const { result, unmount } = renderHook(() =>
    useMedicationDetail(reader, 'paracetamol-500-tablets', 0),
  )
  await waitFor(() => expect(result.current.status).toBe('success'))
  const signal = reader.getMedication.mock.calls[0]![1]!
  expect(reader.listBranches).toHaveBeenCalledWith(signal)
  expect(reader.getAvailability).toHaveBeenCalledWith('med-par-500', signal)
  expect(signal.aborted).toBe(false)
  unmount()
  expect(signal.aborted).toBe(true)
})

it('gives independent branch consumers separate cancellation scopes', async () => {
  const reader = {
    ...localCatalogReader,
    listBranches: vi.fn(localCatalogReader.listBranches),
    getBranch: vi.fn(localCatalogReader.getBranch),
  }
  const directory = renderHook(() => useBranchDirectory(reader, 0))
  const detail = renderHook(() => useBranchDetail(reader, 'aster-commons', 0))
  await waitFor(() => expect(directory.result.current.status).toBe('success'))
  await waitFor(() => expect(detail.result.current.status).toBe('success'))
  const directorySignal = reader.listBranches.mock.calls[0]![0]!
  const detailSignal = reader.getBranch.mock.calls[0]![1]!
  expect(directorySignal).not.toBe(detailSignal)
  directory.unmount()
  expect(directorySignal.aborted).toBe(true)
  expect(detailSignal.aborted).toBe(false)
  detail.unmount()
  expect(detailSignal.aborted).toBe(true)
})
