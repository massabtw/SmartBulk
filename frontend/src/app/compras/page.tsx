"use client";

import { useEffect, useState, useCallback } from "react";
import { motion, AnimatePresence } from "framer-motion";
import { PageHeader } from "@/components/PageHeader";
import { Card } from "@/components/Card";
import { bffGet, bffSend } from "@/lib/bff";
import { addDaysISO, nextMondayISO } from "@/lib/dates";
import { ShoppingListModel, ShoppingListItemModel } from "@/types/api";
import { ShoppingCart, Check, RefreshCw, Sparkles, ChevronLeft, ChevronRight, AlertCircle } from "lucide-react";

function formatGrams(grams: number) {
  if (grams >= 1000) {
    return `${(grams / 1000).toFixed(2).replace(/\.?0+$/, "")} kg`;
  }
  return `${Math.round(grams)} g`;
}

export default function ShoppingListPage() {
  // A lista é sempre da PRÓXIMA semana por padrão (compra-se antes de a semana começar).
  const [weekMonday, setWeekMonday] = useState<string>(() => nextMondayISO());
  const [shoppingList, setShoppingList] = useState<ShoppingListModel | null>(null);
  const [loading, setLoading] = useState(true);
  const [generating, setGenerating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadShoppingList = useCallback(async () => {
    setLoading(true);
    setError(null);
    const res = await bffGet<ShoppingListModel>(`shoppinglist?weekStartDate=${weekMonday}`);
    if (res.error) {
      setShoppingList(null);
      setError(res.error);
    } else {
      // 204 (sem lista ainda) chega como data null.
      setShoppingList(res.data);
    }
    setLoading(false);
  }, [weekMonday]);

  useEffect(() => {
    loadShoppingList();
  }, [loadShoppingList]);

  const handleGenerate = async () => {
    setGenerating(true);
    setError(null);
    const res = await bffSend<ShoppingListModel>("POST", "shoppinglist/generate", {
      weekStartDate: weekMonday,
    });
    setGenerating(false);

    if (res.error || !res.data) {
      setError(res.error ?? "Não foi possível gerar a lista.");
      return;
    }
    setShoppingList(res.data);
  };

  const handleToggleItem = async (item: ShoppingListItemModel) => {
    const nextPurchased = !item.isPurchased;

    setShoppingList((prev) => {
      if (!prev) return prev;
      const items = prev.items.map((i) => (i.uuid === item.uuid ? { ...i, isPurchased: nextPurchased } : i));
      const purchased = items.filter((i) => i.isPurchased).length;
      return { ...prev, items, purchasedItems: purchased, isAllPurchased: purchased === items.length };
    });

    const res = await bffSend<ShoppingListItemModel>("PUT", `shoppinglist/items/${item.uuid}/toggle`, {
      isPurchased: nextPurchased,
    });

    if (res.error) {
      setError(res.error);
      loadShoppingList();
    }
  };

  const items = shoppingList?.items ?? [];
  const purchasedCount = items.filter((i) => i.isPurchased).length;
  const progressPercent = items.length > 0 ? Math.round((purchasedCount / items.length) * 100) : 0;

  return (
    <main className="flex-1 px-4 space-y-4 pb-8">
      <PageHeader
        title="Lista de compras"
        subtitle={`Semana de ${weekMonday}`}
        action={
          <button
            onClick={handleGenerate}
            disabled={generating}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-brand-600 hover:bg-brand-500 text-white font-semibold text-xs transition-colors shadow-sm disabled:opacity-50"
          >
            {generating ? <RefreshCw size={14} className="animate-spin" /> : <Sparkles size={14} />}
            <span>{shoppingList ? "Gerar de novo" : "Gerar lista"}</span>
          </button>
        }
      />

      <div className="flex items-center justify-between px-3 py-1.5 bg-surface-card border border-border/80 rounded-xl text-xs">
        <button
          onClick={() => setWeekMonday(addDaysISO(weekMonday, -7))}
          className="p-1 rounded-lg text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800 transition-colors flex items-center gap-1"
        >
          <ChevronLeft size={16} />
          <span>Anterior</span>
        </button>
        <span className="font-semibold text-zinc-200">Seg {weekMonday.slice(5)}</span>
        <button
          onClick={() => setWeekMonday(addDaysISO(weekMonday, 7))}
          className="p-1 rounded-lg text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800 transition-colors flex items-center gap-1"
        >
          <span>Próxima</span>
          <ChevronRight size={16} />
        </button>
      </div>

      {error && (
        <Card
          role="alert"
          className="bg-rose-950/20 border-rose-500/30 p-3.5 flex items-start gap-2.5 text-xs text-rose-300"
        >
          <AlertCircle size={18} className="text-rose-400 shrink-0 mt-0.5" />
          <p className="flex-1 text-[11px] text-rose-300">{error}</p>
        </Card>
      )}

      {shoppingList && items.length > 0 && (
        <Card className="p-4 bg-gradient-to-br from-zinc-900 to-zinc-950 border-zinc-800 space-y-3">
          <div className="flex items-center justify-between text-xs">
            <div>
              <span className="font-bold text-zinc-100 text-sm">Progresso</span>
              <p className="text-[11px] text-zinc-400 mt-0.5">
                {purchasedCount} de {items.length} itens ({progressPercent}%)
              </p>
            </div>
            <div className="text-right">
              <span className="text-[10px] text-zinc-500 uppercase tracking-wider block">Total cru</span>
              <span className="text-sm font-extrabold text-brand-400">{formatGrams(shoppingList.totalRawGrams)}</span>
            </div>
          </div>

          <div className="h-2 w-full bg-zinc-800 rounded-full overflow-hidden">
            <motion.div
              initial={false}
              animate={{ width: `${progressPercent}%` }}
              transition={{ type: "spring", stiffness: 120, damping: 20 }}
              className="h-full bg-brand-500 rounded-full"
            />
          </div>
        </Card>
      )}

      {loading ? (
        <Card className="p-8 text-center animate-pulse motion-reduce:animate-none">
          <p className="text-xs text-zinc-500">Carregando lista…</p>
        </Card>
      ) : items.length > 0 ? (
        <ul className="space-y-2">
          <AnimatePresence initial={false}>
            {items.map((item) => {
              const hasConversion =
                item.rawYieldFactor && item.rawYieldFactor > 0 && item.rawYieldFactor !== 1.0;

              return (
                <motion.li
                  key={item.uuid}
                  layout
                  initial={{ opacity: 0, y: 5 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0, scale: 0.95 }}
                  transition={{ duration: 0.15 }}
                >
                  <button
                    type="button"
                    role="checkbox"
                    aria-checked={item.isPurchased}
                    onClick={() => handleToggleItem(item)}
                    className={`w-full text-left rounded-2xl border p-3.5 flex items-center gap-3 transition-all duration-200 ${
                      item.isPurchased
                        ? "bg-zinc-950/40 border-zinc-900 opacity-60"
                        : "bg-surface-card hover:bg-surface-hover border-border"
                    }`}
                  >
                    <span
                      className={`w-6 h-6 rounded-lg flex items-center justify-center border transition-all shrink-0 ${
                        item.isPurchased
                          ? "bg-brand-500 border-brand-400 text-white shadow-[0_0_8px_rgba(16,185,129,0.3)]"
                          : "border-zinc-700 bg-zinc-900/60 text-transparent"
                      }`}
                    >
                      <Check size={14} strokeWidth={3} />
                    </span>

                    <span className="min-w-0">
                      <span
                        className={`block text-xs font-semibold truncate transition-colors ${
                          item.isPurchased ? "text-zinc-500 line-through" : "text-zinc-200"
                        }`}
                      >
                        {item.foodName}
                      </span>
                      <span className="flex items-center gap-2 text-[11px] text-zinc-400 mt-0.5">
                        <span className={`font-mono font-bold ${item.isPurchased ? "text-zinc-500" : "text-brand-400"}`}>
                          {formatGrams(item.rawGrams)}
                          {hasConversion ? " cru" : ""}
                        </span>
                        {hasConversion && (
                          <span className="text-[10px] text-zinc-500">
                            ({formatGrams(item.plannedGrams)} pronto ÷ {item.rawYieldFactor})
                          </span>
                        )}
                      </span>
                    </span>
                  </button>
                </motion.li>
              );
            })}
          </AnimatePresence>
        </ul>
      ) : (
        <Card className="p-8 text-center border-dashed border-zinc-800">
          <ShoppingCart className="w-9 h-9 text-zinc-600 mx-auto mb-2 opacity-50" />
          <h4 className="text-xs font-semibold text-zinc-200">Nenhuma lista para esta semana</h4>
          <p className="text-[11px] text-zinc-500 mt-1 max-w-xs mx-auto">
            A lista é calculada a partir do plano da semana em casa. Monte o plano na aba Plano e gere a lista.
          </p>
          <button
            onClick={handleGenerate}
            disabled={generating}
            className="mt-4 px-4 py-2 rounded-xl bg-brand-600 hover:bg-brand-500 text-white font-semibold text-xs transition-colors shadow-sm disabled:opacity-50"
          >
            {generating ? "Calculando…" : "Gerar lista do plano"}
          </button>
        </Card>
      )}
    </main>
  );
}
