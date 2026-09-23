// UI-talad-016 state "loading" — the form as outlines while the promotion loads.
export default function Loading() {
  return (
    <div data-screen="UI-talad-016" aria-busy="true">
      <div className="pagehead">
        <h1>แก้ไขโปรโมชั่น</h1>
      </div>
      <div className="card formcard">
        <div className="field">
          <span className="lbl">ชื่อโปร</span>
          <span className="skeleton" />
        </div>
        <div className="field">
          <span className="lbl">รูปแบบ</span>
          <span className="skeleton" />
        </div>
        <div className="field">
          <span className="lbl">วันเริ่ม</span>
          <span className="skeleton" />
        </div>
      </div>
    </div>
  );
}
