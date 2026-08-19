import type { Metadata } from "next";
import ProductionDashboard from "@/production/ProductionDashboard";

export const metadata: Metadata = {
  title: "Assembly System | PT. Yanmar Diesel Indonesia",
  description: "Assembly nut runner work record and monitoring dashboard",
};

export default function LeaktesterWorkRecordHome() {
  return <ProductionDashboard />;
}
