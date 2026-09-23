/** A button for primary actions, secondary actions, and destructive actions (reject, close account). */
export interface ButtonProps {
  children: React.ReactNode;
  /** Visual style. primary = main action, secondary = outline, ghost = minimal, danger = destructive (reject/reverse) */
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger';
  size?: 'sm' | 'md' | 'lg';
  disabled?: boolean;
  onClick?: () => void;
  type?: 'button' | 'submit';
}
