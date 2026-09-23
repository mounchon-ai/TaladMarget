/** Toggle switch for binary settings (auto-approve minor exceptions, email notifications). */
export interface SwitchProps {
  checked: boolean;
  onChange?: (next: boolean) => void;
  label?: string;
}
