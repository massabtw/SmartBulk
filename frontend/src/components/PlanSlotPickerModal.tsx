"use client";

import { X, ChefHat, Check } from "lucide-react";
import { Card } from "@/components/Card";
import { MealTemplateModel } from "@/types/api";

interface PlanSlotPickerModalProps {
  isOpen: boolean;
  onClose: () => void;
  dateStr: string;
  mealSlot: string;
  templates: MealTemplateModel[];
  onSelectTemplate: (templateUuid: string) => void;
}

const slotLabels: Record<string, string> = {
  Breakfast: "Café da manhã",
  Lunch: "Almoço",
  Dinner: "Jantar",
  Shake: "Shake",
};

/** Tipos de template que fazem sentido para cada refeição. */
function compatibleTypes(slot: string): string[] {
  switch (slot) {
    case "Breakfast":
      return ["Breakfast"];
    case "Shake":
      return ["Shake"];
    default:
      return ["FullMeal"];
  }
}

export function PlanSlotPickerModal({
  isOpen,
  onClose,
  dateStr,
  mealSlot,
  templates,
  onSelectTemplate,
}: PlanSlotPickerModalProps) {
  if (!isOpen) return null;

  const allowed = compatibleTypes(mealSlot);
  const filtered = templates.filter((t) => allowed.includes(t.mealType));

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="slot-title"
      className="fixed inset-0 z-50 bg-black/80 backdrop-blur-sm flex items-end sm:items-center justify-center p-0 sm:p-4"
    >
      <Card className="w-full max-w-md bg-zinc-900 border-zinc-800 rounded-t-3xl sm:rounded-2xl p-5 space-y-4 max-h-[85vh] overflow-y-auto">
        <div className="flex items-center justify-between pb-2 border-b border-zinc-800">
          <div>
            <h2 id="slot-title" className="text-base font-bold text-zinc-100">
              Escolher template
            </h2>
            <p className="text-xs text-zinc-400 mt-0.5">
              {slotLabels[mealSlot] ?? mealSlot} • {dateStr}
            </p>
          </div>
          <button
            onClick={onClose}
            aria-label="Fechar"
            className="p-1.5 rounded-lg text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800 transition-colors"
          >
            <X size={18} />
          </button>
        </div>

        <div className="space-y-2">
          {filtered.length === 0 ? (
            <div className="text-center py-8">
              <ChefHat className="w-8 h-8 text-zinc-600 mx-auto mb-2 opacity-50" />
              <p className="text-xs text-zinc-400">Nenhum template compatível com esta refeição.</p>
              <p className="text-[11px] text-zinc-500 mt-0.5">
                Crie um template do tipo{" "}
                {allowed.map((a) => (a === "Breakfast" ? "Café" : a === "Shake" ? "Shake" : "Completa")).join(" / ")} na
                aba &quot;Templates&quot;.
              </p>
            </div>
          ) : (
            filtered.map((t) => (
              <button
                key={t.uuid}
                type="button"
                onClick={() => {
                  onSelectTemplate(t.uuid);
                  onClose();
                }}
                className="w-full text-left p-3 rounded-xl bg-zinc-950 border border-zinc-800 hover:border-brand-500 transition-all flex items-center justify-between group"
              >
                <span>
                  <span className="block font-semibold text-xs text-zinc-200 group-hover:text-brand-300">{t.name}</span>
                  <span className="flex items-center gap-2 text-[11px] text-zinc-400 mt-1">
                    <span className="text-amber-400 font-semibold">{Math.round(t.totalCalories)} kcal</span>
                    <span>•</span>
                    <span className="text-sky-400 font-semibold">{Math.round(t.totalProtein)}g prot</span>
                  </span>
                </span>
                <span className="w-7 h-7 rounded-lg bg-zinc-900 group-hover:bg-brand-500/20 text-zinc-500 group-hover:text-brand-400 flex items-center justify-center transition-colors">
                  <Check size={14} />
                </span>
              </button>
            ))
          )}
        </div>
      </Card>
    </div>
  );
}
