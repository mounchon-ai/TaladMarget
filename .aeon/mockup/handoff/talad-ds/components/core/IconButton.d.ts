/** A square icon-only button (toolbar actions, table row actions). Wrap a Lucide <i data-lucide> or SVG child. */
export interface IconButtonProps {
  children: React.ReactNode;
  /** Accessible label, also shown as title tooltip */
  label: string;
  onClick?: () => void;
  active?: boolean;
}
