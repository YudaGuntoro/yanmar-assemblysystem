import type { Metadata } from "next";
import WorkRecordPage from "@/production/WorkRecordPage";

export const metadata: Metadata = { title: "Nut Runner Work Record | PT. Yanmar Diesel Indonesia" };

export default function Page() {
  return <WorkRecordPage />;
}
