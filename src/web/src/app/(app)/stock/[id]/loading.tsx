// UI-talad-012 state "loading" — each section as outlines while the product loads.
export default function Loading() {
  return (
    <div data-screen="UI-talad-012" aria-busy="true">
      <div className="pagehead">
        <h1>รายละเอียดสินค้า</h1>
      </div>
      <div className="card formcard">
        {["ชื่อสินค้า", "คงเหลือ", "จุดเตือน", "ราคาปัจจุบัน"].map((label) => (
          <div className="field" key={label}>
            <span className="lbl">{label}</span>
            <span className="skeleton" />
          </div>
        ))}
      </div>
      {["ประวัติราคา", "รายการปรับสต็อก"].map((section) => (
        <section className="card" key={section}>
          <h2>{section}</h2>
          <span className="skeleton" />
        </section>
      ))}
    </div>
  );
}
