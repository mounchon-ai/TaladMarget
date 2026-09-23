// UI-talad-006 state "loading" — the form as outlines while the member loads.
export default function Loading() {
  return (
    <div data-screen="UI-talad-006" aria-busy="true">
      <div className="pagehead">
        <h1>ข้อมูลสมาชิก</h1>
      </div>
      <div className="card formcard">
        <div className="field">
          <span className="lbl">ชื่อ</span>
          <span className="skeleton" />
        </div>
        <div className="field">
          <span className="lbl">เบอร์โทร</span>
          <span className="skeleton" />
        </div>
        <div className="field">
          <span className="lbl">ยอดซื้อสะสม</span>
          <span className="skeleton" />
        </div>
      </div>
    </div>
  );
}
