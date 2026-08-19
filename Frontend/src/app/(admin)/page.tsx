import type { Metadata } from "next";
import ProductionDashboard from "@/production/ProductionDashboard";

export const metadata: Metadata = {
  title: "Smart Engine Assembly System | PT. Yanmar Diesel Indonesia",
  description: "Smart engine assembly work record and monitoring dashboard",
};

export default function LeaktesterWorkRecordHome() {
  return <ProductionDashboard />;
}
