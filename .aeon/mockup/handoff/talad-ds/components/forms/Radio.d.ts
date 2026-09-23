/** Single radio option, group by shared `name` (repayment method, disbursement channel). */
export interface RadioProps {
  label: string;
  name: string;
  value: string;
  checked?: boolean;
  onChange?: (e: React.ChangeEvent<HTMLInputElement>) => void;
}
