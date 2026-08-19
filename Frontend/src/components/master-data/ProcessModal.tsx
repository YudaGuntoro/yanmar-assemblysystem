"use client";

import React, { useEffect, useState } from "react";
import { Modal } from "@/components/ui/modal";
import { useToast } from "@/context/ToastContext";
import ProcessService, {
  Process,
  ProcessPayload,
} from "@/services/ProcessService";

interface ProcessModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  process: Process | null;
}

type ProcessFormData = ProcessPayload;

const initialFormData: ProcessFormData = {
  code: "",
  name: "",
  description: "",
  isActive: true,
  parameterIds: [],
};

const getErrorMessage = (error: unknown, fallback: string) =>
  error instanceof Error ? error.message : fallback;

export default function ProcessModal({
  isOpen,
  onClose,
  onSuccess,
  process,
}: ProcessModalProps) {
  const toast = useToast();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [formData, setFormData] =
    useState<ProcessFormData>(initialFormData);

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    if (process) {
      setFormData({
        code: process.code || "",
        name: process.name || "",
        description: process.description || "",
        isActive: process.isActive ?? true,
        parameterIds: [],
      });
      return;
    }

    setFormData(initialFormData);
  }, [isOpen, process]);

  const handleChange = (
    event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>
  ) => {
    const { name, type, value } = event.target;
    const checked = (event.target as HTMLInputElement).checked;

    setFormData((current) => ({
      ...current,
      [name]: type === "checkbox" ? checked : value,
    }));
  };

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setIsSubmitting(true);

    try {
      if (process) {
        await ProcessService.updateProcess(process.id, formData);
        toast.success({
          title: "Success",
          message: "Process updated successfully",
        });
      } else {
        await ProcessService.createProcess(formData);
        toast.success({
          title: "Success",
          message: "Process created successfully",
        });
      }

      onSuccess();
      onClose();
    } catch (error: unknown) {
      toast.error({
        title: "Error",
        message: getErrorMessage(
          error,
          `Failed to ${process ? "update" : "create"} process`
        ),
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal isOpen={isOpen} onClose={onClose} className="max-w-[640px] p-6">
      <h3 className="mb-4 text-xl font-semibold text-gray-800 dark:text-white/90">
        {process ? "Update Process" : "Create Process"}
      </h3>

      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        <div>
          <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
            Code
          </label>
          <input
            type="text"
            name="code"
            value={formData.code}
            onChange={handleChange}
            required
            className="h-10 w-full rounded-lg border border-gray-300 bg-transparent px-3 py-2 text-sm text-gray-800 outline-none placeholder:text-gray-400 focus:border-brand-300 focus:ring-3 focus:ring-brand-500/10 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90"
            placeholder="Enter process code"
          />
        </div>

        <div>
          <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
            Name
          </label>
          <input
            type="text"
            name="name"
            value={formData.name}
            onChange={handleChange}
            required
            className="h-10 w-full rounded-lg border border-gray-300 bg-transparent px-3 py-2 text-sm text-gray-800 outline-none placeholder:text-gray-400 focus:border-brand-300 focus:ring-3 focus:ring-brand-500/10 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90"
            placeholder="Enter process name"
          />
        </div>

        <div>
          <label className="mb-1.5 block text-sm font-medium text-gray-700 dark:text-gray-300">
            Description
          </label>
          <textarea
            name="description"
            value={formData.description}
            onChange={handleChange}
            rows={3}
            className="w-full rounded-lg border border-gray-300 bg-transparent px-3 py-2 text-sm text-gray-800 outline-none placeholder:text-gray-400 focus:border-brand-300 focus:ring-3 focus:ring-brand-500/10 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90"
            placeholder="Enter process description"
          />
        </div>

        <div className="flex items-center gap-2">
          <input
            type="checkbox"
            id="process-is-active"
            name="isActive"
            checked={formData.isActive}
            onChange={handleChange}
            className="h-4 w-4 rounded border-gray-300 text-brand-500 focus:ring-brand-500"
          />
          <label
            htmlFor="process-is-active"
            className="text-sm font-medium text-gray-700 dark:text-gray-300"
          >
            Active
          </label>
        </div>

        <div className="mt-4 flex justify-end gap-3">
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50 dark:border-gray-700 dark:text-gray-300 dark:hover:bg-gray-800"
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={isSubmitting}
            className="rounded-lg bg-brand-500 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-brand-600 focus:outline-none focus:ring-2 focus:ring-brand-500/50 disabled:opacity-50"
          >
            {isSubmitting ? "Saving..." : "Save"}
          </button>
        </div>
      </form>
    </Modal>
  );
}
