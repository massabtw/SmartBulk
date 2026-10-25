"use client";

import { useEffect, useState, useCallback } from "react";
import { PageHeader } from "@/components/PageHeader";
import { Card } from "@/components/Card";
import { MenuIngestModal } from "@/components/MenuIngestModal";
import { MealReviewCard } from "@/components/MealReviewCard";
import { bffGet, bffSend } from "@/lib/bff";
import { addDaysISO, currentMondayISO, parseLocalISODate, todayISO } from "@/lib/dates";
import { CafeteriaMenuModel, CafeteriaDayOverrideModel } from "@/types/api";
import {
  Upload,
  CheckCircle2,
  AlertCircle,
  Building2,
  Home,
  RefreshCw,
  Lock,
  ChevronLeft,
  ChevronRight,
} from "lucide-react";

export default function CafeteriaMenuPage() {
  const [weekMonday, setWeekMonday] = useState<string>(() => currentMondayISO());
  const [menu, setMenu] = useState<CafeteriaMenuModel | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [ingestModalOpen, setIngestModalOpen] = useState(false);
  const [activeDayIndex, setActiveDayIndex] = useState<number>(() => {
    const dow = parseLocalISODate(todayISO()).getDay(); // 0 = dom
    return dow >= 1 && dow <= 5 ? dow - 1 : 0;
  });
  const [hasCafeteriaByDate, setHasCafeteriaByDate] = useState<Record<string, boolean>>({});
  const [confirming, setConfirming] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const weekDays = [0, 1, 2, 3, 4].map((i) => {
    const dateStr = addDaysISO(weekMonday, i);
    const label = parseLocalISODate(dateStr).toLocaleDateString("pt-BR", { weekday: "short" });
    return { dateStr, label: label.charAt(0).toUpperCase() + label.slice(1, 3) };
  });

  const loadWeek = useCallback(
    async (isBackground = false) => {
      if (!isBackground) setLoading(true);
      else setRefreshing(true);

      const days = [0, 1, 2, 3, 4].map((i) => addDaysISO(weekMonday, i));
      const [menuRes, ...overrideRes] = await Promise.all([
        bffGet<CafeteriaMenuModel>(`cafeteriamenu?date=${weekMonday}`),
        ...days.map((d) => bffGet<CafeteriaDayOverrideModel>(`cafeteriamenu/day-override?date=${d}`)),
      ]);

      // Sem cardápio na semana (404) é um estado normal.
      setMenu(menuRes.data);

      const flags: Record<string, boolean> = {};
      days.forEach((d, idx) => {
        flags[d] = overrideRes[idx].data?.hasCafeteria ?? true;
      });
      setHasCafeteriaByDate(flags);

      setLoading(false);
      setRefreshing(false);
    },
    [weekMonday]
  );

  useEffect(() => {
    loadWeek();
  }, [loadWeek]);

  const selectedDay = weekDays[activeDayIndex] ?? weekDays[0];
  const dayMeals = menu?.meals?.filter((m) => m.date === selectedDay.dateStr) ?? [];
  const selectedHasCafeteria = hasCafeteriaByDate[selectedDay.dateStr] ?? true;

  const totalPending =
    menu?.meals?.reduce((acc, m) => acc + (m.items?.filter((i) => i.isPending).length ?? 0), 0) ?? 0;
  const isConfirmed = menu?.status?.toLowerCase() === "confirmed";

  const handleConfirmMenu = async () => {
    if (!menu || totalPending > 0 || isConfirmed || confirming) return;
    setConfirming(true);
    setActionError(null);
    const res = await bffSend<CafeteriaMenuModel>("POST", `cafeteriamenu/${menu.uuid}/confirm`);
    setConfirming(false);
    if (res.error) {
      setActionError(res.error);
      return;
    }
    await loadWeek(true);
  };

  const handleReopenMenu = async () => {
    if (!menu || !isConfirmed) return;
    setActionError(null);
    const res = await bffSend<CafeteriaMenuModel>("POST", `cafeteriamenu/${menu.uuid}/reopen`);
    if (res.error) {
      setActionError(res.error);
      return;
    }
    await loadWeek(true);
  };

  const handleToggleDayCafeteria = async (dateStr: string) => {
    const current = hasCafeteriaByDate[dateStr] ?? true;
    const next = !current;
    setHasCafeteriaByDate((prev) => ({ ...prev, [dateStr]: next }));
    setActionError(null);

    // Persistimos sempre o valor explícito (true ou false) para não depender do padrão do dia.
    const res = await bffSend<CafeteriaDayOverrideModel>("POST", "cafeteriamenu/day-override", {
      date: dateStr,
      hasCafeteria: next,
      reason: next ? null : "Sem refeitório (home office, feriado ou férias)",
    });

    if (res.error) {
      setHasCafeteriaByDate((prev) => ({ ...prev, [dateStr]: current }));
      setActionError(res.error);
    }
  };

  const statusTone = isConfirmed
    ? "bg-emerald-950/20 border-emerald-500/30"
    : totalPending > 0
    ? "bg-amber-950/20 border-amber-500/30"
    : "bg-blue-950/20 border-blue-500/30";

  return (
    <main className="flex-1 px-4 space-y-4 pb-8">
      <PageHeader
        title="Cardápio do refeitório"
        subtitle={`Semana de ${weekMonday}`}
        action={
          <div className="flex items-center gap-1.5">
            <button
              onClick={() => loadWeek(true)}
              disabled={refreshing}
              aria-label="Atualizar"
              className={`p-1.5 rounded-lg text-zinc-400 hover:text-zinc-200 transition-colors ${
                refreshing ? "animate-spin text-brand-500" : ""
              }`}
            >
              <RefreshCw size={16} />
            </button>
            <button
              onClick={() => setIngestModalOpen(true)}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-brand-600 hover:bg-brand-500 text-white font-semibold text-xs transition-colors shadow-sm"
            >
              <Upload size={14} />
              <span>Enviar</span>
            </button>
          </div>
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

      {actionError && (
        <p role="alert" className="text-xs text-rose-400 bg-rose-950/20 p-2.5 rounded-xl border border-rose-500/20">
          {actionError}
        </p>
      )}

      {menu && (
        <Card className={`p-4 flex items-center justify-between gap-3 border ${statusTone}`}>
          <div className="flex items-center gap-2.5 min-w-0">
            {isConfirmed ? (
              <CheckCircle2 size={20} className="text-emerald-400 shrink-0" />
            ) : totalPending > 0 ? (
              <AlertCircle size={20} className="text-amber-400 shrink-0" />
            ) : (
              <Lock size={20} className="text-blue-400 shrink-0" />
            )}
            <div className="min-w-0">
              <p className="text-xs font-bold text-zinc-100">
                {isConfirmed
                  ? "Cardápio confirmado"
                  : totalPending > 0
                  ? `${totalPending} pendência(s) de revisão`
                  : "Pronto para confirmar"}
              </p>
              <p className="text-[11px] text-zinc-300/80 mt-0.5">
                {isConfirmed
                  ? "O garantido no refeitório já entra no Gap diário."
                  : totalPending > 0
                  ? "Associe um alimento (ícone de troca) ou exclua o item para liberar a confirmação."
                  : "Revise os pratos e confirme para abater no Gap."}
              </p>
            </div>
          </div>

          {isConfirmed ? (
            <button
              onClick={handleReopenMenu}
              className="shrink-0 px-3 py-1.5 rounded-xl bg-zinc-800 hover:bg-zinc-700 text-zinc-200 font-semibold text-xs transition-colors"
            >
              Reabrir
            </button>
          ) : (
            <button
              onClick={handleConfirmMenu}
              disabled={totalPending > 0 || confirming}
              className="shrink-0 px-3 py-1.5 rounded-xl bg-brand-600 hover:bg-brand-500 text-white font-bold text-xs transition-colors disabled:opacity-40 disabled:pointer-events-none shadow-md shadow-brand-500/20"
            >
              {confirming ? "Confirmando…" : "Confirmar"}
            </button>
          )}
        </Card>
      )}

      <div role="tablist" aria-label="Dias da semana" className="grid grid-cols-5 p-1 bg-surface-card border border-border/80 rounded-2xl gap-1">
        {weekDays.map((day, idx) => {
          const isSelected = activeDayIndex === idx;
          const hasCaf = hasCafeteriaByDate[day.dateStr] ?? true;

          return (
            <button
              key={day.dateStr}
              role="tab"
              aria-selected={isSelected}
              onClick={() => setActiveDayIndex(idx)}
              className={`py-2 px-1 rounded-xl text-center transition-all ${
                isSelected
                  ? "bg-brand-500/15 border border-brand-500/30 text-brand-400 font-bold"
                  : "text-zinc-400 hover:text-zinc-200"
              }`}
            >
              <span className="block text-xs">{day.label}</span>
              <span className="text-[9px] opacity-60 block mt-0.5">{day.dateStr.slice(8)}</span>
              <span
                aria-hidden="true"
                className={`w-1.5 h-1.5 rounded-full mx-auto mt-1 ${hasCaf ? "bg-emerald-500" : "bg-zinc-600"}`}
              />
            </button>
          );
        })}
      </div>

      <div className="flex items-center justify-between p-3 rounded-xl bg-zinc-950 border border-zinc-800 text-xs">
        <div className="flex items-center gap-2">
          {selectedHasCafeteria ? (
            <Building2 size={16} className="text-emerald-400" />
          ) : (
            <Home size={16} className="text-amber-400" />
          )}
          <span className="text-zinc-300 font-medium">
            {selectedHasCafeteria ? "Dia com refeitório" : "Sem refeitório (home office, feriado ou férias)"}
          </span>
        </div>
        <button
          onClick={() => handleToggleDayCafeteria(selectedDay.dateStr)}
          className="text-xs text-brand-400 font-semibold px-2 py-1 rounded-lg bg-brand-500/10 hover:bg-brand-500/20 transition-colors"
        >
          Alternar
        </button>
      </div>

      {loading ? (
        <Card className="p-8 text-center animate-pulse motion-reduce:animate-none">
          <p className="text-xs text-zinc-500">Carregando cardápio…</p>
        </Card>
      ) : dayMeals.length > 0 ? (
        <div className="space-y-3">
          {dayMeals.map((meal) => (
            <MealReviewCard
              key={meal.uuid}
              meal={meal}
              readOnly={isConfirmed}
              onItemUpdated={() => loadWeek(true)}
            />
          ))}
        </div>
      ) : (
        <Card className="p-8 text-center border-dashed border-zinc-800">
          <Upload className="w-8 h-8 text-zinc-600 mx-auto mb-2 opacity-60" />
          <h4 className="text-xs font-semibold text-zinc-300">
            {menu ? `Sem refeições em ${selectedDay.dateStr}` : "Nenhum cardápio nesta semana"}
          </h4>
          <p className="text-[11px] text-zinc-500 mt-1 max-w-xs mx-auto">
            Envie o texto ou a foto do cardápio semanal para extrair os pratos.
          </p>
          <button
            onClick={() => setIngestModalOpen(true)}
            className="mt-4 px-3 py-1.5 rounded-xl bg-brand-600 hover:bg-brand-500 text-white font-semibold text-xs transition-colors"
          >
            Enviar cardápio
          </button>
        </Card>
      )}

      <MenuIngestModal
        isOpen={ingestModalOpen}
        onClose={() => setIngestModalOpen(false)}
        onIngestSuccess={() => loadWeek()}
        weekStartDate={weekMonday}
      />
    </main>
  );
}
