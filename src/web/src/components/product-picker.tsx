"use client";

import { useId, useRef, useState, useTransition } from "react";

export type ProductOption = { id: number; name: string };

type Props = {
  /** the ENT-006 attribute the chosen id is submitted as; its name goes as `<name>Name` */
  name: string;
  label: string;
  testId: string;
  initial: ProductOption | null;
  error?: string;
  find: (term: string) => Promise<ProductOption[] | null>;
};

const WAIT_MS = 250; // one request after the person stops typing, not one per key

/**
 * UI-talad-016 product fields (สินค้า A · สินค้า B · ของแถม) — state "overflow": thousands of products, so the
 * box searches at the server as the person types and lists what comes back; state "loading": a spinner while
 * it does. The data-testid sits on the box a person types into; the listed products carry none (they are
 * the answer, not controls design declared). What is submitted is the chosen product's id — typing without
 * choosing clears it, so the api answers the field as empty rather than saving a product nobody picked.
 */
export function ProductPicker({ name, label, testId, initial, error, find }: Props) {
  const id = useId();
  const [chosen, setChosen] = useState<ProductOption | null>(initial);
  const [term, setTerm] = useState(initial?.name ?? "");
  // undefined: the list is closed · null: the search failed
  const [options, setOptions] = useState<ProductOption[] | null | undefined>(undefined);
  const [pending, startTransition] = useTransition();
  const latest = useRef(0);
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  function type(next: string) {
    setTerm(next);
    setChosen(null);
    clearTimeout(timer.current);
    const ask = ++latest.current;
    if (next.trim() === "") {
      setOptions(undefined);
      return;
    }
    timer.current = setTimeout(() => {
      startTransition(async () => {
        const found = await find(next);
        // an answer to a term the person has already typed past is dropped
        if (ask === latest.current) startTransition(() => setOptions(found));
      });
    }, WAIT_MS);
  }

  function choose(option: ProductOption) {
    latest.current++;
    setChosen(option);
    setTerm(option.name);
    setOptions(undefined);
  }

  return (
    <div className={error ? "field invalid" : "field"}>
      <label htmlFor={id} className="lbl">
        {label} <span className="req">*</span>
      </label>
      <div className="picker">
        <input
          id={id}
          type="text"
          role="combobox"
          aria-expanded={options !== undefined}
          aria-controls={`${id}-list`}
          aria-autocomplete="list"
          value={term}
          onChange={(e) => type(e.target.value)}
          autoComplete="off"
          className="input"
          placeholder="พิมพ์ชื่อหรือบาร์โค้ดเพื่อค้นหา"
          aria-invalid={error ? true : undefined}
          data-testid={testId}
        />
        {pending ? <span className="loading loading-spinner loading-sm" role="status" aria-label="กำลังโหลดรายการสินค้า" /> : null}
        <input type="hidden" name={name} value={chosen ? String(chosen.id) : ""} />
        <input type="hidden" name={`${name}Name`} value={chosen?.name ?? ""} />
        {options !== undefined ? (
          <ul id={`${id}-list`} role="listbox" aria-label={label} className="picker-list">
            {options === null ? (
              <li className="muted">โหลดข้อมูลไม่สำเร็จ</li>
            ) : options.length === 0 ? (
              <li className="muted">ไม่พบสินค้า</li>
            ) : (
              options.map((o) => (
                <li key={o.id} role="option" aria-selected={false}>
                  <button type="button" onClick={() => choose(o)}>
                    {o.name}
                  </button>
                </li>
              ))
            )}
          </ul>
        ) : null}
      </div>
      {error ? <span className="err">{error}</span> : null}
    </div>
  );
}
