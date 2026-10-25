"use client";

import { useEffect, useState, useCallback } from "react";
import { PageHeader } from "@/components/PageHeader";
import { Card } from "@/components/Card";
import { TemplateFormModal } from "@/components/TemplateFormModal";
import { PlanSlotPickerModal } from "@/components/PlanSlotPickerModal";
import { bffGet, bffSend } from "@/lib/bff";
import { addDaysISO, nextMondayISO, parseLocalISODate } from "@/lib/dates";
import { MealTemplateModel, HomeMealPlanModel } from "@/types/api";
import {
  ChefHat,
  CalendarRange,
  Plus,
  Trash2,
  CheckCircle2,
  AlertCircle,
  Save,
  Building2,
  Home,
  RefreshCw,
  ChevronLeft,
  ChevronRight,
} from "lucide-react";

const slotLabels: Record<string, string> = {
  Breakfast: "Café da manhã",
  Lunch: "Almoço em casa",
  Dinner: "Jantar em casa",
  Shake: "Shake",
};

type Draft = Record<string, Record<string, string>>; // date -> slot -> templateUuid

export default function MealPlanPage() {
  const [activeTab, setActiveTab] = useState<"plan" | "templates">("plan");
  // O plano é da PRÓXIMA semana por padrão.
  const [weekMonday, setWeekMonday] = useState<string>(() => nextMondayISO());

  const [templates, setTemplates] = useState<MealTemplateModel[]>([]);
  const [plan, setPlan] = useState<HomeMealPlanModel | null>(null);
  const [loading, setLoading] = useState(true);
  const [savingPlan, setSavingPlan] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);

  const [templateModalOpen, setTemplateModalOpen] = useState(false);
  const [slotPicker, setSlotPicker] = useState<{ isOpen: boolean; dateStr: string; mealSlot: string }>({
    isOpen: false,
    dateStr: "",
    mealSlot: "",
  });

  const [draft, setDraft] = useState<Draft>({});

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);

    const [templatesRes, planRes] = await Promise.all([
      bffGet<MealTemplateModel[]>("mealtemplate"),
      bffGet<HomeMealPlanModel>(`homemealplan?weekStartDate=${weekMonday}`),
    ]);

    if (templatesRes.error) setError(templatesRes.error);
    setTemplates(templatesRes.data ?? []);

    if (planRes.error || !planRes.data) {
      setPlan(null);
      setDraft({});
      if (planRes.error) setError(planRes.error);
    } else {
      setPlan(planRes.data);
      const next: Draft = {};
      planRes.data.days?.forEach((day) => {
        next[day.date] = {};
        day.items?.forEach((item) => {
          next[day.date][item.mealSlot] = item.mealTemplateUuid;
        });
      });
      setDraft(next);
    }

    setDirty(false);
    setLoading(false);
  }, [weekMonday]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const changeWeek = (delta: number) => {
    if (dirty && !window.confirm("Há alterações não salvas neste plano. Descartar?")) return;
    setWeekMonday(addDaysISO(weekMonday, delta * 7));
  };

  const handleSelectTemplateForSlot = (templateUuid: string) => {
    const { dateStr, mealSlot } = slotPicker;
    setDraft((prev) => ({ ...prev, [dateStr]: { ...(prev[dateStr] ?? {}), [mealSlot]: templateUuid } }));
    setDirty(true);
  };

  const handleRemoveSlotItem = (dateStr: string, mealSlot: string) => {
    setDraft((prev) => {
      const daySlots = { ...(prev[dateStr] ?? {}) };
      delete daySlots[mealSlot];
      return { ...prev, [dateStr]: daySlots };
    });
    setDirty(true);
  };

  const handleSavePlan = async () => {
    setSavingPlan(true);
    setError(null);

    const items = Object.entries(draft).flatMap(([date, slots]) =>
      Object.entries(slots)
        .filter(([, templateUuid]) => !!templateUuid)
        .map(([mealSlot, mealTemplateUuid]) => ({ date, mealSlot, mealTemplateUuid }))
    );

    const res = await bffSend<HomeMealPlanModel>("POST", "homemealplan", { weekStartDate: weekMonday, items });
    setSavingPlan(false);

    if (res.error) {
      setError(res.error);
      return;
    }
    await loadData();
  };

  const handleDeleteTemplate = async (uuid: string) => {
    if (!window.confirm("Excluir este template?")) return;
    const res = await bffSend<unknown>("DELETE", `mealtemplate/${uuid}`);
    if (res.error) {
      setError(res.error);
      return;
    }
    setTemplates((prev) => prev.filter((t) => t.uuid !== uuid));
  };

  const templateMap = new Map<string, MealTemplateModel>(templates.map((t) => [t.uuid, t]));

  return (
    <main className="flex-1 px-4 space-y-4 pb-12">
      <PageHeader
        title="Plano e templates"
        subtitle={`Semana de ${weekMonday}`}
        action={
          activeTab === "plan" ? (
            <button
              onClick={handleSavePlan}
              disabled={savingPlan || !dirty}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-brand-600 hover:bg-brand-500 text-white font-semibold text-xs transition-colors shadow-sm disabled:opacity-50"
            >
              {savingPlan ? <RefreshCw size={14} className="animate-spin" /> : <Save size={14} />}
              <span>Salvar plano</span>
            </button>
          ) : (
            <button
              onClick={() => setTemplateModalOpen(true)}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-brand-600 hover:bg-brand-500 text-white font-semibold text-xs transition-colors shadow-sm"
            >
              <Plus size={14} />
              <span>Novo template</span>
            </button>
          )
        }
      />

      <div role="tablist" className="grid grid-cols-2 p-1 bg-surface-card border border-border/80 rounded-2xl text-xs font-semibold">
        <button
          role="tab"
          aria-selected={activeTab === "plan"}
          onClick={() => setActiveTab("plan")}
          className={`py-2 rounded-xl flex items-center justify-center gap-2 transition-all ${
            activeTab === "plan"
              ? "bg-brand-500/15 border border-brand-500/30 text-brand-400"
              : "text-zinc-400 hover:text-zinc-200"
          }`}
        >
          <CalendarRange size={16} />
          <span>Plano da semana</span>
        </button>
        <button
          role="tab"
          aria-selected={activeTab === "templates"}
          onClick={() => setActiveTab("templates")}
          className={`py-2 rounded-xl flex items-center justify-center gap-2 transition-all ${
            activeTab === "templates"
              ? "bg-brand-500/15 border border-brand-500/30 text-brand-400"
              : "text-zinc-400 hover:text-zinc-200"
          }`}
        >
          <ChefHat size={16} />
          <span>Templates ({templates.length})</span>
        </button>
      </div>

      {error && (
        <p role="alert" className="text-xs text-rose-400 bg-rose-950/20 p-2.5 rounded-xl border border-rose-500/20">
          {error}
        </p>
      )}

      {loading ? (
        <Card className="p-8 text-center animate-pulse motion-reduce:animate-none">
          <p className="text-xs text-zinc-500">Carregando…</p>
        </Card>
      ) : activeTab === "plan" ? (
        <div className="space-y-4">
          <div className="flex items-center justify-between px-3 py-1.5 bg-surface-card border border-border/80 rounded-xl text-xs">
            <button
              onClick={() => changeWeek(-1)}
              className="p-1 rounded-lg text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800 transition-colors flex items-center gap-1"
            >
              <ChevronLeft size={16} />
              <span>Anterior</span>
            </button>
            <span className="font-semibold text-zinc-200">Seg {weekMonday.slice(5)}</span>
            <button
              onClick={() => changeWeek(1)}
              className="p-1 rounded-lg text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800 transition-colors flex items-center gap-1"
            >
              <span>Próxima</span>
              <ChevronRight size={16} />
            </button>
          </div>

          {plan && (
            <Card className="p-4 bg-gradient-to-br from-zinc-900 to-zinc-950 border-zinc-800 flex items-center gap-2.5">
              {plan.isWeekComplete && !dirty ? (
                <CheckCircle2 size={20} className="text-emerald-400 shrink-0" />
              ) : (
                <AlertCircle size={20} className="text-amber-400 shrink-0" />
              )}
              <div>
                <p className="text-xs font-bold text-zinc-100">
                  {dirty
                    ? "Alterações não salvas"
                    : plan.isWeekComplete
                    ? "Semana completa"
                    : "Há refeições em aberto"}
                </p>
                <p className="text-[11px] text-zinc-400 mt-0.5">
                  Salvo: {Math.round(plan.totalCalories)} kcal • {Math.round(plan.totalProtein)}g proteína na semana
                </p>
              </div>
            </Card>
          )}

          <div className="space-y-3">
            {plan?.days?.map((day) => {
              const dayDraft = draft[day.date] ?? {};
              let dayCalories = 0;
              let dayProtein = 0;
              Object.values(dayDraft).forEach((tUuid) => {
                const t = templateMap.get(tUuid);
                if (t) {
                  dayCalories += t.totalCalories;
                  dayProtein += t.totalProtein;
                }
              });

              return (
                <Card key={day.date} className="p-4 bg-zinc-900 border-zinc-800 space-y-3">
                  <div className="flex items-center justify-between pb-2 border-b border-zinc-800/80">
                    <div className="flex items-center gap-2">
                      <span className="text-xs font-bold text-zinc-100">
                        {parseLocalISODate(day.date).toLocaleDateString("pt-BR", {
                          weekday: "short",
                          day: "numeric",
                          month: "short",
                        })}
                      </span>
                      {day.hasCafeteria ? (
                        <span className="px-2 py-0.5 rounded text-[10px] font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 flex items-center gap-1">
                          <Building2 size={11} />
                          Refeitório
                        </span>
                      ) : (
                        <span className="px-2 py-0.5 rounded text-[10px] font-semibold bg-amber-500/10 text-amber-400 border border-amber-500/20 flex items-center gap-1">
                          <Home size={11} />
                          Tudo em casa
                        </span>
                      )}
                    </div>

                    <div className="text-right text-[11px] text-zinc-400">
                      <span className="text-amber-400 font-semibold">{Math.round(dayCalories)} kcal</span>
                      {" • "}
                      <span className="text-sky-400 font-semibold">{Math.round(dayProtein)}g prot</span>
                    </div>
                  </div>

                  <div className="space-y-2">
                    {day.requiredSlots.map((slot) => {
                      const selected = dayDraft[slot] ? templateMap.get(dayDraft[slot]) : null;
                      const label = slotLabels[slot] ?? slot;

                      return selected ? (
                        <div
                          key={slot}
                          className="p-2.5 rounded-xl bg-zinc-950 border border-zinc-800 flex items-center justify-between text-xs"
                        >
                          <div className="min-w-0">
                            <span className="text-[10px] font-semibold text-zinc-500 uppercase block">{label}</span>
                            <span className="text-xs font-bold text-zinc-200">{selected.name}</span>
                            <span className="text-[11px] text-zinc-400 ml-2">
                              ({Math.round(selected.totalCalories)} kcal • {Math.round(selected.totalProtein)}g)
                            </span>
                          </div>
                          <div className="flex items-center gap-1 shrink-0">
                            <button
                              onClick={() => setSlotPicker({ isOpen: true, dateStr: day.date, mealSlot: slot })}
                              className="text-[11px] text-brand-400 hover:text-brand-300 px-2 py-1 rounded-lg bg-brand-500/10"
                            >
                              Trocar
                            </button>
                            <button
                              onClick={() => handleRemoveSlotItem(day.date, slot)}
                              aria-label={`Remover ${label}`}
                              className="p-1 text-zinc-500 hover:text-rose-400"
                            >
                              <Trash2 size={13} />
                            </button>
                          </div>
                        </div>
                      ) : (
                        <button
                          key={slot}
                          onClick={() => setSlotPicker({ isOpen: true, dateStr: day.date, mealSlot: slot })}
                          className="w-full p-2.5 rounded-xl border border-dashed border-amber-500/40 bg-amber-500/5 hover:bg-amber-500/10 text-amber-300 font-semibold flex items-center justify-between transition-colors text-xs"
                        >
                          <span>{label}</span>
                          <span className="text-[11px] text-amber-400 flex items-center gap-1 font-normal">
                            <Plus size={13} />
                            Escolher template
                          </span>
                        </button>
                      );
                    })}
                  </div>
                </Card>
              );
            })}
          </div>
        </div>
      ) : (
        <div className="space-y-3">
          {templates.length === 0 ? (
            <Card className="p-8 text-center border-dashed border-zinc-800">
              <ChefHat className="w-9 h-9 text-zinc-600 mx-auto mb-2 opacity-50" />
              <h4 className="text-xs font-semibold text-zinc-200">Nenhum template cadastrado</h4>
              <p className="text-[11px] text-zinc-500 mt-1 max-w-xs mx-auto">
                Crie receitas de café, shake ou refeição completa com cálculo nutricional automático.
              </p>
              <button
                onClick={() => setTemplateModalOpen(true)}
                className="mt-4 px-4 py-2 rounded-xl bg-brand-600 hover:bg-brand-500 text-white font-semibold text-xs transition-colors"
              >
                Criar primeiro template
              </button>
            </Card>
          ) : (
            templates.map((template) => (
              <Card key={template.uuid} className="p-4 bg-zinc-900 border-zinc-800 flex items-center justify-between">
                <div>
                  <div className="flex items-center gap-2">
                    <h4 className="text-xs font-bold text-zinc-100">{template.name}</h4>
                    <span className="text-[10px] px-1.5 rounded bg-zinc-800 text-zinc-400 font-medium">
                      {template.mealType}
                    </span>
                  </div>
                  <div className="flex items-center gap-2 text-[11px] text-zinc-400 mt-1">
                    <span className="text-amber-400 font-semibold">{Math.round(template.totalCalories)} kcal</span>
                    <span>•</span>
                    <span className="text-sky-400 font-semibold">{Math.round(template.totalProtein)}g proteína</span>
                    <span>•</span>
                    <span>{template.items?.length ?? 0} itens</span>
                  </div>
                </div>
                <button
                  onClick={() => handleDeleteTemplate(template.uuid)}
                  aria-label={`Excluir ${template.name}`}
                  className="p-1.5 text-zinc-500 hover:text-rose-400 hover:bg-rose-500/10 rounded-lg transition-colors"
                >
                  <Trash2 size={16} />
                </button>
              </Card>
            ))
          )}
        </div>
      )}

      <TemplateFormModal
        isOpen={templateModalOpen}
        onClose={() => setTemplateModalOpen(false)}
        onSaved={() => loadData()}
      />

      <PlanSlotPickerModal
        isOpen={slotPicker.isOpen}
        onClose={() => setSlotPicker((prev) => ({ ...prev, isOpen: false }))}
        dateStr={slotPicker.dateStr}
        mealSlot={slotPicker.mealSlot}
        templates={templates}
        onSelectTemplate={handleSelectTemplateForSlot}
      />
    </main>
  );
}

