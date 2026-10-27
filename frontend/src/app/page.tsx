"use client";

import { useEffect, useState, useCallback, useRef } from "react";
import { PageHeader } from "@/components/PageHeader";
import { Card } from "@/components/Card";
import { MacroProgressBar } from "@/components/MacroProgressBar";
import { CreatineToggle } from "@/components/CreatineToggle";
import { WeightLogger } from "@/components/WeightLogger";
import { MealHistoryList } from "@/components/MealHistoryList";
import { bffGet, bffSend } from "@/lib/bff";
import { addDaysISO, parseLocalISODate, todayISO } from "@/lib/dates";
import {
  DailyGapModel,
  WeightLogModel,
  CreatineLogModel,
  CafeteriaDayOverrideModel,
} from "@/types/api";
import { ChevronLeft, ChevronRight, RefreshCw, Flame, AlertTriangle, Building2 } from "lucide-react";

export default function TodayPage() {
  const [dateStr, setDateStr] = useState<string>(() => todayISO());
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [gapData, setGapData] = useState<DailyGapModel | null>(null);
  const [latestWeight, setLatestWeight] = useState<number | null>(null);
  const [creatineTaken, setCreatineTaken] = useState(false);

  interface DayCacheItem {
    gap: DailyGapModel;
    creatine: boolean;
  }
  const dayCache = useRef<Map<string, DayCacheItem>>(new Map());

  const isToday = dateStr === todayISO();

  const loadData = useCallback(
    async (isBackground = false) => {
      const cached = dayCache.current.get(dateStr);
      if (cached && !isBackground) {
        setGapData(cached.gap);
        setCreatineTaken(cached.creatine);
        setLoading(false);
        setRefreshing(true);
      } else if (!isBackground && !dayCache.current.has(dateStr)) {
        setLoading(true);
      } else {
        setRefreshing(true);
      }

      setError(null);

      const [gapRes, weightRes, creatineRes] = await Promise.all([
        bffGet<DailyGapModel>(`meallog/daily-gap?date=${dateStr}`),
        bffGet<WeightLogModel>("weight/latest"),
        bffGet<CreatineLogModel>(`creatine?date=${dateStr}`),
      ]);

      if (gapRes.error || !gapRes.data) {
        if (!cached) setGapData(null);
        setError(gapRes.error ?? "Não foi possível carregar o dia.");
      } else {
        setGapData(gapRes.data);
        dayCache.current.set(dateStr, {
          gap: gapRes.data,
          creatine: creatineRes.data?.taken ?? false,
        });
      }

      // Sem peso registrado (404) é um estado normal.
      setLatestWeight(weightRes.data?.weightKg ?? null);
      setCreatineTaken(creatineRes.data?.taken ?? false);

      setLoading(false);
      setRefreshing(false);
    },
    [dateStr]
  );

  useEffect(() => {
    loadData();
  }, [loadData]);

  const formattedDate = parseLocalISODate(dateStr).toLocaleDateString("pt-BR", {
    weekday: "long",
    day: "numeric",
    month: "long",
  });

  const handleToggleTodayDinner = async () => {
    if (!gapData?.cafeteria) return;
    const currentIncludeDinner = gapData.cafeteria.includeDinner ?? true;
    const nextIncludeDinner = !currentIncludeDinner;

    await bffSend<CafeteriaDayOverrideModel>("POST", "cafeteriamenu/day-override", {
      date: dateStr,
      hasCafeteria: true,
      includeDinner: nextIncludeDinner,
      reason: nextIncludeDinner ? null : "NoDinner",
    });

    await loadData(true);
  };

  return (
    <main className="flex-1 px-4 space-y-4 pb-6">
      <PageHeader
        title="SmartBulk"
        subtitle={formattedDate.charAt(0).toUpperCase() + formattedDate.slice(1)}
        action={
          <div className="flex items-center gap-1.5">
            <button
              onClick={() => loadData(true)}
              disabled={refreshing}
              aria-label="Atualizar dados"
              className={`p-1.5 rounded-lg text-zinc-400 hover:text-zinc-200 transition-colors ${
                refreshing ? "animate-spin text-brand-500" : ""
              }`}
            >
              <RefreshCw size={16} />
            </button>

            {isToday ? (
              <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-medium bg-brand-500/10 text-brand-500 border border-brand-500/20">
                <span className="w-1.5 h-1.5 rounded-full bg-brand-500 animate-pulse motion-reduce:animate-none" />
                Hoje
              </span>
            ) : (
              <button
                onClick={() => setDateStr(todayISO())}
                className="px-2.5 py-1 rounded-full text-[11px] font-medium bg-zinc-800 text-zinc-300 hover:bg-zinc-700 transition-colors"
              >
                Voltar a hoje
              </button>
            )}
          </div>
        }
      />

      <div className="flex items-center justify-between px-2 py-1 bg-surface-card border border-border/80 rounded-xl text-xs">
        <button
          onClick={() => setDateStr(addDaysISO(dateStr, -1))}
          className="px-2 py-1 rounded-lg text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800 transition-colors flex items-center gap-1 min-h-[36px]"
        >
          <ChevronLeft size={16} />
          <span>Anterior</span>
        </button>
        <span className="font-semibold text-zinc-200">{dateStr}</span>
        <button
          onClick={() => setDateStr(addDaysISO(dateStr, 1))}
          className="px-2 py-1 rounded-lg text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800 transition-colors flex items-center gap-1 min-h-[36px]"
        >
          <span>Próximo</span>
          <ChevronRight size={16} />
        </button>
      </div>

      {error && (
        <Card
          role="alert"
          className="bg-rose-950/20 border-rose-500/30 p-3.5 flex items-start gap-2.5 text-xs text-rose-300"
        >
          <AlertTriangle size={18} className="text-rose-400 shrink-0 mt-0.5" />
          <div className="flex-1">
            <p className="font-semibold">Não foi possível carregar o dia</p>
            <p className="text-[11px] text-rose-400/90 mt-0.5">{error}</p>
          </div>
          <button
            onClick={() => loadData()}
            className="px-2 py-1 bg-rose-500/20 hover:bg-rose-500/30 text-rose-200 font-medium rounded-lg text-[11px] transition-colors"
          >
            Tentar de novo
          </button>
        </Card>
      )}

      {loading ? (
        <Card className="p-4 space-y-4 animate-pulse motion-reduce:animate-none">
          <div className="h-6 bg-zinc-800 rounded w-1/2" />
          <div className="h-4 bg-zinc-800 rounded w-full" />
          <div className="h-4 bg-zinc-800 rounded w-full" />
        </Card>
      ) : gapData ? (
        <Card className="bg-gradient-to-br from-zinc-900 via-zinc-900 to-zinc-950 border-zinc-800 space-y-4 p-4">
          <div className="flex items-center justify-between pb-3 border-b border-zinc-800/80">
            <div className="flex items-center gap-2.5">
              <div className="w-9 h-9 rounded-xl bg-calorie/10 text-calorie flex items-center justify-center">
                <Flame size={20} />
              </div>
              <div>
                <p className="text-[11px] text-zinc-400 uppercase font-medium">Falta fechar em casa</p>
                <h2 className="text-2xl font-black text-zinc-100 tracking-tight">
                  {Math.round(gapData.calorieGap)}{" "}
                  <span className="text-sm font-semibold text-zinc-400">kcal</span>
                </h2>
              </div>
            </div>

            <div className="text-right">
              <span className="text-[10px] text-zinc-500 uppercase tracking-wider block">Meta</span>
              <span className="text-sm font-bold text-zinc-300">
                {Math.round(gapData.calorieTarget)} kcal
              </span>
            </div>
          </div>

          {gapData.cafeteria && gapData.cafeteria.hasCafeteria && (
            <div className="flex flex-wrap items-center justify-between gap-2 p-2.5 rounded-xl bg-zinc-950/80 border border-zinc-800/80 text-xs">
              <div className="flex items-center gap-2">
                <Building2
                  size={15}
                  className={
                    gapData.cafeteria.includeDinner ?? true ? "text-emerald-400" : "text-amber-400"
                  }
                />
                <span className="text-zinc-300 font-medium text-[11px]">
                  {(gapData.cafeteria.includeDinner ?? true)
                    ? "Refeitório hoje: Almoço (1.0x) + Jantar (0.75x)"
                    : "Refeitório hoje: Apenas Almoço (Sem jantar lá • 0.0x)"}
                </span>
              </div>
              <button
                type="button"
                onClick={handleToggleTodayDinner}
                className={`px-2.5 py-1 rounded-lg text-[11px] font-semibold transition-colors border ${
                  gapData.cafeteria.includeDinner ?? true
                    ? "bg-zinc-800 border-zinc-700 text-zinc-200 hover:bg-zinc-700 hover:text-white"
                    : "bg-rose-950/40 border-rose-500/40 text-rose-300 hover:bg-rose-900/50"
                }`}
              >
                {(gapData.cafeteria.includeDinner ?? true)
                  ? "Não vou jantar lá"
                  : "Sem jantar lá (0.0x) • Reativar"}
              </button>
            </div>
          )}

          {gapData.cafeteria &&
            gapData.cafeteria.hasCafeteria &&
            !gapData.cafeteria.hasConfirmedMenu && (
              <p className="text-[11px] text-amber-300 bg-amber-500/10 border border-amber-500/20 rounded-xl p-2">
                Cardápio do refeitório ainda não confirmado: o Gap não desconta o almoço e a janta.
              </p>
            )}

          {gapData.weightWarning && (
            <p className="text-[11px] text-amber-300 bg-amber-500/10 border border-amber-500/20 rounded-xl p-2">
              {gapData.weightWarning}
            </p>
          )}

          <MacroProgressBar
            label="Calorias"
            unit="kcal"
            target={gapData.calorieTarget}
            consumed={gapData.consumedCalories}
            guaranteed={gapData.cafeteriaCalories}
            gap={gapData.calorieGap}
            consumedColor="bg-amber-500"
            guaranteedColor="bg-sky-400"
          />

          <MacroProgressBar
            label="Proteína"
            unit="g"
            target={gapData.proteinTarget}
            consumed={gapData.consumedProtein}
            guaranteed={gapData.cafeteriaProtein}
            gap={gapData.proteinGap}
            consumedColor="bg-sky-500"
            guaranteedColor="bg-indigo-400"
          />
        </Card>
      ) : null}

      <div className="grid grid-cols-2 gap-3">
        <WeightLogger latestWeight={latestWeight} dateStr={dateStr} onWeightSaved={() => loadData(true)} />
        <CreatineToggle initialTaken={creatineTaken} dateStr={dateStr} />
      </div>

      <MealHistoryList meals={gapData?.meals ?? []} onMealDeleted={() => loadData(true)} />
    </main>
  );
}
