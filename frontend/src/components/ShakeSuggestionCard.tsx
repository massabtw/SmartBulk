"use client";

import { useState } from "react";
import { Coffee, ChevronDown, ChevronUp, Sparkles, CheckCircle2 } from "lucide-react";
import { Card } from "@/components/Card";
import { ShakeAdjustmentModel } from "@/types/api";

interface ShakeSuggestionCardProps {
  adjustment: ShakeAdjustmentModel | null;
  loading?: boolean;
  /** Mensagem do backend quando não há sugestão (ex.: nenhum template de shake). */
  message?: string | null;
}

export function ShakeSuggestionCard({ adjustment, loading = false, message }: ShakeSuggestionCardProps) {
  const [expanded, setExpanded] = useState(false);

  if (loading) {
    return (
      <Card className="p-4 animate-pulse">
        <div className="h-4 bg-zinc-800 rounded w-1/3 mb-2" />
        <div className="h-6 bg-zinc-800 rounded w-2/3" />
      </Card>
    );
  }

  if (!adjustment) {
    return (
      <Card className="p-4 border-dashed border-zinc-800 text-center">
        <Coffee className="w-6 h-6 text-zinc-600 mx-auto mb-1" />
        <p className="text-xs text-zinc-400">
          {message ?? "Sem sugestão de shake disponível."}
        </p>
      </Card>
    );
  }

  const ingredients = adjustment.ingredients ?? [];

  return (
    <Card className="bg-gradient-to-br from-zinc-900 to-zinc-950 border-zinc-800 overflow-hidden">
      <div className="flex items-center justify-between pb-3 border-b border-zinc-800/80">
        <div className="flex items-center gap-2.5">
          <div className="w-9 h-9 rounded-xl bg-amber-500/10 text-amber-400 flex items-center justify-center">
            <Coffee size={18} />
          </div>
          <div>
            <div className="flex items-center gap-1.5">
              <span className="text-xs font-semibold text-zinc-200">Shake de hoje</span>
              <span className="px-1.5 py-0.5 rounded text-[10px] font-bold bg-amber-500/15 text-amber-400 border border-amber-500/30">
                {adjustment.scaleFactor.toFixed(2)}x
              </span>
            </div>
            <p className="text-[11px] text-zinc-400">
              {adjustment.noAdjustmentNeeded
                ? "Meta do dia já está coberta"
                : "Ajustado para fechar o Gap"}
            </p>
          </div>
        </div>

        <button
          onClick={() => setExpanded(!expanded)}
          className="text-zinc-500 hover:text-zinc-300 p-1 rounded-lg transition-colors"
          aria-label={expanded ? "Recolher detalhes" : "Expandir detalhes"}
          aria-expanded={expanded}
        >
          {expanded ? <ChevronUp size={18} /> : <ChevronDown size={18} />}
        </button>
      </div>

      <div className="grid grid-cols-2 gap-3 py-3 text-xs">
        <div>
          <span className="text-zinc-500 text-[10px] uppercase font-medium">Calorias do shake</span>
          <p className="text-base font-bold text-amber-400 mt-0.5">
            {Math.round(adjustment.scaledCalories)} kcal
          </p>
        </div>
        <div>
          <span className="text-zinc-500 text-[10px] uppercase font-medium">Proteína do shake</span>
          <p className="text-base font-bold text-sky-400 mt-0.5">
            {Math.round(adjustment.scaledProtein)}g
          </p>
        </div>
      </div>

      {adjustment.noAdjustmentNeeded ? (
        <div className="p-2.5 rounded-xl bg-emerald-500/10 border border-emerald-500/20 text-[11px] text-emerald-300 flex items-center gap-2">
          <CheckCircle2 size={16} className="shrink-0" />
          <span>Nenhum ajuste necessário.</span>
        </div>
      ) : (
        <p className="text-[11px] text-zinc-400">
          Gap restante após o shake:{" "}
          <span className="text-zinc-200 font-semibold">
            {Math.round(adjustment.resultingCalorieGap)} kcal
          </span>{" "}
          •{" "}
          <span className="text-zinc-200 font-semibold">
            {Math.round(adjustment.resultingProteinGap)}g prot
          </span>
        </p>
      )}

      {adjustment.extraItem && (
        <div className="mt-2 p-2.5 rounded-xl bg-amber-500/10 border border-amber-500/20 text-[11px] text-amber-200 flex items-start gap-2">
          <Sparkles size={14} className="shrink-0 mt-0.5 text-amber-400" />
          <span>
            <span className="font-semibold">
              Extra: ~{Math.round(adjustment.extraItem.grams)}g de {adjustment.extraItem.foodName}
            </span>
            {adjustment.extraItem.reason ? ` — ${adjustment.extraItem.reason}` : ""}
          </span>
        </div>
      )}

      {expanded && ingredients.length > 0 && (
        <div className="pt-3 mt-2 border-t border-zinc-800/80 space-y-1.5">
          <p className="text-[10px] font-semibold text-zinc-400 uppercase tracking-wider">
            Ingredientes ajustados
          </p>
          {ingredients.map((item) => (
            <div
              key={item.foodUuid}
              className="flex items-center justify-between text-xs py-1 px-2 rounded-lg bg-zinc-950/50 border border-zinc-900"
            >
              <span className="text-zinc-300">{item.foodName}</span>
              <div className="flex items-center gap-2">
                <span className="text-zinc-500 line-through text-[11px]">
                  {Math.round(item.originalGrams)}g
                </span>
                <span className="font-bold text-amber-400 text-xs">{Math.round(item.scaledGrams)}g</span>
              </div>
            </div>
          ))}
        </div>
      )}
    </Card>
  );
}
