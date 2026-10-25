"use client";

import { useState } from "react";
import { Utensils, Trash2, ChevronDown, ChevronUp } from "lucide-react";
import { Card } from "@/components/Card";
import { bffSend } from "@/lib/bff";
import { MealLogModel } from "@/types/api";

interface MealHistoryListProps {
  meals: MealLogModel[];
  onMealDeleted?: () => void;
}

export function MealHistoryList({ meals, onMealDeleted }: MealHistoryListProps) {
  const [expandedUuid, setExpandedUuid] = useState<string | null>(null);
  const [deletingUuid, setDeletingUuid] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const handleDelete = async (uuid: string) => {
    if (deletingUuid) return;
    setDeletingUuid(uuid);
    setError(null);

    const res = await bffSend<unknown>("DELETE", `meallog/${uuid}`);
    setDeletingUuid(null);

    if (res.error) {
      setError(res.error);
      return;
    }
    onMealDeleted?.();
  };

  if (!meals || meals.length === 0) {
    return (
      <Card className="p-6 text-center border-dashed border-zinc-800">
        <Utensils className="w-8 h-8 text-zinc-600 mx-auto mb-2 opacity-50" />
        <p className="text-xs font-medium text-zinc-300">Nenhuma refeição registrada</p>
        <p className="text-[11px] text-zinc-500 mt-0.5">
          Envie pelo Telegram para registrar. O que já está garantido no refeitório conta no Gap.
        </p>
      </Card>
    );
  }

  return (
    <div className="space-y-2.5">
      <h3 className="px-1 text-xs font-semibold text-zinc-400 uppercase tracking-wider">
        Refeições do dia ({meals.length})
      </h3>

      {error && (
        <p className="text-xs text-rose-400 bg-rose-950/20 p-2.5 rounded-xl border border-rose-500/20">
          {error}
        </p>
      )}

      {meals.map((meal) => {
        const isExpanded = expandedUuid === meal.uuid;
        const isDeleting = deletingUuid === meal.uuid;

        return (
          <Card
            key={meal.uuid}
            className={`p-3.5 transition-all ${isDeleting ? "opacity-50 pointer-events-none" : ""}`}
          >
            <div className="flex items-center justify-between">
              <button
                type="button"
                className="flex items-center gap-2.5 flex-1 min-w-0 text-left"
                onClick={() => setExpandedUuid(isExpanded ? null : meal.uuid)}
                aria-expanded={isExpanded}
              >
                <div className="w-8 h-8 rounded-xl bg-brand-500/10 text-brand-500 flex items-center justify-center font-bold text-xs shrink-0">
                  {(meal.name || "R").charAt(0).toUpperCase()}
                </div>
                <div className="min-w-0">
                  <p className="text-xs font-bold text-zinc-200 truncate">{meal.name}</p>
                  <div className="flex items-center gap-2 text-[11px] text-zinc-400 mt-0.5">
                    <span className="font-semibold text-zinc-200">
                      {Math.round(meal.totalCalories)} kcal
                    </span>
                    <span>•</span>
                    <span className="text-sky-400">{Math.round(meal.totalProtein)}g prot</span>
                  </div>
                </div>
              </button>

              <div className="flex items-center gap-1 shrink-0">
                <button
                  onClick={() => handleDelete(meal.uuid)}
                  title="Desfazer refeição"
                  aria-label={`Desfazer ${meal.name}`}
                  className="p-1.5 text-zinc-500 hover:text-rose-400 hover:bg-rose-500/10 rounded-lg transition-colors text-xs flex items-center gap-1"
                >
                  <Trash2 size={15} />
                  <span className="text-[10px]">Desfazer</span>
                </button>

                <button
                  onClick={() => setExpandedUuid(isExpanded ? null : meal.uuid)}
                  aria-label={isExpanded ? "Recolher" : "Expandir"}
                  className="p-1 text-zinc-500 hover:text-zinc-300 rounded-lg transition-colors"
                >
                  {isExpanded ? <ChevronUp size={16} /> : <ChevronDown size={16} />}
                </button>
              </div>
            </div>

            {isExpanded && meal.items && meal.items.length > 0 && (
              <div className="mt-3 pt-2.5 border-t border-zinc-800/80 space-y-1.5">
                {meal.items.map((item) => (
                  <div
                    key={item.uuid}
                    className="flex items-center justify-between text-xs py-1 px-2 rounded-lg bg-zinc-950/60"
                  >
                    <span className="text-zinc-300 truncate max-w-[170px]">{item.foodName}</span>
                    <div className="flex items-center gap-2 text-[11px] text-zinc-400">
                      <span>{Math.round(item.servingGrams)}g</span>
                      <span className="text-zinc-200 font-medium">{Math.round(item.calories)} kcal</span>
                      <span className="text-sky-400">{Math.round(item.protein)}g</span>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </Card>
        );
      })}
    </div>
  );
}
