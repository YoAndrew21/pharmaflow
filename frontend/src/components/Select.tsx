import { Check, ChevronDown } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import {
  Button,
  I18nProvider,
  Label,
  ListBox,
  ListBoxItem,
  Popover,
  Select as AriaSelect,
  SelectValue,
} from 'react-aria-components'
import styles from './Select.module.css'

export interface SelectOption {
  readonly value: string
  readonly label: string
  readonly lang?: string
}

interface SelectProps {
  readonly label: string
  readonly value: string
  readonly options: readonly SelectOption[]
  readonly onChange: (value: string) => void
  readonly name?: string
  readonly disabled?: boolean
  readonly variant?: 'catalog' | 'compact'
}

/** Original PharmaFlow presentation; React Aria owns interaction and overlays. */
export function Select({
  label,
  value,
  options,
  onChange,
  name,
  disabled = false,
  variant = 'catalog',
}: SelectProps) {
  const { i18n } = useTranslation()
  return (
    <I18nProvider locale={i18n.resolvedLanguage ?? 'en'}>
      <AriaSelect
        className={`${styles.control} ${variant === 'compact' ? styles.compact : ''}`}
        name={name}
        value={value}
        onChange={(next) => {
          if (typeof next === 'string') onChange(next)
        }}
        isDisabled={disabled}
      >
        <Label className={styles.label}>{label}</Label>
        <Button className={styles.trigger} type="button">
          <SelectValue className={styles.value} />
          <ChevronDown
            className={styles.chevron}
            size={17}
            aria-hidden="true"
          />
        </Button>
        <Popover
          className={`${styles.popover} ${variant === 'compact' ? styles.compactPanel : ''}`}
          placement="bottom start"
          shouldFlip
          containerPadding={12}
          offset={6}
          maxHeight={352}
        >
          <ListBox className={styles.list} items={options}>
            {(option) => (
              <ListBoxItem
                id={option.value}
                textValue={option.label}
                className={styles.option}
              >
                {({ isSelected }) => (
                  <>
                    <span lang={option.lang}>{option.label}</span>
                    {isSelected && <Check size={17} aria-hidden="true" />}
                  </>
                )}
              </ListBoxItem>
            )}
          </ListBox>
        </Popover>
      </AriaSelect>
    </I18nProvider>
  )
}
