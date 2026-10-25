"use client";

import { useState } from "react";
import { Plus, Trash2, X, Star, Loader2 } from "lucide-react";
import { Card } from "@/components/Card";
import { FoodSearchPicker } from "@/components/FoodSearchPicker";
import { bffSend } from "@/lib/bff";
import { FoodModel, MealTemplateModel } from "@/types/api";

interface TemplateFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSaved: () => void;
}

interface SelectedItem {
  foodUuid: string;
  foodName: string;
  servingGrams: number;
  caloriesPer100G: number;
  proteinPer100G: number;
  carbsPer100G: number;
  fatPer100G: number;
  isFavorite: boolean;
}

export function TemplateFormModal({ isOpen, onClose, onSaved }: TemplateFormModalProps) {
  const [name, setName] = useState("");
  const [mealType, setMealType] = useState<"Breakfast" | "Shake" | "FullMeal">("Breakfast");
  const [items, setItems] = useState<SelectedItem[]>([]);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!isOpen) return null;

  const reset = () => {
    setName("");
    setMealType("Breakfast");
    setItems([]);
    setError(null);
  };

  const handleAddFood = (food: FoodModel) => {
    setItems((prev) =>
      prev.some((i) => i.foodUuid === food.uuid)
        ? prev
        : [
            ...prev,
            {
              foodUuid: food.uuid,
              foodName: food.name,
              servingGrams: 100,
              caloriesPer100G: food.calories,
              proteinPer100G: food.protein,
              carbsPer100G: food.carbohydrates,
              fatPer100G: food.fat,
              isFavorite: food.isFavorite,
            },
          ]
    );
  };

  const handleToggleFavorite = async (foodUuid: string, current: boolean) => {
    const res = await bffSend<FoodModel>("PUT", `food/${foodUuid}/favorite`, { isFavorite: !current });
    if (res.error) {
      setError(res.error);
      return;
    }
    setItems((prev) => prev.map((i) => (i.foodUuid === foodUuid ? { ...i, isFavorite: !current } : i)));
  };

  const totals = items.reduce(
    (acc, i) => {
      const f = i.servingGrams / 100;
      return {
        calories: acc.calories + f * i.caloriesPer100G,
        protein: acc.protein + f * i.proteinPer100G,
        carbs: acc.carbs + f * i.carbsPer100G,
        fat: acc.fat + f * i.fatPer100G,
      };
    },
    { calories: 0, protein: 0, carbs: 0, fat: 0 }
  );

  const handleSave = async () => {
    if (!name.trim()) {
      setError("Informe o nome do template.");
      return;
    }
    if (items.length === 0) {
      setError("Adicione pelo menos um alimento.");
      return;
    }
    if (items.some((i) => !(i.servingGrams > 0))) {
      setError("Todas as porções precisam ser maiores que zero.");
      return;
    }

    setSaving(true);
    setError(null);
    const res = await bffSend<MealTemplateModel>("POST", "mealtemplate", {
      name: name.trim(),
      mealType,
      items: items.map((i) => ({ foodUuid: i.foodUuid, servingGrams: i.servingGrams })),
    });
    setSaving(false);

    if (res.error) {
      setError(res.error);
      return;
    }

    reset();
    onSaved();
    onClose();
  };

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="template-title"
      className="fixed inset-0 z-50 bg-black/80 backdrop-blur-sm flex items-end sm:items-center justify-center p-0 sm:p-4"
    >
      <Card className="w-full max-w-md bg-zinc-900 border-zinc-800 rounded-t-3xl sm:rounded-2xl p-5 space-y-4 max-h-[92vh] overflow-y-auto">
        <div className="flex items-center justify-between pb-2 border-b border-zinc-800">
          <div>
            <h2 id="template-title" className="text-base font-bold text-zinc-100">
              Novo template
            </h2>
            <p className="text-xs text-zinc-400 mt-0.5">Receita de casa</p>
          </div>
          <button
            onClick={onClose}
            aria-label="Fechar"
            className="p-1.5 rounded-lg text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800 transition-colors"
          >
            <X size={18} />
          </button>
        </div>

        <div className="grid grid-cols-4 gap-2 p-2.5 rounded-xl bg-zinc-950 border border-zinc-800 text-center text-xs" aria-live="polite">
          <div>
            <span className="text-[10px] text-zinc-500 uppercase font-semibold block">Kcal</span>
            <span className="text-sm font-bold text-amber-400">{Math.round(totals.calories)}</span>
          </div>
          <div>
            <span className="text-[10px] text-zinc-500 uppercase font-semibold block">Prot</span>
            <span className="text-sm font-bold text-sky-400">{Math.round(totals.protein)}g</span>
          </div>
          <div>
            <span className="text-[10px] text-zinc-500 uppercase font-semibold block">Carb</span>
            <span className="text-sm font-bold text-purple-400">{Math.round(totals.carbs)}g</span>
          </div>
          <div>
            <span className="text-[10px] text-zinc-500 uppercase font-semibold block">Gord</span>
            <span className="text-sm font-bold text-rose-400">{Math.round(totals.fat)}g</span>
          </div>
        </div>

        <div className="space-y-3">
          <div>
            <label htmlFor="template-name" className="text-xs font-semibold text-zinc-300 block mb-1">
              Nome
            </label>
            <input
              id="template-name"
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Ex: Shake hipercalórico"
              maxLength={100}
              className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-3 py-2 text-xs text-zinc-100 placeholder-zinc-600 focus:outline-none focus:border-brand-500"
            />
          </div>

          <div>
            <span className="text-xs font-semibold text-zinc-300 block mb-1">Tipo</span>
            <div role="radiogroup" aria-label="Tipo de refeição" className="grid grid-cols-3 p-1 bg-zinc-950 border border-zinc-800 rounded-xl text-xs gap-1">
              {(["Breakfast", "Shake", "FullMeal"] as const).map((type) => (
                <button
                  key={type}
                  type="button"
                  role="radio"
                  aria-checked={mealType === type}
                  onClick={() => setMealType(type)}
                  className={`py-1.5 rounded-lg text-center transition-all ${
                    mealType === type
                      ? "bg-brand-500/15 border border-brand-500/30 text-brand-400 font-bold"
                      : "text-zinc-400 hover:text-zinc-200"
                  }`}
                >
                  {type === "Breakfast" ? "Café" : type === "Shake" ? "Shake" : "Completa"}
                </button>
              ))}
            </div>
          </div>
        </div>

        <div className="space-y-2">
          <span className="text-xs font-semibold text-zinc-300 block">Adicionar alimentos</span>
          <FoodSearchPicker
            onPick={handleAddFood}
            renderAction={(f) => (
              <span className="flex items-center gap-1">
                {f.isFavorite && <Star size={14} className="text-amber-400 fill-amber-400" aria-label="Favorito" />}
                <Plus size={14} className="text-brand-400" aria-hidden="true" />
              </span>
            )}
          />
        </div>

        <div className="space-y-1.5">
          <span className="text-xs font-semibold text-zinc-300 block">Ingredientes ({items.length})</span>

          {items.map((item) => (
            <div
              key={item.foodUuid}
              className="p-2.5 rounded-xl bg-zinc-950 border border-zinc-800 text-xs flex items-center justify-between gap-2"
            >
              <div className="flex-1 min-w-0">
                <span className="font-semibold text-zinc-200 truncate block">{item.foodName}</span>
                <span className="text-[10px] text-zinc-500">
                  {Math.round((item.servingGrams / 100) * item.caloriesPer100G)} kcal •{" "}
                  {Math.round((item.servingGrams / 100) * item.proteinPer100G)}g prot
                </span>
              </div>

              <div className="flex items-center gap-1.5 shrink-0">
                <button
                  type="button"
                  onClick={() => handleToggleFavorite(item.foodUuid, item.isFavorite)}
                  aria-label={item.isFavorite ? "Remover dos favoritos" : "Marcar como favorito"}
                  aria-pressed={item.isFavorite}
                  className={`p-1 rounded ${item.isFavorite ? "text-amber-400" : "text-zinc-600 hover:text-zinc-400"}`}
                >
                  <Star size={14} fill={item.isFavorite ? "currentColor" : "none"} />
                </button>
                <input
                  type="number"
                  step="5"
                  min="1"
                  aria-label={`Gramas de ${item.foodName}`}
                  value={item.servingGrams}
                  onChange={(e) => {
                    const grams = parseFloat(e.target.value) || 0;
                    setItems((prev) =>
                      prev.map((i) => (i.foodUuid === item.foodUuid ? { ...i, servingGrams: grams } : i))
                    );
                  }}
                  className="w-16 bg-zinc-900 border border-zinc-700 rounded-lg px-2 py-1 text-xs text-zinc-100 text-center font-mono"
                />
                <span className="text-[11px] text-zinc-400">g</span>
                <button
                  type="button"
                  onClick={() => setItems((prev) => prev.filter((i) => i.foodUuid !== item.foodUuid))}
                  aria-label={`Remover ${item.foodName}`}
                  className="p-1 text-zinc-500 hover:text-rose-400 transition-colors"
                >
                  <Trash2 size={14} />
                </button>
              </div>
            </div>
          ))}
        </div>

        {error && (
          <p role="alert" className="text-xs text-rose-400 bg-rose-950/20 p-2.5 rounded-xl border border-rose-500/20">
            {error}
          </p>
        )}

        <button
          type="button"
          onClick={handleSave}
          disabled={saving}
          className="w-full py-3 rounded-xl bg-brand-600 hover:bg-brand-500 text-white font-semibold text-xs transition-colors flex items-center justify-center gap-2 disabled:opacity-50"
        >
          {saving && <Loader2 size={16} className="animate-spin" />}
          <span>Salvar template</span>
        </button>
      </Card>
    </div>
  );
}
