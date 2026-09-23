import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "ตลาดมาร์เก็ต",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="th" data-theme="talad">
      <body>{children}</body>
    </html>
  );
}
