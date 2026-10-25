"use client";

import { useEffect, useState } from "react";
import { motion } from "framer-motion";
import { bffSend } from "@/lib/bff";

interface PlateProfileSelectorProps {
  currentProfile: string; // Standard, Good, Poor, None, Small, Generous
  date: string;
  mealType: string;
  onProfileChanged?: (newProfile: string, multiplier: number) => void;
}

const lunchProfiles = [
  { id: "Standard", label: "Padrão", mult: 1.0, sub: "1.0x" },
  { id: "Good", label: "Prato bom", mult: 1.25, sub: "1.25x" },
  { id: "Poor", label: "Prato ruim", mult: 0.75, sub: "0.75x" },
  { id: "None", label: "Não comi", mult: 0.0, sub: "0.0x" },
];

const dinnerProfiles = [
  { id: "Small", label: "Padrão reduzido", mult: 0.75, sub: "0.75x" },
  { id: "Standard", label: "Porção inteira", mult: 1.0, sub: "1.0x" },
  { id: "None", label: "Não janto lá", mult: 0.0, sub: "0.0x" },
];

function matchesProfile(current: string, targetId: string, isDinner: boolean): boolean {
  const c = current?.toLowerCase() ?? "";
  const t = targetId.toLowerCase();

  if (t === "none") {
    return c === "none" || c === "skip" || c.includes("não") || c.includes("nao");
  }
  if (t === "small" || t === "poor") {
    if (!c && isDinner) return true; // Default for dinner is Small
    return c === "small" || c === "poor" || c.includes("ruim") || c.includes("reduzid");
  }
  if (t === "good" || t === "generous") {
    return c === "good" || c === "generous" || c.includes("bom");
  }
  if (t === "standard") {
    if (!c && !isDinner) return true; // Default for lunch is Standard
    return c === "standard" || c.includes("padrão") || c.includes("padrao");
  }

  return c === t;
}

export function PlateProfileSelector({
  currentProfile,
  date,
  mealType,
  onProfileChanged,
}: PlateProfileSelectorProps) {
  const isDinner = mealType?.toLowerCase() === "dinner";
  const defaultProfile = isDinner ? "Small" : "Standard";
  const [profile, setProfile] = useState(currentProfile || defaultProfile);
  const [error, setError] = useState<string | null>(null);

  const activeProfiles = isDinner ? dinnerProfiles : lunchProfiles;

  useEffect(() => {
    setProfile(currentProfile || (isDinner ? "Small" : "Standard"));
  }, [currentProfile, isDinner]);

  const handleSelect = async (selectedId: string, mult: number) => {
    if (matchesProfile(profile, selectedId, isDinner)) return;
    const previous = profile;
    const previousMult =
      activeProfiles.find((p) => matchesProfile(previous, p.id, isDinner))?.mult ??
      (isDinner ? 0.75 : 1.0);

    setProfile(selectedId);
    setError(null);
    onProfileChanged?.(selectedId, mult);

    const res = await bffSend<unknown>("PUT", "cafeteriamenu/meals/profile", {
      date,
      mealType,
      plateProfile: selectedId,
    });

    if (res.error) {
      setProfile(previous);
      onProfileChanged?.(previous, previousMult);
      setError(res.error);
    }
  };

  return (
    <div>
      <div
        role="radiogroup"
        aria-label="Perfil do prato"
        className={`grid ${isDinner ? "grid-cols-3" : "grid-cols-4"} p-1 bg-zinc-950/80 border border-zinc-800 rounded-xl text-[11px] gap-1`}
      >
        {activeProfiles.map((p) => {
          const isSelected = matchesProfile(profile, p.id, isDinner);

          return (
            <button
              key={p.id}
              role="radio"
              aria-checked={isSelected}
              onClick={() => handleSelect(p.id, p.mult)}
              className={`relative py-1.5 px-1 rounded-lg text-center transition-all ${
                isSelected
                  ? p.mult === 0
                    ? "text-rose-400 font-bold"
                    : "text-brand-300 font-bold"
                  : "text-zinc-500 hover:text-zinc-300"
              }`}
            >
              <span className="relative z-10 block tracking-tight truncate">{p.label}</span>
              <span className="relative z-10 text-[9px] opacity-75 font-mono">{p.sub}</span>

              {isSelected && (
                <motion.div
                  layoutId={`plateProfile_${date}_${mealType}`}
                  className={`absolute inset-0 rounded-lg ${
                    p.mult === 0
                      ? "bg-rose-500/15 border border-rose-500/30"
                      : "bg-brand-500/15 border border-brand-500/30"
                  }`}
                  transition={{ type: "spring", stiffness: 400, damping: 25 }}
                />
              )}
            </button>
          );
        })}
      </div>
      {error && <p className="text-[11px] text-rose-400 mt-1">{error}</p>}
    </div>
  );
}
