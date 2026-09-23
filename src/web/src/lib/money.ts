const baht = new Intl.NumberFormat("th-TH", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/** ฿45.00 — the way mock's pages print an amount. */
export const formatBaht = (amount: number) => `฿${baht.format(amount)}`;
