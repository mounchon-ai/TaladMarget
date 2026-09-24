"use client";

import { useRef, useState, useTransition } from "react";
import { PromotionChoiceDialog } from "@/components/promotion-choice-dialog";
import { formatBaht } from "@/lib/money";
import { OFFLINE } from "@/lib/offline";
import type { PaidSale, PromotionChoice } from "@/lib/sales-api";

type Payment = { ok: true; sale: PaidSale } | { ok: false; choices: PromotionChoice[] } | { ok: false; message: string };

type Props = {
  /** The cart the screen shows — what is paid, so a second press or a resend finds it paid (AC-talad-027 · 028). */
  cartId: number;
  empty: boolean;
  pay: (cartId: number, choice: number[]) => Promise<Payment>;
};

/**
 * UI-talad-002 action checkout ชำระเงิน (UC-talad-003 · FE-talad-034). The api prices the cart at the moment of pressing
 * (BR-talad-038@v1), so a tie is learned from its answer, not from the totals on screen: PROMOTION_CHOICE_NEEDED opens
 * UI-talad-003 with the tied promotions, เลือกโปรนี้ pays again with that choice added (one per tied round, in order),
 * ยกเลิก leaves the cart as it was. The button is off while the cart is empty (state "empty") and while a payment is on
 * its way (state "loading") — a ref, not only the transition, keeps two presses in one frame to one request. A refusal
 * shows the rule's sentence as the api worded it; a payment whose answer never came back says so (state "error"). Paid: the page reads the api's new empty cart; the
 * receipt full screen (RPT-talad-001) is FE-talad-036's, and until it is built the till says which bill was made.
 */
export function CheckoutZone({ cartId, empty, pay }: Props) {
  const [pending, startTransition] = useTransition();
  const inFlight = useRef(false);
  const chosen = useRef<number[]>([]);
  const [choices, setChoices] = useState<PromotionChoice[] | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [paid, setPaid] = useState<PaidSale | null>(null);

  function submit(choice: number[]) {
    if (inFlight.current) return;
    inFlight.current = true;
    chosen.current = choice;
    setMessage(null);
    setPaid(null);
    startTransition(async () => {
      try {
        const result = await pay(cartId, choice);
        if (!result.ok && "choices" in result) {
          setChoices(result.choices);
          return;
        }
        setChoices(null);
        chosen.current = [];
        if (result.ok) setPaid(result.sale);
        else setMessage(result.message);
      } catch {
        // the page's own server could not be reached, or its answer never came back (AC-talad-028) — the api may have
        // paid; pressing again sends the same cart and is told so
        setChoices(null);
        chosen.current = [];
        setMessage(OFFLINE);
      } finally {
        inFlight.current = false;
      }
    });
  }

  function cancel() {
    setChoices(null);
    chosen.current = [];
  }

  return (
    <>
      <button
        type="button"
        className="btn btn-block pay"
        disabled={empty || pending}
        onClick={() => submit([])}
        data-testid="ui-talad-002-checkout"
      >
        {pending ? (
          <>
            <span className="loading loading-spinner loading-sm" aria-hidden="true" />
            กำลังชำระเงิน…
          </>
        ) : (
          "ชำระเงิน"
        )}
      </button>
      {message ? (
        <div role="alert" className="toast">
          <div className="grow">{message}</div>
        </div>
      ) : null}
      {paid ? (
        <div role="status" className="toast success">
          <div className="grow">
            ชำระเงินสำเร็จ · ใบเสร็จ {paid.receiptNo} · {formatBaht(paid.netTotal)}
          </div>
        </div>
      ) : null}
      <PromotionChoiceDialog
        choices={choices ?? []}
        open={choices !== null}
        pending={pending}
        onChoose={(id) => submit([...chosen.current, id])}
        onCancel={cancel}
      />
    </>
  );
}
