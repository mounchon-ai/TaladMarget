/** Transient notification for background actions (payment recorded, schedule regenerated). */
export interface ToastProps {
  children: React.ReactNode;
  variant?: 'info' | 'success' | 'danger';
  onClose?: () => void;
}
