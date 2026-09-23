/** Labeled text input with optional error message and unit suffix (used for loan amount, income, dates). */
export interface InputProps {
  label?: string;
  placeholder?: string;
  value?: string;
  onChange?: (e: React.ChangeEvent<HTMLInputElement>) => void;
  error?: string;
  type?: string;
  /** e.g. "THB" or "months" */
  suffix?: string;
}
