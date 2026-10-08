import {
  CheckCircle2,
  TriangleAlert,
  CircleMinus,
  CircleHelp,
} from 'lucide-react'
import type { AvailabilityStatus } from '../../domain/models'
import styles from './AvailabilityBadge.module.css'

export function AvailabilityBadge({
  status,
  text,
}: {
  status: AvailabilityStatus | 'unknown'
  text: string
}) {
  const icons = {
    available: CheckCircle2,
    'low-stock': TriangleAlert,
    unavailable: CircleMinus,
    unknown: CircleHelp,
  }
  const Icon = icons[status]
  return (
    <span className={styles.badge} data-status={status}>
      <Icon size={16} aria-hidden="true" />
      <span>{text}</span>
    </span>
  )
}
