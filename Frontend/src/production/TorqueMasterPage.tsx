"use client";

import { ChangeEvent, FormEvent, useCallback, useEffect, useMemo, useRef, useState } from "react";
import ActionIconButton from "@/components/common/ActionIconButton";
import { Modal } from "@/components/ui/modal";
import { apiGet, apiRequest } from "@/lib/api";
import { ArrowUpIcon, PlusIcon } from "@/icons";
import { ConfirmModal } from "@/components/ui/modal/ConfirmModal";
import type { TorqueMasterResponse, TorqueMasterRow, TorqueMasterSpec } from "./types";

const inputClass = "h-10 rounded-lg border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-800 outline-none transition placeholder:text-slate-400 focus:border-brand-400 focus:ring-3 focus:ring-brand-500/15 dark:border-slate-700 dark:bg-slate-900 dark:text-white";
const headerCellClass = "bg-[#e60028] px-4 py-2.5 text-xs font-black uppercase leading-4 text-white";

type EditNumberModalState = {
  field: "process_no" | "step_no";
  row: TorqueMasterRow;
} | null;

function formatValue(value?: number | null) {
  if (value === null || value === undefined) return "";
  return new Intl.NumberFormat("en-US", { maximumFractionDigits: 2 }).format(value);
}

function specText(spec?: TorqueMasterSpec, key: "min" | "max" = "min") {
  if (!spec) return "";
  const value = key === "min" ? spec.min : spec.max;
  return formatValue(value);
}

function toolClass(row: TorqueMasterRow) {
  if (row.tool_type === "Torque Wrench") return "bg-emerald-50 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-300";
  if (row.tool_type === "Nut Runner") return "bg-sky-50 text-sky-700 dark:bg-sky-500/10 dark:text-sky-300";
  if (row.tool_type === "Visual Inspect") return "bg-violet-50 text-violet-700 dark:bg-violet-500/10 dark:text-violet-300";
  return "bg-slate-100 text-slate-500 dark:bg-slate-800 dark:text-slate-300";
}

function processKey(row: TorqueMasterRow) {
  return row.process_no ?? "";
}

function processRowSpan(rows: TorqueMasterRow[], index: number) {
  const current = processKey(rows[index]);
  if (index > 0 && processKey(rows[index - 1]) === current) return 0;

  let span = 1;
  for (let nextIndex = index + 1; nextIndex < rows.length; nextIndex += 1) {
    if (processKey(rows[nextIndex]) !== current) break;
    span += 1;
  }

  return span;
}

