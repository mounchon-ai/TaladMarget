/** Underline tab switcher for a set of related views (e.g. application detail: Summary / Documents / Schedule). */
export interface TabsProps {
  tabs: { value: string; label: string }[];
  active: string;
  onChange?: (value: string) => void;
}
