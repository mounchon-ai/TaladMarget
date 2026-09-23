/** Data table with tabular-numeral columns — the amortization schedule, payment history, application queue. */
export interface TableColumn {
  key: string;
  label: string;
  align?: 'left' | 'right';
  /** applies font-variant-numeric: tabular-nums */
  numeric?: boolean;
}
export interface TableProps {
  columns: TableColumn[];
  rows: Record<string, React.ReactNode>[];
}
