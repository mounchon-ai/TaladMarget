/** Small rectangular label for metadata (loan product type, channel, tags) — not for lifecycle state, use StatusBadge for that. */
export interface TagProps {
  children: React.ReactNode;
  color?: 'gray' | 'blue' | 'green';
}
