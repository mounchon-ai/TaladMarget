// UI-talad-017 state "loading" — the setting as an outline while it loads.
export default function Loading() {
  return (
    <div data-screen="UI-talad-017" aria-busy="true">
      <div className="pagehead">
        <h1>ตั้งส่วนลดสมาชิก</h1>
      </div>
      <div className="card formcard">
        <div className="field">
          <span className="lbl">% ส่วนลดสมาชิก</span>
          <span className="skeleton" />
        </div>
        <div className="field">
          <span className="lbl">ตั้งล่าสุดโดย</span>
          <span className="skeleton" />
        </div>
        <div className="field">
          <span className="lbl">ตั้งล่าสุดเมื่อ</span>
          <span className="skeleton" />
        </div>
      </div>
    </div>
  );
}
