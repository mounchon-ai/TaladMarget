"use client";

import Link from "next/link";
import { useActionState, useState } from "react";
import { ProductPicker, type ProductOption } from "@/components/product-picker";
import {
  PROMOTIONS_PATH,
  PROMOTION_TYPES,
  conditionsFor,
  type PromotionField,
  type PromotionFieldErrors,
  type PromotionFormState,
  type PromotionValues,
} from "@/lib/promotion-form";

type Props = {
  initial: PromotionValues;
  action: (prev: PromotionFormState, formData: FormData) => Promise<PromotionFormState>;
  find: (term: string) => Promise<ProductOption[] | null>;
};

type Plain = { field: PromotionField; label: string; testId: string; type?: string; inputMode?: "numeric" | "decimal"; required?: boolean };

function TextField({ field, label, testId, type = "text", inputMode, required, values, errors }: Plain & { values: PromotionValues; errors: PromotionFieldErrors }) {
  const error = errors[field];
  return (
    <label className={error ? "field invalid" : "field"}>
      <span className="lbl">
        {label} {required ? <span className="req">*</span> : null}
      </span>
      <input
        type={type}
        name={field}
        inputMode={inputMode}
        defaultValue={values[field]}
        key={`${field}|${values[field]}`}
        autoComplete="off"
        className="input"
        aria-invalid={error ? true : undefined}
        data-testid={testId}
      />
      {error ? <span className="err">{error}</span> : null}
    </label>
  );
}

const product = (id: string, name: string): ProductOption | null => (id ? { id: Number(id), name } : null);

/**
 * UI-talad-016 เพิ่ม/แก้ไขโปรโมชั่น — the wireframe MCK-talad-016 is the only picture of this screen; every
 * data-testid is its control id, zones general · conditions · footer in its order. Only the condition fields
 * the chosen form uses are drawn (conditionsFor). noValidate: every rule is the domain's, and its sentence —
 * not the browser's bubble — sits under the field that is wrong (state "error").
 */
export function PromotionForm({ initial, action, find }: Props) {
  const [state, formAction, pending] = useActionState(action, undefined);
  const errors = state?.errors ?? {};
  const values = state?.values ?? initial;
  // the type decides which conditions are drawn, so it is followed here as well; the select itself stays
  // uncontrolled like every other box — React resets a form after its action, and a controlled select comes
  // back from that reset empty while its conditions are still drawn
  const [type, setType] = useState(values.type);
  const conditions = conditionsFor(type);
  const shown = (field: PromotionField) => (conditions as readonly string[]).includes(field);

  return (
    <form action={formAction} noValidate className="card formcard">
      <h2>ข้อมูลทั่วไป</h2>
      <TextField field="name" label="ชื่อโปร" testId="ui-talad-016-ent-006-name" required values={values} errors={errors} />

      <label className={errors.type ? "field invalid" : "field"}>
        <span className="lbl">
          รูปแบบ <span className="req">*</span>
        </span>
        <select
          name="type"
          defaultValue={values.type}
          key={`type|${values.type}`}
          onChange={(e) => setType(e.target.value)}
          className="select"
          aria-invalid={errors.type ? true : undefined}
          data-testid="ui-talad-016-ent-006-type"
        >
          <option value="">— เลือกรูปแบบ —</option>
          {PROMOTION_TYPES.map((t) => (
            <option key={t.code} value={t.code}>
              {t.label}
            </option>
          ))}
        </select>
        {errors.type ? <span className="err">{errors.type}</span> : null}
      </label>

      <TextField field="startDate" label="วันเริ่ม" type="date" testId="ui-talad-016-ent-006-start-date" required values={values} errors={errors} />
      <TextField field="endDate" label="วันสิ้นสุด (ว่าง = ไม่มีวันสิ้นสุด)" type="date" testId="ui-talad-016-ent-006-end-date" values={values} errors={errors} />

      {conditions.length > 0 ? (
        <div className="conditions">
          <h2>เงื่อนไข</h2>
          {shown("productA") ? (
            <ProductPicker name="productA" label="สินค้า A" testId="ui-talad-016-ent-006-product-a" initial={product(values.productA, values.productAName)} error={errors.productA} find={find} />
          ) : null}
          {shown("qtyA") ? (
            <TextField field="qtyA" label="จำนวน A ต่อชุด" inputMode="numeric" testId="ui-talad-016-ent-006-qty-a" required values={values} errors={errors} />
          ) : null}
          {shown("productB") ? (
            <ProductPicker name="productB" label="สินค้า B" testId="ui-talad-016-ent-006-product-b" initial={product(values.productB, values.productBName)} error={errors.productB} find={find} />
          ) : null}
          {shown("qtyB") ? (
            <TextField field="qtyB" label="จำนวน B ต่อชุด" inputMode="numeric" testId="ui-talad-016-ent-006-qty-b" required values={values} errors={errors} />
          ) : null}
          {shown("freeProduct") ? (
            <ProductPicker
              name="freeProduct"
              label="ของแถม"
              testId="ui-talad-016-ent-006-free-product"
              initial={product(values.freeProduct, values.freeProductName)}
              error={errors.freeProduct}
              find={find}
            />
          ) : null}
          {shown("freeQty") ? (
            <TextField field="freeQty" label="จำนวนของแถมต่อชุด" inputMode="numeric" testId="ui-talad-016-ent-006-free-qty" required values={values} errors={errors} />
          ) : null}
          {shown("ratePercent") ? (
            <TextField field="ratePercent" label="% ส่วนลด" inputMode="numeric" testId="ui-talad-016-ent-006-rate-percent" required values={values} errors={errors} />
          ) : null}
          {shown("minSubtotal") ? (
            <TextField field="minSubtotal" label="ยอดขั้นต่ำ" inputMode="decimal" testId="ui-talad-016-ent-006-min-subtotal" required values={values} errors={errors} />
          ) : null}
        </div>
      ) : null}

      {state?.message ? (
        <div role="alert" className="alert">
          {state.message}
        </div>
      ) : null}

      <div className="formactions">
        {/* state "loading" — save is off while saving */}
        <button type="submit" className="btn btn-primary" disabled={pending} data-testid="ui-talad-016-save">
          {pending ? (
            <>
              <span className="loading loading-spinner loading-sm" aria-hidden="true" />
              กำลังบันทึก…
            </>
          ) : (
            "บันทึก"
          )}
        </button>
        <Link href={PROMOTIONS_PATH} className="btn btn-ghost" data-testid="ui-talad-016-cancel">
          ยกเลิก
        </Link>
      </div>
    </form>
  );
}
