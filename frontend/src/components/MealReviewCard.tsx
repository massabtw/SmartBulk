"use client";

import { useEffect, useState } from "react";
import { AlertCircle, Trash2, Edit2, Check, X, Replace, Moon } from "lucide-react";
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

function multiplierFor(profile?: string, mealType?: string) {
  const p = profile?.toLowerCase() ?? "";
  if (p === "none" || p === "skip" || p.includes("não") || p.includes("nao")) return 0.0;
  if (p === "good" || p === "generous" || p.includes("bom")) return 1.25;
  if (p === "poor" || p === "small" || p.includes("ruim") || p.includes("reduzid")) return 0.75;
  if (p === "standard" || p.includes("padrão") || p.includes("padrao")) return 1.0;
  return mealType?.toLowerCase() === "dinner" ? 0.75 : 1.0;
}

export function MealReviewCard({ meal, readOnly = false, onItemUpdated }: MealReviewCardProps) {
  const isDinner = meal.mealType?.toLowerCase() === "dinner";
  const [editingItemUuid, setEditingItemUuid] = useState<string | null>(null);
  const [swappingItemUuid, setSwappingItemUuid] = useState<string | null>(null);
  const [editGrams, setEditGrams] = useState<string>("");
  const [activeProfile, setActiveProfile] = useState<string>(
    () => meal.plateProfile || (isDinner ? "Small" : "Standard")
  );
  const [multiplier, setMultiplier] = useState<number>(() =>
    multiplierFor(meal.plateProfile, meal.mealType)
  );
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const nextProf = meal.plateProfile || (isDinner ? "Small" : "Standard");
    setActiveProfile(nextProf);
    setMultiplier(multiplierFor(nextProf, meal.mealType));
  }, [meal.plateProfile, meal.mealType, isDinner]);

  const isSkipped = multiplier === 0;

  const items = meal.items ?? [];
  const baseCalories = items.reduce((acc, i) => acc + (i.calories || 0), 0);
  const baseProtein = items.reduce((acc, i) => acc + (i.protein || 0), 0);
  const pendingCount = items.filter((i) => i.isPending).length;

  const handleQuickDinnerToggle = async () => {
    const nextProfile = isSkipped ? "Small" : "None";
    const nextMult = isSkipped ? 0.75 : 0.0;
    const prevProfile = activeProfile;
    const prevMult = multiplier;

    setActiveProfile(nextProfile);
    setMultiplier(nextMult);
    setError(null);

    const res = await bffSend<unknown>("PUT", "cafeteriamenu/meals/profile", {
      date: meal.date,
      mealType: meal.mealType,
      plateProfile: nextProfile,
    });

    if (res.error) {
      setActiveProfile(prevProfile);
      setMultiplier(prevMult);
      setError(res.error);
    } else {
      onItemUpdated?.();
    }
  };

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
    <Card className={`space-y-3 p-4 transition-all ${isSkipped ? "bg-zinc-950/70 border-zinc-800/60 opacity-80" : "bg-zinc-900 border-zinc-800"}`}>
      <div className="flex items-center justify-between">
        <div>
          <div className="flex items-center gap-2">
            <h3 className="text-sm font-bold text-zinc-100 flex items-center gap-1.5">
              {isDinner ? <Moon size={14} className="text-indigo-400" /> : null}
              {isDinner ? "Jantar" : "Almoço"}
            </h3>
            {isDinner && (
              <span className="px-1.5 py-0.2 rounded text-[10px] font-semibold bg-indigo-500/15 text-indigo-300 border border-indigo-500/30">
                Padrão 0.75x
              </span>
            )}
            {pendingCount > 0 && (
              <span className="px-1.5 py-0.5 rounded text-[10px] font-semibold bg-amber-500/15 text-amber-400 border border-amber-500/30 flex items-center gap-1">
                <AlertCircle size={11} />
                {pendingCount} pendente(s)
              </span>
            )}
          </div>
          <p className="text-[11px] text-zinc-400 mt-0.5">
            Estimativa ({multiplier.toFixed(2)}x):{" "}
            <span className={isSkipped ? "text-zinc-500 line-through" : "text-amber-400 font-semibold"}>
              {Math.round(baseCalories * multiplier)} kcal
            </span>
            {" • "}
            <span className={isSkipped ? "text-zinc-500 line-through" : "text-sky-400 font-semibold"}>
              {Math.round(baseProtein * multiplier)}g proteína
            </span>
          </p>
        </div>

        <div className="flex items-center gap-2">
          {isDinner && (
            <button
              type="button"
              onClick={handleQuickDinnerToggle}
              className={`px-2.5 py-1 rounded-xl text-xs font-semibold transition-all flex items-center gap-1 shadow-sm ${
                isSkipped
                  ? "bg-rose-950/40 border border-rose-500/40 text-rose-300 hover:bg-rose-900/50"
                  : "bg-zinc-800 border border-zinc-700/80 text-zinc-300 hover:bg-zinc-700 hover:text-white"
              }`}
            >
              {isSkipped ? "Hoje não jantei lá (0.0x)" : "Não vou jantar lá"}
            </button>
          )}
          <span className="text-xs text-zinc-500">{items.length} itens</span>
        </div>
      </div>

      {isSkipped && (
        <div className="p-2.5 rounded-xl bg-rose-950/20 border border-rose-500/30 text-rose-300 text-xs flex items-center justify-between gap-2">
          <span>Refeição pulada: calorias e proteínas voltam integralmente para o seu Gap do dia.</span>
          <button
            type="button"
            onClick={handleQuickDinnerToggle}
            className="underline text-[11px] text-rose-400 hover:text-rose-200 shrink-0 font-medium"
          >
            Reativar
          </button>
        </div>
      )}

      <div className="space-y-1">
        <span className="text-[10px] font-semibold text-zinc-500 uppercase tracking-wider">
          Perfil do prato
        </span>
        <PlateProfileSelector
          currentProfile={activeProfile}
          date={meal.date}
          mealType={meal.mealType}
          onProfileChanged={(prof, mult) => {
            setActiveProfile(prof);
            setMultiplier(mult);
            onItemUpdated?.();
          }}
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
                          className="w-16 px-1.5 py-0.5 bg-zinc-900 border border-zinc-700 rounded text-center text-xs text-zinc-100"
                        />
                        <button
                          onClick={() => handleSaveGrams(item)}
                          className="p-1 text-emerald-400 hover:text-emerald-300"
                          title="Salvar"
                        >
                          <Check size={14} />
                        </button>
                        <button
                          onClick={() => setEditingItemUuid(null)}
                          className="p-1 text-zinc-400 hover:text-zinc-300"
                          title="Cancelar"
                        >
                          <X size={14} />
                        </button>
                      </div>
                    ) : (
                      <>
                        <button
                          onClick={() => {
                            setEditingItemUuid(item.uuid);
                            setEditGrams(String(Math.round(item.servingGrams)));
                          }}
                          className="p-1 text-zinc-400 hover:text-zinc-200"
                          title="Editar gramas"
                        >
                          <Edit2 size={13} />
                        </button>
                        <button
                          onClick={() => setSwappingItemUuid(item.uuid)}
                          className="p-1 text-zinc-400 hover:text-zinc-200"
                          title="Trocar alimento"
                        >
                          <Replace size={13} />
                        </button>
                        <button
                          onClick={() => handleDeleteItem(item.uuid)}
                          className="p-1 text-zinc-400 hover:text-rose-400"
                          title="Remover"
                        >
                          <Trash2 size={13} />
                        </button>
                      </>
                    )}
                  </div>
                )}
              </div>

              {isSwapping && (
                <div className="mt-2 pt-2 border-t border-zinc-800">
                  <div className="flex items-center justify-between mb-1.5">
                    <span className="text-[10px] font-semibold text-zinc-400">
                      Selecione o alimento correspondente:
                    </span>
                    <button
                      onClick={() => setSwappingItemUuid(null)}
                      className="text-[10px] text-zinc-500 hover:text-zinc-300"
                    >
                      Fechar
                    </button>
                  </div>
                  <FoodSearchPicker onPick={(food) => handleSwapFood(item, food)} />
                </div>
              )}
            </li>
          );
        })}
      </ul>
    </Card>
  );
}