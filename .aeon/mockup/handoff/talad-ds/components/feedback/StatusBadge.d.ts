/** The single canonical status pill for LoanApplication / LoanAccount lifecycle states — use everywhere a state shows, so colors never drift between screens. */
export interface StatusBadgeProps {
  state: 'Draft' | 'Submitted' | 'UnderReview' | 'Approved' | 'Rejected' | 'Disbursed' | 'Active' | 'Closed';
}
