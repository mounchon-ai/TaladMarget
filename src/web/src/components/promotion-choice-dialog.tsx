"use client";

import { useEffect, useRef } from "react";
import { formatBaht } from "@/lib/money";
import type { PromotionChoice } from "@/lib/sales-api";

type Props = {
  /** API-009's `needsChoice` — at least two promotions that give exactly the same (BR-talad-029@v1). */
  choices: PromotionChoice[];
  open: boolean;
  /** state "loading" — the choose buttons are off while the sale is being paid. */
  pending?: boolean;
  onChoose: (promotionId: number) => void;
  onCancel: () => void;
};

/**
 * UI-talad-003 เลือกโปรโมชั่นที่ลดเท่ากัน (UC-talad-004 · FE-talad-032) — a window over the sales screen (kind modal);
 * the wireframe MCK-talad-003 is the only picture and every data-testid is its. It lists each tied promotion with the
 * baht it gives (AC-talad-127); เลือกโปรนี้ hands the promotion to whoever opened it — ชำระเงิน, which pays with that
 * choice, is FE-talad-034's, and so is opening it. ยกเลิก closes it and leaves the cart as it was. Each option row
 * carries data-row-key = the promotion's id (gate 126); a long list scrolls inside the window (state "overflow").
 */
export function PromotionChoiceDialog({ choices, open, pending = false, onChoose, onCancel }: Props) {
  const dialog = useRef<HTMLDialogElement>(null);

  // the <dialog> element is the one thing outside React this keeps in step with `open`
  useEffect(() => {
    const d = dialog.current;
    if (!d) return;
    if (open && !d.open) d.showModal();
    if (!open && d.open) d.close();
  }, [open]);

  return (
    <dialog ref={dialog} className="modal" aria-labelledby="promotion-choice-title" onCancel={onCancel}>
      <div className="modal-box">
        <h2 id="promotion-choice-title">เลือกโปรโมชั่นที่ลดเท่ากัน</h2>
        <p>มีโปรโมชั่นที่ลดเท่ากัน กรุณาเลือกโปรโมชั่น</p>
        <div className="tablewrap choices">
          <table>
            <thead>
              <tr>
                <th>ชื่อโปร</th>
                <th className="num">ส่วนลดของโปร (บาท)</th>
                <th aria-label="เลือก" />
              </tr>
            </thead>
            <tbody>
              {choices.map((c) => (
                <tr key={c.id} data-row-key={c.id}>
                  <td data-testid="ui-talad-003-ent-006-name">{c.name}</td>
                  <td className="num" data-testid="ui-talad-003-promo-discount">
                    −{formatBaht(c.discount)}
                  </td>
                  <td>
                    <button type="button" className="btn btn-primary btn-sm" disabled={pending} onClick={() => onChoose(c.id)} data-testid="ui-talad-003-choose-promo">
                      {pending ? <span className="loading loading-spinner loading-sm" aria-hidden="true" /> : null}
                      เลือกโปรนี้
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="modal-action">
          <button type="button" className="btn" disabled={pending} onClick={onCancel} data-testid="ui-talad-003-cancel">
            ยกเลิก
          </button>
        </div>
      </div>
    </dialog>
  );
}
