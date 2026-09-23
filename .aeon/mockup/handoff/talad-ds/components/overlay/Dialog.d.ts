/** Modal dialog for confirmations that change loan state (approve, reject, early settlement). */
export interface DialogProps {
  open: boolean;
  title: string;
  children: React.ReactNode;
  onClose?: () => void;
  footer?: React.ReactNode;
}
