"use client";

import { useState } from "react";
import { AlertCircle, Trash2, Edit2, Check, X, Replace } from "lucide-react";
import { Card } from "@/components/Card";
import { PlateProfileSelector } from "@/components/PlateProfileSelector";
import { FoodSearchPicker } from "@/components/FoodSearchPicker";
import { bffSend } from "@/lib/bff";
import { CafeteriaMealModel, CafeteriaMealItemModel, FoodModel } from "@/types/api";

interface MealReviewCardProps {
  meal: CafeteriaMealModel;
  /** Cardápio confirmado não pode ser editado (o backend recusa). */
  readOnly?: boolean;
  onItemUpdated?: () => void;
}

function multiplierFor(profile?: string) {
  const p = profile?.toLowerCase();
  if (p === "good") return 1.25;
  if (p === "poor") return 0.75;
  return 1.0;
}

export function MealReviewCard({ meal, readOnly = false, onItemUpdated }: MealReviewCardProps) {
  const [editingItemUuid, setEditingItemUuid] = useState<string | null>(null);
  const [swappingItemUuid, setSwappingItemUuid] = useState<string | null>(null);
  const [editGrams, setEditGrams] = useState<string>("");
  const [multiplier, setMultiplier] = useState<number>(() => multiplierFor(meal.plateProfile));
  const [error, setError] = useState<string | null>(null);

  const items = meal.items ?? [];
  const baseCalories = items.reduce((acc, i) => acc + (i.calories || 0), 0);
  const baseProtein = items.reduce((acc, i) => acc + (i.protein || 0), 0);
  const pendingCount = items.filter((i) => i.isPending).length;

  const updateItem = async (item: CafeteriaMealItemModel, body: Record<string, unknown>) => {
    setError(null);
    const res = await bffSend<unknown>("PUT", `cafeteriamenu/items/${item.uuid}`, body);
    if (res.error) {
      setError(res.error);
      return false;
    }
    onItemUpdated?.();
    return true;
  };

  const handleSaveGrams = async (item: CafeteriaMealItemModel) => {
    const parsed = parseFloat(editGrams.replace(",", "."));
    if (isNaN(parsed) || parsed <= 0) {
      setError("Informe uma quantidade em gramas maior que zero.");
      return;
    }
    const ok = await updateItem(item, {
      servingGrams: parsed,
      // Mantém o alimento já associado, se houver.
      foodUuid: item.foodUuid ?? undefined,
    });
    if (ok) setEditingItemUuid(null);
  };

  const handleSwapFood = async (item: CafeteriaMealItemModel, food: FoodModel) => {
    const ok = await updateItem(item, { servingGrams: item.servingGrams, foodUuid: food.uuid });
    if (ok) setSwappingItemUuid(null);
  };

  const handleDeleteItem = async (itemUuid: string) => {
    setError(null);
    const res = await bffSend<unknown>("DELETE", `cafeteriamenu/items/${itemUuid}`);
    if (res.error) {
      setError(res.error);
      return;
    }
    onItemUpdated?.();
  };

  return (
    <Card className="space-y-3 p-4 bg-zinc-900 border-zinc-800">
      <div className="flex items-center justify-between">
        <div>
          <div className="flex items-center gap-2">
            <h3 className="text-sm font-bold text-zinc-100">
              {meal.mealType === "Lunch" ? "Almoço" : "Jantar"}
            </h3>
            {pendingCount > 0 && (
              <span className="px-1.5 py-0.5 rounded text-[10px] font-semibold bg-amber-500/15 text-amber-400 border border-amber-500/30 flex items-center gap-1">
                <AlertCircle size={11} />
                {pendingCount} pendente(s)
              </span>
            )}
          </div>
          <p className="text-[11px] text-zinc-400 mt-0.5">
            Estimativa ({multiplier.toFixed(2)}x):{" "}
            <span className="text-amber-400 font-semibold">{Math.round(baseCalories * multiplier)} kcal</span>
            {" • "}
            <span className="text-sky-400 font-semibold">{Math.round(baseProtein * multiplier)}g proteína</span>
          </p>
        </div>
        <span className="text-xs text-zinc-500">{items.length} itens</span>
      </div>

      <div className="space-y-1">
        <span className="text-[10px] font-semibold text-zinc-500 uppercase tracking-wider">
          Perfil do prato
        </span>
        <PlateProfileSelector
          currentProfile={meal.plateProfile || "Standard"}
          date={meal.date}
          mealType={meal.mealType}
          onProfileChanged={(_, mult) => setMultiplier(mult)}
        />
      </div>

      {error && (
        <p role="alert" className="text-[11px] text-rose-400 bg-rose-950/20 p-2 rounded-lg border border-rose-500/20">
          {error}
        </p>
      )}

      <ul className="space-y-1.5 pt-1">
        {items.map((item) => {
          const isEditing = editingItemUuid === item.uuid;
          const isSwapping = swappingItemUuid === item.uuid;

          return (
            <li
              key={item.uuid}
              className={`p-2.5 rounded-xl border transition-all text-xs ${
                item.isPending
                  ? "bg-amber-950/20 border-amber-500/30 text-amber-200"
                  : "bg-zinc-950/60 border-zinc-800 text-zinc-200"
              }`}
            >
              <div className="flex items-center justify-between">
                <div className="flex-1 min-w-0 pr-2">
                  <div className="flex items-center gap-1.5">
                    <span className="font-semibold text-zinc-100 truncate">{item.rawName}</span>
                    {item.matchedFoodName ? (
                      <span className="text-[10px] text-zinc-500 truncate">({item.matchedFoodName})</span>
                    ) : (
                      <span className="text-[10px] text-amber-400">sem alimento associado</span>
                    )}
                  </div>
                  <div className="flex items-center gap-2 text-[11px] text-zinc-400 mt-0.5">
                    <span className="text-zinc-300 font-medium">{Math.round(item.servingGrams)}g</span>
                    <span>•</span>
                    <span>{Math.round(item.calories || 0)} kcal</span>
                    <span>•</span>
                    <span className="text-sky-400">{Math.round(item.protein || 0)}g prot</span>
                  </div>
                </div>

                {!readOnly && (
                  <div className="flex items-center gap-1 shrink-0">
                    {isEditing ? (
                      <div className="flex items-center gap-1">
                        <input
                          type="number"
                          autoFocus
                          aria-label="Gramas"
                          value={editGrams}
                          onChange={(e) => setEditGrams(e.target.value)}
                          onKeyDown={(e) => {
                            if (e.key === "Enter") handleSaveGrams(item);
                            if (e.key === "Escape") setEditingItemUuid(null);
                          }}
                          className="w-16 bg-zinc-900 border border-brand-500 rounded px-1.5 py-0.5 text-xs text-zinc-100 text-center"
                        />
                        <button
                          onClick={() => handleSaveGrams(item)}
                          aria-label="Salvar gramas"
                          className="p-1 text-emerald-400 hover:bg-emerald-500/20 rounded"
                        >
                          <Check size={14} />
                        </button>
                        <button
                          onClick={() => setEditingItemUuid(null)}
                          aria-label="Cancelar"
                          className="p-1 text-zinc-500 hover:bg-zinc-800 rounded"
                        >
                          <X size={14} />
                        </button>
                      </div>
                    ) : (
                      <>
                        <button
                          onClick={() => {
                            setEditGrams(String(Math.round(item.servingGrams)));
                            setEditingItemUuid(item.uuid);
                            setSwappingItemUuid(null);
                          }}
                          className="p-1 text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800 rounded transition-colors"
                          title="Editar gramas"
                          aria-label="Editar gramas"
                        >
                          <Edit2 size={13} />
                        </button>
                        <button
                          onClick={() => {
                            setSwappingItemUuid(isSwapping ? null : item.uuid);
                            setEditingItemUuid(null);
                          }}
                          className={`p-1 rounded transition-colors ${
                            item.isPending
                              ? "text-amber-300 bg-amber-500/15 hover:bg-amber-500/25"
                              : "text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800"
                          }`}
                          title={item.isPending ? "Associar alimento" : "Trocar alimento"}
                          aria-label={item.isPending ? "Associar alimento" : "Trocar alimento"}
                        >
                          <Replace size={13} />
                        </button>
                        <button
                          onClick={() => handleDeleteItem(item.uuid)}
                          className="p-1 text-zinc-500 hover:text-rose-400 hover:bg-rose-500/10 rounded transition-colors"
                          title="Excluir item"
                          aria-label="Excluir item"
                        >
                          <Trash2 size={13} />
                        </button>
                      </>
                    )}
                  </div>
                )}
              </div>

              {isSwapping && !readOnly && (
                <div className="mt-2 pt-2 border-t border-zinc-800">
                  <FoodSearchPicker
                    autoFocus
                    placeholder={`Alimento para "${item.rawName}"`}
                    onPick={(food) => handleSwapFood(item, food)}
                  />
                </div>
              )}
            </li>
          );
        })}
      </ul>
    </Card>
  );
}
