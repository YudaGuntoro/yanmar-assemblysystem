"use client";

import { FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import ActionIconButton, { AddActionIcon } from "@/components/common/ActionIconButton";
import { ConfirmModal, Modal } from "@/components/ui/modal";
import { apiGet, apiPost, apiRequest } from "@/lib/api";
import type { AssemblyTool, AssemblyWorkstation } from "./types";

type WorkstationModalState = { mode: "create" | "edit"; item?: AssemblyWorkstation } | null;
type ToolModalState = { mode: "create" | "edit"; workstationId?: number; item?: AssemblyTool } | null;
type DeleteTarget =
  | { kind: "workstation"; id: number; label: string }
  | { kind: "tool"; id: number; label: string }
  | null;

const inputClass = "mt-2 h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm font-semibold text-slate-800 outline-none transition placeholder:text-slate-400 focus:border-brand-400 focus:ring-3 focus:ring-brand-500/15 dark:border-slate-700 dark:bg-slate-900 dark:text-white";
const labelClass = "text-sm font-bold text-slate-700 dark:text-slate-200";
const secondaryButtonClass = "h-8 rounded-md border border-slate-200 px-2.5 text-xs font-bold text-slate-600 transition hover:border-slate-300 hover:bg-slate-50 disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800";
const iconButtonClass = "inline-flex size-9 shrink-0 items-center justify-center rounded-md border border-slate-200 text-slate-600 transition hover:border-slate-300 hover:bg-slate-50 disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800";

function formatNumber(value?: number | null) {
  if (value === null || value === undefined) return "-";

  return new Intl.NumberFormat("en-US", {
    maximumFractionDigits: 2,
    minimumFractionDigits: Number.isInteger(value) ? 0 : 2,
  }).format(value);
}

function toNumber(value: FormDataEntryValue | null) {
  const parsed = Number(value ?? 0);
  return Number.isFinite(parsed) ? parsed : 0;
}

function optionalNumber(value: FormDataEntryValue | null) {
  if (value === null || String(value).trim() === "") return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function torqueRange(tool: AssemblyTool) {
  return `${formatNumber(tool.torque_min)} - ${formatNumber(tool.torque_max)} ${tool.unit || "N.m"}`;
}

export default function WorkstationPage() {
  const [items, setItems] = useState<AssemblyWorkstation[]>([]);
  const [message, setMessage] = useState<{ kind: "ok" | "error"; text: string } | null>(null);
  const [busy, setBusy] = useState(false);
  const [workstationModal, setWorkstationModal] = useState<WorkstationModalState>(null);
  const [toolModal, setToolModal] = useState<ToolModalState>(null);
  const [deleteTarget, setDeleteTarget] = useState<DeleteTarget>(null);

  const totals = useMemo(() => {
    const tools = items.reduce((count, workstation) => count + workstation.tools.length, 0);
    return { tools, workstations: items.length };
  }, [items]);

  const load = useCallback(async () => {
    setBusy(true);
    setMessage(null);

    try {
      setItems(await apiGet<AssemblyWorkstation[]>("/api/leaktester/assembly-workstations?status=active"));
    } catch (err) {
      setMessage({ kind: "error", text: err instanceof Error ? err.message : "Failed to load workstation master." });
    } finally {
      setBusy(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  async function submitWorkstation(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const formElement = event.currentTarget;
    const form = new FormData(formElement);
    const payload = {
      workstation_code: form.get("workstation_code"),
      workstation_name: form.get("workstation_name"),
      workstation_no: toNumber(form.get("workstation_no")),
      description: form.get("description"),
      is_deleted: form.get("is_active") !== "on",
    };

    setBusy(true);
    setMessage(null);
    try {
      if (workstationModal?.mode === "edit" && workstationModal.item) {
        await apiRequest<AssemblyWorkstation>(`/api/leaktester/assembly-workstations/${workstationModal.item.id}`, {
          body: JSON.stringify(payload),
          method: "PUT",
        });
      } else {
        await apiPost<AssemblyWorkstation>("/api/leaktester/assembly-workstations", payload);
      }

      setWorkstationModal(null);
      formElement.reset();
      setMessage({ kind: "ok", text: workstationModal?.mode === "edit" ? "Workstation updated." : "Workstation saved." });
      await load();
    } catch (err) {
      setMessage({ kind: "error", text: err instanceof Error ? err.message : "Failed to save workstation." });
    } finally {
      setBusy(false);
    }
  }

  async function submitTool(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const formElement = event.currentTarget;
    const form = new FormData(formElement);
    const payload = {
      workstation_id: toNumber(form.get("workstation_id")),
      tool_code: form.get("tool_code"),
      tool_name: form.get("tool_name"),
      nut_size: form.get("nut_size"),
      program_no: optionalNumber(form.get("program_no")),
      torque_standard: toNumber(form.get("torque_standard")),
      torque_min: toNumber(form.get("torque_min")),
      torque_max: toNumber(form.get("torque_max")),
      unit: form.get("unit") || "N.m",
      sequence_no: toNumber(form.get("sequence_no")),
      is_deleted: form.get("is_active") !== "on",
    };

    setBusy(true);
    setMessage(null);
    try {
      if (toolModal?.mode === "edit" && toolModal.item) {
        await apiRequest<AssemblyTool>(`/api/leaktester/assembly-tools/${toolModal.item.id}`, {
          body: JSON.stringify(payload),
          method: "PUT",
        });
      } else {
        await apiPost<AssemblyTool>("/api/leaktester/assembly-tools", payload);
      }

      setToolModal(null);
      formElement.reset();
      setMessage({ kind: "ok", text: toolModal?.mode === "edit" ? "Tool updated." : "Tool saved." });
      await load();
    } catch (err) {
      setMessage({ kind: "error", text: err instanceof Error ? err.message : "Failed to save tool." });
    } finally {
      setBusy(false);
    }
  }

  async function confirmDelete() {
    if (!deleteTarget) return;

    const path =
      deleteTarget.kind === "workstation"
        ? `/api/leaktester/assembly-workstations/${deleteTarget.id}`
        : `/api/leaktester/assembly-tools/${deleteTarget.id}`;

    setBusy(true);
    setMessage(null);
    try {
      await apiRequest(path, { method: "DELETE" });
      setMessage({ kind: "ok", text: `${deleteTarget.label} deleted.` });
      setDeleteTarget(null);
      await load();
    } catch (err) {
      setMessage({ kind: "error", text: err instanceof Error ? err.message : "Failed to delete data." });
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <div className="space-y-6">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <h1 className="text-xl font-black text-slate-900 dark:text-white">Tool Setting</h1>
          <div className="flex items-center gap-2 text-sm font-semibold text-slate-400 dark:text-slate-500">
            <span>Home</span>
            <span className="text-slate-500">&gt;</span>
            <span>Master Data</span>
            <span className="text-slate-500">&gt;</span>
            <span className="text-slate-900 dark:text-white">Tool Setting</span>
          </div>
        </div>

        {message ? (
          <div className={`rounded-md border px-4 py-3 text-sm font-medium ${message.kind === "ok" ? "border-emerald-200 bg-emerald-50 text-emerald-700" : "border-rose-200 bg-rose-50 text-rose-700"}`}>
            {message.text}
          </div>
        ) : null}

        <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <div className="flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between">
            <div>
              <p className="text-xs font-black uppercase tracking-[0.2em] text-brand-500">Assembly Master</p>
              <h2 className="mt-1 text-lg font-black text-slate-900 dark:text-white">Tool and Estic standard setting</h2>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <div className="grid grid-cols-2 overflow-hidden rounded-md border border-slate-200 text-center dark:border-slate-700">
                <div className="px-3 py-2">
                  <div className="text-sm font-black text-slate-900 dark:text-white">{totals.workstations}</div>
                  <div className="text-[11px] font-bold uppercase text-slate-400">Station</div>
                </div>
                <div className="border-l border-slate-200 px-3 py-2 dark:border-slate-700">
                  <div className="text-sm font-black text-slate-900 dark:text-white">{totals.tools}</div>
                  <div className="text-[11px] font-bold uppercase text-slate-400">Tool</div>
                </div>
              </div>
              <button className="h-10 rounded-lg bg-slate-900 px-4 text-sm font-bold text-white transition hover:bg-slate-800 disabled:bg-slate-400 dark:bg-white dark:text-slate-900" disabled={busy} onClick={() => setWorkstationModal({ mode: "create" })} type="button">
                Add Tool Setting
              </button>
            </div>
          </div>
        </section>

        {items.length === 0 ? (
          <section className="rounded-lg border border-dashed border-slate-300 bg-white px-5 py-12 text-center dark:border-slate-700 dark:bg-slate-900">
            <p className="text-sm font-bold text-slate-500 dark:text-slate-300">No tool setting data.</p>
          </section>
        ) : null}

        <div className="space-y-5">
          {items.map((workstation) => (
            <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900" key={workstation.id}>
              <div className="flex flex-col gap-3 border-l-4 border-l-brand-500 border-b border-slate-200 px-5 py-4 dark:border-b-slate-800 md:flex-row md:items-center md:justify-between">
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="rounded-md bg-slate-100 px-2.5 py-1 text-xs font-black text-slate-700 dark:bg-slate-800 dark:text-slate-200">{workstation.workstation_code}</span>
                    <span className={`rounded-md px-2.5 py-1 text-xs font-black ${workstation.is_deleted ? "bg-slate-100 text-slate-500 dark:bg-slate-800 dark:text-slate-300" : "bg-emerald-50 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-300"}`}>
                      {workstation.is_deleted ? "DELETED" : "ACTIVE"}
                    </span>
                  </div>
                  <h2 className="mt-2 text-lg font-black text-slate-900 dark:text-white">{workstation.workstation_name}</h2>
                  {workstation.description ? (
                    <p className="mt-1 text-sm font-medium text-slate-500 dark:text-slate-400">{workstation.description}</p>
                  ) : null}
                </div>
                <div className="flex flex-wrap items-center gap-3 md:justify-end">
                  <div className="flex flex-wrap items-center gap-1">
                    <ActionIconButton aria-label={`Edit ${workstation.workstation_name}`} disabled={busy} icon="edit" onClick={() => setWorkstationModal({ mode: "edit", item: workstation })} title="Edit" />
                    {!workstation.is_deleted ? (
                      <ActionIconButton aria-label={`Delete ${workstation.workstation_name}`} disabled={busy} icon="delete" onClick={() => setDeleteTarget({ kind: "workstation", id: workstation.id, label: workstation.workstation_name })} title="Delete" />
                    ) : null}
                  </div>
                  <div className="min-w-24 text-center">
                    <div className="text-xs font-bold uppercase text-slate-400">Station No</div>
                    <div className="text-2xl font-black text-brand-500">{workstation.workstation_no}</div>
                  </div>
                </div>
              </div>

              <div className="px-5 py-4">
                <div className="mb-4 flex justify-end">
                  <div className="flex flex-wrap items-center gap-1">
                    <span className="rounded-md border border-slate-200 px-2.5 py-1 text-xs font-bold text-slate-500 dark:border-slate-700 dark:text-slate-300">{workstation.tools.length} Tool</span>
                    <button aria-label={`Add tool to ${workstation.workstation_name}`} className={iconButtonClass} disabled={busy} onClick={() => setToolModal({ mode: "create", workstationId: workstation.id })} title="Add Tool" type="button">
                      <AddActionIcon className="size-5" />
                    </button>
                  </div>
                </div>

                <div className="overflow-x-auto rounded-md border border-slate-200 dark:border-slate-800">
                      <table className="w-full min-w-[900px] table-fixed text-left text-sm">
                        <colgroup>
                          <col className="w-1/6" />
                          <col className="w-1/6" />
                          <col className="w-1/6" />
                          <col className="w-1/6" />
                          <col className="w-1/6" />
                          <col className="w-1/6" />
                        </colgroup>
                        <thead className="border-b border-slate-200 bg-slate-50 text-xs uppercase text-slate-500 dark:border-slate-800 dark:bg-slate-800/60 dark:text-slate-300">
                          <tr>
                            <th className="px-4 py-2.5 font-black">Tool</th>
                            <th className="px-4 py-2.5 font-black">Nut</th>
                            <th className="px-4 py-2.5 font-black">Program No</th>
                            <th className="px-4 py-2.5 font-black">Torque Standard</th>
                            <th className="px-4 py-2.5 font-black">Torque Limit</th>
                            <th className="px-4 py-2.5 text-right font-black">Action</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                          {workstation.tools.length === 0 ? (
                            <tr>
                              <td className="px-4 py-5 text-center font-semibold text-slate-400" colSpan={6}>No tool data for this process.</td>
                            </tr>
                          ) : (
                            workstation.tools.map((tool) => (
                              <tr className="bg-white align-top transition hover:bg-slate-50/80 dark:bg-slate-900 dark:hover:bg-slate-800/60" key={tool.id}>
                                <td className="px-4 py-3">
                                  <div className="font-black text-slate-900 dark:text-white">{tool.tool_code}</div>
                                  <div className="text-xs font-semibold text-slate-500 dark:text-slate-400">{tool.tool_name}</div>
                                </td>
                                <td className="px-4 py-3">
                                  <span className="rounded-md bg-slate-100 px-2.5 py-1 text-xs font-black text-slate-700 dark:bg-slate-800 dark:text-slate-200">{tool.nut_size}</span>
                                </td>
                                <td className="px-4 py-3 font-bold text-slate-700 dark:text-slate-200">{tool.program_no ?? "-"}</td>
                                <td className="px-4 py-3 font-bold text-slate-700 dark:text-slate-200">{formatNumber(tool.torque_standard)} {tool.unit || "N.m"}</td>
                                <td className="px-4 py-3 font-bold text-slate-700 dark:text-slate-200">{torqueRange(tool)}</td>
                                <td className="px-4 py-3">
                                  <div className="flex justify-end gap-1">
                                    <ActionIconButton aria-label={`Edit ${tool.tool_code}`} disabled={busy} icon="edit" onClick={() => setToolModal({ mode: "edit", item: tool, workstationId: workstation.id })} title="Edit" />
                                    {!tool.is_deleted ? (
                                      <ActionIconButton aria-label={`Delete ${tool.tool_code}`} disabled={busy} icon="delete" onClick={() => setDeleteTarget({ kind: "tool", id: tool.id, label: tool.tool_code })} title="Delete" />
                                    ) : null}
                                  </div>
                                </td>
                              </tr>
                            ))
                          )}
                        </tbody>
                      </table>
                </div>
              </div>
            </section>
          ))}
        </div>
      </div>

      <Modal className="mx-4 max-w-[560px] p-0" isOpen={Boolean(workstationModal)} onClose={() => !busy && setWorkstationModal(null)} showCloseButton={false}>
        <form onSubmit={(event) => void submitWorkstation(event)}>
          <div className="border-b border-slate-200 px-6 py-5 dark:border-slate-800">
            <h2 className="text-lg font-black text-slate-900 dark:text-white">{workstationModal?.mode === "edit" ? "Update Tool Setting" : "Add Tool Setting"}</h2>
          </div>
          <div className="grid gap-4 px-6 py-5 sm:grid-cols-2">
            <label className={labelClass}>Code<input className={inputClass} defaultValue={workstationModal?.item?.workstation_code ?? ""} name="workstation_code" placeholder="WKS-01" required /></label>
            <label className={labelClass}>No<input className={inputClass} defaultValue={workstationModal?.item?.workstation_no ?? ""} min={1} name="workstation_no" type="number" required /></label>
            <label className={`${labelClass} sm:col-span-2`}>Name<input className={inputClass} defaultValue={workstationModal?.item?.workstation_name ?? ""} name="workstation_name" placeholder="Workstation 1" required /></label>
            <label className={`${labelClass} sm:col-span-2`}>Description<textarea className={`${inputClass} h-24 resize-y py-3`} defaultValue={workstationModal?.item?.description ?? ""} name="description" /></label>
            <label className="flex items-center gap-2 text-sm font-bold text-slate-700 dark:text-slate-200"><input defaultChecked={!workstationModal?.item?.is_deleted} name="is_active" type="checkbox" />Active</label>
          </div>
          <ModalFooter busy={busy} onCancel={() => setWorkstationModal(null)} />
        </form>
      </Modal>

      <Modal className="mx-4 max-w-[760px] p-0" isOpen={Boolean(toolModal)} onClose={() => !busy && setToolModal(null)} showCloseButton={false}>
        <form onSubmit={(event) => void submitTool(event)}>
          <div className="border-b border-slate-200 px-6 py-5 dark:border-slate-800">
            <h2 className="text-lg font-black text-slate-900 dark:text-white">{toolModal?.mode === "edit" ? "Update Tool" : "Add Tool"}</h2>
          </div>
          <div className="grid gap-4 px-6 py-5 sm:grid-cols-3">
            <label className={`${labelClass} sm:col-span-3`}>Process<select className={inputClass} defaultValue={toolModal?.item?.workstation_id ?? toolModal?.workstationId ?? items[0]?.id ?? ""} name="workstation_id" required>{items.map((workstation) => <option key={workstation.id} value={workstation.id}>{workstation.workstation_code} - {workstation.workstation_name}</option>)}</select></label>
            <label className={labelClass}>Tool Code<input className={inputClass} defaultValue={toolModal?.item?.tool_code ?? ""} name="tool_code" placeholder="ESTIC-01A" required /></label>
            <label className={`${labelClass} sm:col-span-2`}>Tool Name<input className={inputClass} defaultValue={toolModal?.item?.tool_name ?? ""} name="tool_name" placeholder="Estic Nut Runner 01A" required /></label>
            <label className={labelClass}>Nut Size<input className={inputClass} defaultValue={toolModal?.item?.nut_size ?? ""} name="nut_size" placeholder="M8" required /></label>
            <label className={labelClass}>Unit<input className={inputClass} defaultValue={toolModal?.item?.unit ?? "N.m"} name="unit" /></label>
            <label className={labelClass}>Sequence<input className={inputClass} defaultValue={toolModal?.item?.sequence_no ?? 10} name="sequence_no" type="number" /></label>
            <label className={labelClass}>Program No<input className={inputClass} defaultValue={toolModal?.item?.program_no ?? ""} inputMode="numeric" name="program_no" placeholder="9" /></label>
            <label className={labelClass}>Torque Standard<input className={inputClass} defaultValue={toolModal?.item?.torque_standard ?? ""} inputMode="decimal" name="torque_standard" placeholder="24" required /></label>
            <label className={labelClass}>Torque Min<input className={inputClass} defaultValue={toolModal?.item?.torque_min ?? ""} inputMode="decimal" name="torque_min" placeholder="22" required /></label>
            <label className={labelClass}>Torque Max<input className={inputClass} defaultValue={toolModal?.item?.torque_max ?? ""} inputMode="decimal" name="torque_max" placeholder="26" required /></label>
            <label className="flex items-center gap-2 text-sm font-bold text-slate-700 dark:text-slate-200"><input defaultChecked={!toolModal?.item?.is_deleted} name="is_active" type="checkbox" />Active</label>
          </div>
          <ModalFooter busy={busy} onCancel={() => setToolModal(null)} />
        </form>
      </Modal>

      <ConfirmModal
        cancelText="Cancel"
        confirmText="Yes, Delete"
        isDestructive
        isLoading={busy}
        isOpen={Boolean(deleteTarget)}
        message={deleteTarget ? `Delete ${deleteTarget.label}? Data will be marked as deleted and hidden from active lists.` : ""}
        onClose={() => !busy && setDeleteTarget(null)}
        onConfirm={() => void confirmDelete()}
        title="Delete Master Data?"
      />
    </>
  );
}

function ModalFooter({ busy, onCancel }: { busy: boolean; onCancel: () => void }) {
  return (
    <div className="flex justify-end gap-3 border-t border-slate-200 px-6 py-5 dark:border-slate-800">
      <button className="h-10 rounded-lg border border-slate-300 px-5 text-sm font-bold text-slate-700 transition hover:bg-slate-50 disabled:opacity-60 dark:border-slate-700 dark:text-slate-200" disabled={busy} onClick={onCancel} type="button">
        Cancel
      </button>
      <button className="h-10 rounded-lg bg-brand-500 px-5 text-sm font-bold text-white transition hover:bg-brand-600 disabled:bg-brand-300" disabled={busy} type="submit">
        {busy ? "Saving..." : "Save"}
      </button>
    </div>
  );
}
