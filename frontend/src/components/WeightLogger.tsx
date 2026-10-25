"use client";

import { useEffect, useState } from "react";
import { Dumbbell, Check, X } from "lucide-react";
import { Card } from "@/components/Card";
import { bffSend } from "@/lib/bff";
import { WeightLogModel } from "@/types/api";

interface WeightLoggerProps {
  latestWeight?: number | null;
  dateStr: string;
  onWeightSaved?: () => void;
}

export function WeightLogger({ latestWeight, dateStr, onWeightSaved }: WeightLoggerProps) {
  const [weight, setWeight] = useState<number | null>(latestWeight ?? null);
  const [isEditing, setIsEditing] = useState(false);
  const [inputValue, setInputValue] = useState(latestWeight ? String(latestWeight) : "");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setWeight(latestWeight ?? null);
  }, [latestWeight]);

  const handleSave = async () => {
    const parsed = parseFloat(inputValue.replace(",", "."));
    if (isNaN(parsed) || parsed < 30 || parsed > 300) {
      setError("Informe um peso entre 30 e 300 kg.");
      return;
    }

    setLoading(true);
    setError(null);
    const res = await bffSend<WeightLogModel>("POST", "weight", { date: dateStr, weightKg: parsed });
    setLoading(false);

    if (res.error) {
      setError(res.error);
      return;
    }

    setWeight(parsed);
    setIsEditing(false);
    onWeightSaved?.();
  };

  return (
    <Card className="p-3.5 transition-all duration-200">
      {!isEditing ? (
        <button
          type="button"
          onClick={() => {
            setInputValue(weight ? String(weight) : "");
            setError(null);
            setIsEditing(true);
          }}
          className="w-full flex items-center justify-between text-left"
        >
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-xl bg-blue-500/10 text-blue-400 flex items-center justify-center">
              <Dumbbell size={18} />
            </div>
            <div>
              <p className="text-xs font-semibold text-zinc-200">Peso</p>
              <p className="text-[11px] text-zinc-400">
                {weight ? `${weight.toFixed(1)} kg (último)` : "Toque para registrar"}
              </p>
            </div>
          </div>
        </button>
      ) : (
        <div className="space-y-2">
          <div className="flex items-center gap-2">
            <input
              type="number"
              step="0.1"
              autoFocus
              aria-label="Peso em quilos"
              value={inputValue}
              onChange={(e) => setInputValue(e.target.value)}
              placeholder="Ex: 78.5"
              className="flex-1 min-w-0 bg-zinc-950 border border-zinc-700 rounded-xl px-3 py-1.5 text-sm text-zinc-100 placeholder-zinc-600 focus:outline-none focus:border-blue-500"
              onKeyDown={(e) => {
                if (e.key === "Enter") handleSave();
                if (e.key === "Escape") setIsEditing(false);
              }}
            />
            <button
              onClick={handleSave}
              disabled={loading}
              aria-label="Salvar peso"
              className="w-8 h-8 rounded-xl bg-blue-600 hover:bg-blue-500 text-white flex items-center justify-center transition-colors disabled:opacity-50"
            >
              <Check size={16} />
            </button>
            <button
              onClick={() => setIsEditing(false)}
              aria-label="Cancelar"
              className="w-8 h-8 rounded-xl bg-zinc-800 hover:bg-zinc-700 text-zinc-400 flex items-center justify-center transition-colors"
            >
              <X size={16} />
            </button>
          </div>
          {error && <p className="text-[11px] text-rose-400">{error}</p>}
        </div>
      )}
    </Card>
  );
}