export default function TorqueMasterPage() {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [data, setData] = useState<TorqueMasterResponse>({ models: [], rows: [] });
  const [message, setMessage] = useState<{ kind: "ok" | "error"; text: string } | null>(null);
  const [busy, setBusy] = useState(false);
  const [searchText, setSearchText] = useState("");
  const [processNo, setProcessNo] = useState("");
  const [selectedModelId, setSelectedModelId] = useState("");
  const [editNumberModal, setEditNumberModal] = useState<EditNumberModalState>(null);
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [editingRow, setEditingRow] = useState<TorqueMasterRow | null>(null);
  const [deletingRow, setDeletingRow] = useState<TorqueMasterRow | null>(null);

  const query = useMemo(() => {
    const params = new URLSearchParams();
    if (searchText.trim()) params.set("search", searchText.trim());
    if (processNo.trim()) params.set("process_no", processNo.trim());
    if (selectedModelId.trim()) params.set("model_ids", selectedModelId.trim());
    return params.toString() ? `?${params.toString()}` : "";
  }, [processNo, searchText, selectedModelId]);

  const load = useCallback(async () => {
    setBusy(true);
    setMessage(null);
    try {
      setData(await apiGet<TorqueMasterResponse>(`/api/leaktester/torque-master${query}`));
    } catch (err) {
      setMessage({ kind: "error", text: err instanceof Error ? err.message : "Failed to load torque master." });
    } finally {
      setBusy(false);
    }
  }, [query]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (!selectedModelId && data.models[0]) {
      setSelectedModelId(String(data.models[0].id));
    }
  }, [data.models, selectedModelId]);

  async function importExcel(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) return;

    const form = new FormData();
    form.append("file", file);
    setBusy(true);
    setMessage(null);
    try {
      const result = await apiRequest<{
        rows_read: number;
        standards_saved: number;
        specs_saved: number;
        models_saved: number;
        skipped: number;
      }>("/api/leaktester/torque-master/import", {
        body: form,
        method: "POST",
      });
      setMessage({
        kind: "ok",
        text: `Import done. Rows: ${result.rows_read}, standards: ${result.standards_saved}, specs: ${result.specs_saved}, skipped: ${result.skipped}.`,
      });
      await load();
    } catch (err) {
      setMessage({ kind: "error", text: err instanceof Error ? err.message : "Failed to import torque master." });
    } finally {
      setBusy(false);
      event.target.value = "";
    }
  }

  async function submitNumberUpdate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editNumberModal) return;

    const form = new FormData(event.currentTarget);
    const rawValue = String(form.get("value") ?? "").trim();
    const parsedValue = rawValue === "" ? null : Number(rawValue);

    if (parsedValue !== null && (!Number.isInteger(parsedValue) || parsedValue < 0)) {
      setMessage({ kind: "error", text: "Value must be a positive number." });
      return;
    }

    setBusy(true);
    setMessage(null);
    try {
      await apiRequest(`/api/leaktester/torque-master/rows/${editNumberModal.row.id}`, {
        body: JSON.stringify({ [editNumberModal.field]: parsedValue }),
        method: "PUT",
      });
      setEditNumberModal(null);
      setMessage({ kind: "ok", text: editNumberModal.field === "process_no" ? "Process no updated." : "Step number updated." });
      await load();
    } catch (err) {
      setMessage({ kind: "error", text: err instanceof Error ? err.message : "Failed to update torque master row." });
    } finally {
      setBusy(false);
    }
  }

  async function submitCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const numberOrNull = (name: string) => {
      const value = String(form.get(name) ?? "").trim();
      return value === "" ? null : Number(value);
    };
    setBusy(true);
    setMessage(null);
    try {
      await apiRequest("/api/leaktester/torque-master/rows", {
        method: "POST",
        body: JSON.stringify({
          model_id: Number(selectedModelId),
          process_no: numberOrNull("process_no"),
          step_no: numberOrNull("step_no"),
          item: String(form.get("item") ?? "").trim(),
          tool_type: String(form.get("tool_type") ?? "Visual Inspect"),
          item_check: String(form.get("item_check") ?? "").trim() || null,
          nut_spec: String(form.get("nut_spec") ?? "").trim() || null,
          nut_usage: numberOrNull("nut_usage"),
          tool: numberOrNull("tool"),
          min: numberOrNull("min"),
          max: numberOrNull("max"),
          unit: String(form.get("unit") ?? "").trim() || null,
          page: numberOrNull("page"),
        }),
      });
      setCreateModalOpen(false);
      setMessage({ kind: "ok", text: "Torque master row created." });
      await load();
    } catch (err) {
      setMessage({ kind: "error", text: err instanceof Error ? err.message : "Failed to create torque master row." });
    } finally {
      setBusy(false);
    }
  }

  async function submitFullUpdate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editingRow) return;
    const form = new FormData(event.currentTarget);
    const numberOrNull = (name: string) => {
      const value = String(form.get(name) ?? "").trim();
      return value === "" ? null : Number(value);
    };
    setBusy(true);
    setMessage(null);
    try {
      await apiRequest(`/api/leaktester/torque-master/rows/${editingRow.id}`, {
        method: "PUT",
        body: JSON.stringify({
          model_id: Number(selectedModelId),
          process_no: numberOrNull("process_no"), step_no: numberOrNull("step_no"),
          item: String(form.get("item") ?? "").trim(), tool_type: String(form.get("tool_type") ?? "Visual Inspect"),
          item_check: String(form.get("item_check") ?? "").trim() || null, nut_spec: String(form.get("nut_spec") ?? "").trim() || null,
          nut_usage: numberOrNull("nut_usage"), tool: numberOrNull("tool"), min: numberOrNull("min"), max: numberOrNull("max"),
          unit: String(form.get("unit") ?? "").trim() || null, model_page: String(form.get("model_page") ?? "").trim() || null, page: numberOrNull("page"),
        }),
      });
      setEditingRow(null);
      setMessage({ kind: "ok", text: "Torque master row updated." });
      await load();
    } catch (err) {
      setMessage({ kind: "error", text: err instanceof Error ? err.message : "Failed to update torque master row." });
    } finally {
      setBusy(false);
    }
  }

  async function deleteRow() {
    if (!deletingRow) return;
    setBusy(true);
    try {
      await apiRequest(`/api/leaktester/torque-master/rows/${deletingRow.id}`, { method: "DELETE" });
      setDeletingRow(null);
      setMessage({ kind: "ok", text: "Torque master row deleted." });
      await load();
    } catch (err) {
      setMessage({ kind: "error", text: err instanceof Error ? err.message : "Failed to delete torque master row." });
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
    <div className="space-y-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="text-xl font-black text-slate-900 dark:text-white">Torque Master</h1>
        <div className="flex items-center gap-2 text-sm font-semibold text-slate-400 dark:text-slate-500">
          <span>Home</span>
          <span className="text-slate-500">&gt;</span>
          <span>Master Data</span>
          <span className="text-slate-500">&gt;</span>
          <span className="text-slate-900 dark:text-white">Torque Master</span>
        </div>
      </div>

      {message ? (
        <div className={`rounded-md border px-4 py-3 text-sm font-medium ${message.kind === "ok" ? "border-emerald-200 bg-emerald-50 text-emerald-700" : "border-rose-200 bg-rose-50 text-rose-700"}`}>
          {message.text}
        </div>
      ) : null}

      <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-col gap-4 xl:flex-row xl:items-end xl:justify-between">
          <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
            <label className="text-base font-bold text-slate-600 dark:text-slate-300">Select Engine Model</label>
            <select
              className="h-10 min-w-56 rounded-md border border-slate-300 bg-white px-3 text-sm font-bold text-slate-800 outline-none focus:border-brand-400 focus:ring-3 focus:ring-brand-500/15 dark:border-slate-700 dark:bg-slate-900 dark:text-white"
              onChange={(event) => setSelectedModelId(event.target.value)}
              value={selectedModelId}
            >
              {data.models.map((model) => (
                <option key={model.id} value={model.id}>{model.model_name}</option>
              ))}
            </select>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <input className={inputClass} onChange={(event) => setSearchText(event.target.value)} placeholder="Search item / check / nut" value={searchText} />
            <input className={`${inputClass} w-32`} inputMode="numeric" onChange={(event) => setProcessNo(event.target.value)} placeholder="Process" value={processNo} />
            <input
              accept=".xlsx,.xls"
              className="hidden"
              onChange={(event) => void importExcel(event)}
              ref={fileInputRef}
              type="file"
            />
            <button
              className="inline-flex h-10 items-center gap-2 rounded-lg bg-[#22a568] px-4 text-sm font-bold text-white shadow-sm transition hover:bg-[#198c58] disabled:bg-slate-400"
              disabled={busy}
              onClick={() => fileInputRef.current?.click()}
              type="button"
            >
              <ArrowUpIcon className="h-4 w-4" />
              Upload Excel
            </button>
            <button className="inline-flex h-10 items-center gap-2 rounded-lg bg-[#2f5597] px-4 text-sm font-bold text-white transition hover:bg-[#24487f] disabled:bg-slate-400" disabled={busy} onClick={() => setCreateModalOpen(true)} type="button">
              <PlusIcon className="h-4 w-4" />
              Create
            </button>
          </div>
        </div>
      </section>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="flex items-center justify-between border-b border-slate-200 px-5 py-4 dark:border-slate-800">
          <div>
            <div className="text-sm font-black text-slate-900 dark:text-white">{data.rows.length} Standard Rows</div>
            <div className="text-xs font-bold text-slate-400">{data.models.find((model) => String(model.id) === selectedModelId)?.model_name || "Model"} data master</div>
          </div>
          <div className="rounded-md bg-slate-100 px-2.5 py-1 text-xs font-black text-slate-600 dark:bg-slate-800 dark:text-slate-300">Min / Max</div>
        </div>

        <div className="mx-5 mb-5 mt-4 max-h-[calc(100vh-260px)] overflow-auto rounded-lg border border-slate-200 dark:border-slate-800">
          <table className="min-w-[1280px] w-full border-separate border-spacing-0 text-left text-xs">
            <thead className="sticky top-0 z-20 shadow-[0_1px_0_rgba(226,232,240,1)] dark:shadow-[0_1px_0_rgba(30,41,59,1)]">
              <tr>
                <th className={`${headerCellClass} rounded-tl-lg`}>ProcessNo</th>
                <th className={headerCellClass}>Step Number</th>
                <th className={headerCellClass}>Item</th>
                <th className={`${headerCellClass} w-36 min-w-36`}>Tool Type</th>
                <th className={headerCellClass}>Item Check</th>
                <th className={`${headerCellClass} text-center`}>Min</th>
                <th className={`${headerCellClass} text-center`}>Max</th>
                <th className={`${headerCellClass} text-center`}>Unit</th>
                <th className={headerCellClass}>Nut Specification</th>
                <th className={`${headerCellClass} text-center`}>Nut Usage</th>
                <th className={`${headerCellClass} text-center`}>Tool</th>
                <th className={headerCellClass}>Model File</th>
                <th className={`${headerCellClass} text-center`}>Page</th>
                <th className={`${headerCellClass} rounded-tr-lg text-center`}>Action</th>
              </tr>
            </thead>
            <tbody>
              {data.rows.length === 0 ? (
                <tr>
                  <td className="px-4 py-12 text-center text-sm font-bold text-slate-400" colSpan={14}>No torque standard data.</td>
                </tr>
              ) : (
                data.rows.map((row, index) => (
                  <tr className={`${index % 2 === 0 ? "bg-white dark:bg-slate-900" : "bg-slate-50/80 dark:bg-slate-800/35"} hover:bg-brand-50/50 dark:hover:bg-brand-500/5`} key={row.id}>
                    {(() => {
                      const spec = row.specs[selectedModelId] || row.specs[String(data.models[0]?.id)];
                      const toolTone = row.tool_type === "Torque Wrench" ? "bg-green-100 dark:bg-emerald-500/15" : row.tool_type === "Nut Runner" ? "bg-cyan-100 dark:bg-cyan-500/15" : row.tool_type === "Visual Inspect" ? "bg-violet-50 dark:bg-violet-500/10" : "";
                      const processSpan = processRowSpan(data.rows, index);

                      return (
                        <>
                    {processSpan > 0 ? (
                      <td className="border border-slate-200 bg-white p-0 text-center align-middle text-lg font-black text-slate-800 dark:border-slate-800 dark:bg-slate-900 dark:text-slate-100" rowSpan={processSpan}>
                        <button className="flex h-full min-h-16 w-full items-center justify-center px-3 py-2 transition hover:bg-brand-50 focus:outline-none focus:ring-2 focus:ring-brand-500/30 dark:hover:bg-brand-500/10" onClick={() => setEditNumberModal({ field: "process_no", row })} title="Update Process No" type="button">
                          {row.process_no ?? ""}
                        </button>
                      </td>
                    ) : null}
                    <td className="border border-slate-200 p-0 text-center font-bold text-slate-700 dark:border-slate-800 dark:text-slate-200">
                      <button className="h-full min-h-10 w-full px-3 py-2 transition hover:bg-brand-50 focus:outline-none focus:ring-2 focus:ring-brand-500/30 dark:hover:bg-brand-500/10" onClick={() => setEditNumberModal({ field: "step_no", row })} title="Update Step Number" type="button">
                        {row.step_no ?? ""}
                      </button>
                    </td>
                    <td className="border border-slate-200 px-3 py-2 font-bold text-slate-800 dark:border-slate-800 dark:text-white">{row.item || ""}</td>
                    <td className={`w-36 min-w-36 whitespace-nowrap border border-slate-200 px-3 py-2 dark:border-slate-800 ${toolTone}`}>
                      <span className={`inline-flex whitespace-nowrap rounded-md px-2.5 py-1 text-[11px] font-black ${toolClass(row)}`}>{row.tool_type}</span>
                    </td>
                    <td className="border border-slate-200 px-3 py-2 font-semibold text-slate-600 dark:border-slate-800 dark:text-slate-300">{row.item_check || ""}</td>
                    <td className={`border border-slate-200 px-3 py-2 text-center font-bold dark:border-slate-800 ${spec ? "bg-green-100 text-green-900 dark:bg-emerald-500/15 dark:text-emerald-200" : "text-slate-300"}`}>{specText(spec, "min")}</td>
                    <td className={`border border-slate-200 px-3 py-2 text-center font-bold dark:border-slate-800 ${spec ? "bg-green-100 text-green-900 dark:bg-emerald-500/15 dark:text-emerald-200" : "text-slate-300"}`}>{specText(spec, "max")}</td>
                    <td className="border border-slate-200 px-3 py-2 text-center font-semibold text-slate-600 dark:border-slate-800 dark:text-slate-300">{spec?.unit || ""}</td>
                    <td className="border border-slate-200 px-3 py-2 font-semibold text-slate-600 dark:border-slate-800 dark:text-slate-300">{row.nut_spec || ""}</td>
                    <td className="border border-slate-200 px-3 py-2 text-center font-semibold text-slate-600 dark:border-slate-800 dark:text-slate-300">{row.nut_usage ?? ""}</td>
                    <td className="border border-slate-200 px-3 py-2 text-center font-semibold text-slate-600 dark:border-slate-800 dark:text-slate-300">{row.tool ?? ""}</td>
                    <td className="border border-slate-200 px-3 py-2 font-semibold text-slate-600 dark:border-slate-800 dark:text-slate-300">{row.model_page || data.models.find((model) => String(model.id) === selectedModelId)?.model_name || ""}</td>
                    <td className="border border-slate-200 px-3 py-2 text-center font-semibold text-slate-600 dark:border-slate-800 dark:text-slate-300">{row.page ?? ""}</td>
                    <td className="border border-slate-200 px-3 py-2 text-center dark:border-slate-800">
                      <div className="inline-flex items-center gap-2">
                        <ActionIconButton aria-label="Update torque master row" icon="edit" onClick={() => setEditingRow(row)} title="Update" />
                        <ActionIconButton aria-label="Delete torque master row" icon="delete" onClick={() => setDeletingRow(row)} title="Delete" />
                      </div>
                    </td>
                        </>
                      );
                    })()}
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </section>
    </div>
    <Modal className="mx-4 max-w-[420px] p-0" isOpen={Boolean(editNumberModal)} onClose={() => !busy && setEditNumberModal(null)} showCloseButton={false}>
      <form onSubmit={(event) => void submitNumberUpdate(event)}>
        <div className="border-b border-slate-200 px-6 py-5 dark:border-slate-800">
          <h2 className="text-lg font-black text-slate-900 dark:text-white">
            {editNumberModal?.field === "process_no" ? "Update Process No" : "Update Step Number"}
          </h2>
        </div>
        <div className="px-6 py-5">
          <label className="text-sm font-bold text-slate-700 dark:text-slate-200">
            {editNumberModal?.field === "process_no" ? "Process No" : "Step Number"}
            <input
              className={`${inputClass} mt-2 w-full`}
              defaultValue={editNumberModal?.field === "process_no" ? editNumberModal.row.process_no ?? "" : editNumberModal?.row.step_no ?? ""}
              inputMode="numeric"
              min={0}
              name="value"
              type="number"
            />
          </label>
        </div>
        <div className="flex justify-end gap-3 border-t border-slate-200 px-6 py-5 dark:border-slate-800">
          <button className="h-10 rounded-lg border border-slate-300 px-5 text-sm font-bold text-slate-700 transition hover:bg-slate-50 disabled:opacity-60 dark:border-slate-700 dark:text-slate-200" disabled={busy} onClick={() => setEditNumberModal(null)} type="button">
            Cancel
          </button>
          <button className="h-10 rounded-lg bg-brand-500 px-5 text-sm font-bold text-white transition hover:bg-brand-600 disabled:bg-brand-300" disabled={busy} type="submit">
            {busy ? "Saving..." : "Save"}
          </button>
        </div>
      </form>
    </Modal>
    <ConfirmModal confirmText="Yes, Delete" isDestructive isLoading={busy} isOpen={Boolean(deletingRow)} message={deletingRow ? `Delete ${deletingRow.item || "this torque master row"}?` : ""} onClose={() => !busy && setDeletingRow(null)} onConfirm={() => void deleteRow()} title="Delete Torque Master Row?" />
    <Modal className="mx-4 max-h-[90vh] max-w-[720px] overflow-y-auto p-0" isOpen={createModalOpen} onClose={() => !busy && setCreateModalOpen(false)} showCloseButton={false}>
      <form onSubmit={(event) => void submitCreate(event)}>
        <div className="border-b border-slate-200 px-6 py-5 dark:border-slate-800"><h2 className="text-lg font-black text-slate-900 dark:text-white">Create Torque Master Row</h2></div>
        <div className="grid grid-cols-1 gap-4 px-6 py-5 sm:grid-cols-2">
          {[["process_no", "Process No"], ["step_no", "Step Number"], ["item", "Item"], ["item_check", "Item Check"], ["nut_spec", "Nut Specification"], ["nut_usage", "Nut Usage"], ["tool", "Tool"], ["min", "Min"], ["max", "Max"], ["unit", "Unit"], ["model_page", "Model File"], ["page", "Page"]].map(([name, label]) => (
            <label className="text-sm font-bold text-slate-700 dark:text-slate-200" key={name}>{label}<input className={`${inputClass} mt-2 w-full`} name={name} required={name === "item"} type={name === "item" || name === "item_check" || name === "nut_spec" || name === "unit" || name === "model_page" ? "text" : "number"} /></label>
          ))}
          <label className="text-sm font-bold text-slate-700 dark:text-slate-200">Tool Type<select className={`${inputClass} mt-2 w-full`} defaultValue="Visual Inspect" name="tool_type"><option>Visual Inspect</option><option>Nut Runner</option></select></label>
        </div>
        <div className="flex justify-end gap-3 border-t border-slate-200 px-6 py-5 dark:border-slate-800"><button className="h-10 rounded-lg border border-slate-300 px-5 text-sm font-bold text-slate-700" disabled={busy} onClick={() => setCreateModalOpen(false)} type="button">Cancel</button><button className="h-10 rounded-lg bg-brand-500 px-5 text-sm font-bold text-white disabled:bg-brand-300" disabled={busy} type="submit">{busy ? "Saving..." : "Save"}</button></div>
      </form>
    </Modal>
    <Modal className="mx-4 max-h-[90vh] max-w-[720px] overflow-y-auto p-0" isOpen={Boolean(editingRow)} onClose={() => !busy && setEditingRow(null)} showCloseButton={false}>
      <form onSubmit={(event) => void submitFullUpdate(event)}>
        <div className="border-b border-slate-200 px-6 py-5 dark:border-slate-800"><h2 className="text-lg font-black text-slate-900 dark:text-white">Update Torque Master Row</h2></div>
        <div className="grid grid-cols-1 gap-4 px-6 py-5 sm:grid-cols-2">
          {(() => {
            const spec = editingRow?.specs[selectedModelId];
            const values: Record<string, string | number | null | undefined> = { process_no: editingRow?.process_no, step_no: editingRow?.step_no, item: editingRow?.item, item_check: editingRow?.item_check, nut_spec: editingRow?.nut_spec, nut_usage: editingRow?.nut_usage, tool: editingRow?.tool, min: spec?.min, max: spec?.max, unit: spec?.unit, model_page: editingRow?.model_page, page: editingRow?.page };
            return [["process_no", "Process No"], ["step_no", "Step Number"], ["item", "Item"], ["item_check", "Item Check"], ["nut_spec", "Nut Specification"], ["nut_usage", "Nut Usage"], ["tool", "Tool"], ["min", "Min"], ["max", "Max"], ["unit", "Unit"], ["model_page", "Model File"], ["page", "Page"]].map(([name, label]) => <label className="text-sm font-bold text-slate-700 dark:text-slate-200" key={name}>{label}<input className={`${inputClass} mt-2 w-full`} defaultValue={values[name] ?? ""} name={name} required={name === "item"} type={["item", "item_check", "nut_spec", "unit", "model_page"].includes(name) ? "text" : "number"} /></label>);
          })()}
          <label className="text-sm font-bold text-slate-700 dark:text-slate-200">Tool Type<select className={`${inputClass} mt-2 w-full`} defaultValue={editingRow?.tool_type || "Visual Inspect"} name="tool_type"><option>Visual Inspect</option><option>Nut Runner</option></select></label>
        </div>
        <div className="flex justify-end gap-3 border-t border-slate-200 px-6 py-5 dark:border-slate-800"><button className="h-10 rounded-lg border border-slate-300 px-5 text-sm font-bold text-slate-700 dark:border-slate-700 dark:text-slate-200" disabled={busy} onClick={() => setEditingRow(null)} type="button">Cancel</button><button className="h-10 rounded-lg bg-brand-500 px-5 text-sm font-bold text-white disabled:bg-brand-300" disabled={busy} type="submit">{busy ? "Saving..." : "Save"}</button></div>
      </form>
    </Modal>
    </>
  );
}
