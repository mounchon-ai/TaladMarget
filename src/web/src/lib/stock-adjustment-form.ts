import type { StockAdjustmentReason } from "./products-api";

// UI-talad-014 ปรับสต็อก · UI-talad-012 zone adjustments — shared by the server action and the client, so it holds
// no server import.

/** ENT-003.reason — the list design declares (เลือกจากรายการ: รับของเข้า · ของเน่า/เสีย · นับสต็อกใหม่), in its order. */
export const REASONS: readonly { code: StockAdjustmentReason; label: string }[] = [
  { code: "RECEIVE", label: "รับของเข้า" },
  { code: "SPOILED", label: "ของเน่า/เสีย" },
  { code: "RECOUNT", label: "นับสต็อกใหม่" },
];

export function reasonLabel(code: string): string {
  return REASONS.find((r) => r.code === code)?.label ?? code;
}

/** "+10" · "−2" — the minus is U+2212, as AC-talad-076 · 078 write it. */
export function formatDelta(delta: number): string {
  return delta > 0 ? `+${delta.toLocaleString("th-TH")}` : `−${Math.abs(delta).toLocaleString("th-TH")}`;
}

/** What was typed, kept across a refusal (state "error" keeps the form as it was). */
export type AdjustValues = { reason: string; quantity: string; countedQty: string; note: string };
export const EMPTY_VALUES: AdjustValues = { reason: "", quantity: "", countedQty: "", note: "" };

/** A refusal sits under the field it names; `message` is for the whole form; `saved` closes the window. */
export type AdjustFormState =
  | { values: AdjustValues; errors: Partial<Record<"reason" | "quantity" | "countedQty", string>>; message?: string; saved?: boolean }
  | undefined;

export function readAdjustValues(formData: FormData): AdjustValues {
  const text = (name: string) => String(formData.get(name) ?? "");
  return { reason: text("reason"), quantity: text("quantity").trim(), countedQty: text("countedQty").trim(), note: text("note") };
}

/**
 * A number as typed, or null when empty or not a number — whether it is a whole number, not 0, not below zero is the
 * domain's to say. A typed "−" (U+2212) is read as a minus.
 */
export function numberOrNull(typed: string): number | null {
  const t = typed.replace(/−/g, "-").trim();
  if (t === "") return null;
  const n = Number(t);
  return Number.isFinite(n) ? n : null;
}

/**
 * BR-talad-041@v1 — the key a form mints when it opens. `crypto.getRandomValues`, not `randomUUID`: the shop's web
 * is served inside its LAN (DEC-001) and may be opened by plain http, where `randomUUID` does not exist (secure
 * contexts only).
 */
export function newRequestKey(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  return Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join("");
}
