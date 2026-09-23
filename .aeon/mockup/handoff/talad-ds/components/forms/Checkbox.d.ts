/** Single checkbox with label (consent, document checklist items). */
export interface CheckboxProps {
  label: string;
  checked?: boolean;
  onChange?: (e: React.ChangeEvent<HTMLInputElement>) => void;
}
