"use client";

import { useEffect, useState } from "react";
import { PageHeader } from "@/components/PageHeader";
import { Card } from "@/components/Card";
import { bffGet, bffSend } from "@/lib/bff";
import { UserSettingsModel, WeightLogModel } from "@/types/api";
import {
  Flame,
  Dumbbell,
  Clock,
  Globe,
  Save,
  CheckCircle2,
  AlertCircle,
  Loader2,
} from "lucide-react";

export default function SettingsPage() {
  const [settings, setSettings] = useState<UserSettingsModel>({
    calorieTargetKcal: 3000,
    proteinTargetGPerKg: 2.0,
    timeZoneId: "America/Sao_Paulo",
    lunchReminderTime: "12:30",
    dinnerReminderTime: "19:30",
  });

  const [latestWeight, setLatestWeight] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadFailed, setLoadFailed] = useState(false);
  const [saving, setSaving] = useState(false);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      setLoading(true);
      const [settingsRes, weightRes] = await Promise.all([
        bffGet<UserSettingsModel>("settings"),
        bffGet<WeightLogModel>("weight/latest"),
      ]);

      if (settingsRes.error || !settingsRes.data) {
        // Não mostramos valores padrão como se fossem os atuais: salvar sobrescreveria os reais.
        setLoadFailed(true);
        setErrorMessage(settingsRes.error ?? "Não foi possível carregar as configurações.");
      } else {
        const s = settingsRes.data;
        setSettings({
          uuid: s.uuid,
          calorieTargetKcal: s.calorieTargetKcal,
          proteinTargetGPerKg: s.proteinTargetGPerKg,
          timeZoneId: s.timeZoneId,
          lunchReminderTime: s.lunchReminderTime?.slice(0, 5),
          dinnerReminderTime: s.dinnerReminderTime?.slice(0, 5),
        });
      }

      // Sem peso registrado (404) é um estado normal.
      setLatestWeight(weightRes.data?.weightKg ?? null);
      setLoading(false);
    }

    load();
  }, []);

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setSuccessMessage(null);
    setErrorMessage(null);

    // Basic validation
    if (settings.calorieTargetKcal <= 0) {
      setErrorMessage("A meta de calorias deve ser maior que zero.");
      setSaving(false);
      return;
    }

    if (settings.proteinTargetGPerKg <= 0) {
      setErrorMessage("A meta de proteína por kg deve ser maior que zero.");
      setSaving(false);
      return;
    }

    const res = await bffSend<UserSettingsModel>("PUT", "settings", settings);
    setSaving(false);

    if (res.error) {
      setErrorMessage(res.error);
      return;
    }

    setSuccessMessage("Configurações salvas!");
    setTimeout(() => setSuccessMessage(null), 4000);
  };

  const calculatedProteinGrams = Math.round(
    (latestWeight ?? 80) * settings.proteinTargetGPerKg
  );

  return (
    <main className="flex-1 px-4 space-y-4 pb-12">
      <PageHeader
        title="Configurações"
        subtitle="Parâmetros metabólicos e horários do SmartBulk"
      />

      {/* Success Notification */}
      {successMessage && (
        <Card className="bg-emerald-950/20 border-emerald-500/30 p-3.5 flex items-center gap-2.5 text-xs text-emerald-300">
          <CheckCircle2 size={18} className="text-emerald-400 shrink-0" />
          <span className="font-semibold">{successMessage}</span>
        </Card>
      )}

      {/* Error Notification */}
      {errorMessage && (
        <Card className="bg-rose-950/20 border-rose-500/30 p-3.5 flex items-center gap-2.5 text-xs text-rose-300">
          <AlertCircle size={18} className="text-rose-400 shrink-0" />
          <span className="font-semibold">{errorMessage}</span>
        </Card>
      )}

      {loading ? (
        <Card className="p-8 text-center animate-pulse">
          <p className="text-xs text-zinc-500">Carregando configurações...</p>
        </Card>
      ) : loadFailed ? (
        <Card className="p-6 text-center border-dashed border-zinc-800">
          <p className="text-xs text-zinc-300 font-semibold">Configurações indisponíveis</p>
          <p className="text-[11px] text-zinc-500 mt-1">
            Para não sobrescrever seus valores reais, o formulário fica bloqueado até a API responder.
          </p>
          <button
            type="button"
            onClick={() => window.location.reload()}
            className="mt-4 px-4 py-2 rounded-xl bg-brand-600 hover:bg-brand-500 text-white font-semibold text-xs transition-colors"
          >
            Tentar de novo
          </button>
        </Card>
      ) : (
        <form onSubmit={handleSave} className="space-y-4">
          {/* Live Calculated Protein Card */}
          <Card className="p-4 bg-gradient-to-br from-zinc-900 to-zinc-950 border-zinc-800">
            <div className="flex items-center justify-between pb-3 border-b border-zinc-800/80">
              <div className="flex items-center gap-2.5">
                <div className="w-9 h-9 rounded-xl bg-sky-500/10 text-sky-400 flex items-center justify-center">
                  <Dumbbell size={18} />
                </div>
                <div>
                  <p className="text-[11px] text-zinc-400 uppercase font-semibold">
                    Meta Diária de Proteína
                  </p>
                  <p className="text-xl font-black text-sky-400">
                    {calculatedProteinGrams}g / dia
                  </p>
                </div>
              </div>

              <div className="text-right text-[11px] text-zinc-400">
                <span className="text-zinc-500 block">Cálculo dinâmico</span>
                <span className="font-mono text-zinc-300">
                  {latestWeight ?? 80} kg × {settings.proteinTargetGPerKg} g/kg
                </span>
              </div>
            </div>

            <p className="text-[11px] text-zinc-500 pt-2 leading-relaxed">
              Calculada multiplicando sua meta por kg pelo peso mais recente registrado (
              {latestWeight ? `${latestWeight} kg` : "80 kg padrão"}).
            </p>
          </Card>

          {/* Nutritional Targets */}
          <Card className="p-4 space-y-4 bg-zinc-900 border-zinc-800">
            <div className="flex items-center gap-2 pb-2 border-b border-zinc-800">
              <Flame size={16} className="text-amber-400" />
              <h3 className="text-xs font-bold text-zinc-200 uppercase tracking-wider">
                Metas Nutricionais de Bulking
              </h3>
            </div>

            <div>
              <label className="text-xs font-semibold text-zinc-300 block mb-1">
                Meta Diária de Calorias (kcal)
              </label>
              <input
                type="number"
                step="50"
                min="1000"
                max="10000"
                value={settings.calorieTargetKcal}
                onChange={(e) =>
                  setSettings({
                    ...settings,
                    calorieTargetKcal: parseInt(e.target.value) || 0,
                  })
                }
                className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-3 py-2 text-xs text-zinc-100 font-mono focus:outline-none focus:border-brand-500"
              />
              <span className="text-[10px] text-zinc-500 mt-1 block">
                Valor recomendado para superávit calórico: ~3000 kcal/dia.
              </span>
            </div>

            <div>
              <label className="text-xs font-semibold text-zinc-300 block mb-1">
                Proteína por Kg de Peso Corporal (g/kg)
              </label>
              <input
                type="number"
                step="0.1"
                min="0.5"
                max="5.0"
                value={settings.proteinTargetGPerKg}
                onChange={(e) =>
                  setSettings({
                    ...settings,
                    proteinTargetGPerKg: parseFloat(e.target.value) || 0,
                  })
                }
                className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-3 py-2 text-xs text-zinc-100 font-mono focus:outline-none focus:border-brand-500"
              />
              <span className="text-[10px] text-zinc-500 mt-1 block">
                Padrão científico para hipertrofia: 2,0 g por kg de peso corporal.
              </span>
            </div>
          </Card>

          {/* Time & Reminders */}
          <Card className="p-4 space-y-4 bg-zinc-900 border-zinc-800">
            <div className="flex items-center gap-2 pb-2 border-b border-zinc-800">
              <Clock size={16} className="text-purple-400" />
              <h3 className="text-xs font-bold text-zinc-200 uppercase tracking-wider">
                Lembretes & Fuso Horário
              </h3>
            </div>

            <div>
              <label className="text-xs font-semibold text-zinc-300 block mb-1 flex items-center gap-1.5">
                <Globe size={13} className="text-zinc-500" />
                <span>Fuso Horário</span>
              </label>
              <select
                value={settings.timeZoneId}
                onChange={(e) =>
                  setSettings({ ...settings, timeZoneId: e.target.value })
                }
                className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-3 py-2 text-xs text-zinc-100 focus:outline-none focus:border-brand-500"
              >
                <option value="America/Sao_Paulo">Brasília / São Paulo (UTC-3)</option>
                <option value="America/Manaus">Manaus (UTC-4)</option>
                <option value="America/Cuiaba">Cuiabá (UTC-4)</option>
                <option value="America/Belem">Belém (UTC-3)</option>
                <option value="America/Fortaleza">Fortaleza (UTC-3)</option>
                <option value="UTC">UTC Padrão</option>
              </select>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="text-xs font-semibold text-zinc-300 block mb-1">
                  Lembrete Almoço
                </label>
                <input
                  type="time"
                  value={settings.lunchReminderTime}
                  onChange={(e) =>
                    setSettings({ ...settings, lunchReminderTime: e.target.value })
                  }
                  className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-3 py-2 text-xs text-zinc-100 font-mono focus:outline-none focus:border-brand-500"
                />
              </div>

              <div>
                <label className="text-xs font-semibold text-zinc-300 block mb-1">
                  Lembrete Jantar
                </label>
                <input
                  type="time"
                  value={settings.dinnerReminderTime}
                  onChange={(e) =>
                    setSettings({ ...settings, dinnerReminderTime: e.target.value })
                  }
                  className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-3 py-2 text-xs text-zinc-100 font-mono focus:outline-none focus:border-brand-500"
                />
              </div>
            </div>
            <span className="text-[10px] text-zinc-500 block">
              Disparados via bot do Telegram com botões de perfil de prato nos dias com refeitório.
            </span>
          </Card>

          {/* Submit Button */}
          <button
            type="submit"
            disabled={saving}
            className="w-full py-3.5 rounded-xl bg-brand-600 hover:bg-brand-500 text-white font-bold text-xs transition-colors flex items-center justify-center gap-2 shadow-lg shadow-brand-500/10 disabled:opacity-50"
          >
            {saving ? (
              <>
                <Loader2 size={16} className="animate-spin" />
                <span>Salvando...</span>
              </>
            ) : (
              <>
                <Save size={16} />
                <span>Salvar Configurações</span>
              </>
            )}
          </button>
        </form>
      )}
    </main>
  );
}
