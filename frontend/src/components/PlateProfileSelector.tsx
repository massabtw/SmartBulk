"use client";

import { useEffect, useState } from "react";
import { motion } from "framer-motion";
import { bffSend } from "@/lib/bff";

interface PlateProfileSelectorProps {
  currentProfile: string; // Standard, Good, Poor
  date: string;
  mealType: string;
  onProfileChanged?: (newProfile: string, multiplier: number) => void;
}

const profiles = [
  { id: "Standard", label: "Padrão", mult: 1.0, sub: "1.0x" },
  { id: "Good", label: "Prato bom", mult: 1.25, sub: "1.25x" },
  { id: "Poor", label: "Prato ruim", mult: 0.75, sub: "0.75x" },
];

export function PlateProfileSelector({
  currentProfile,
  date,
  mealType,
  onProfileChanged,
}: PlateProfileSelectorProps) {
  const [profile, setProfile] = useState(currentProfile);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setProfile(currentProfile);
  }, [currentProfile]);

  const handleSelect = async (selectedId: string, mult: number) => {
    if (selectedId === profile) return;
    const previous = profile;
    const previousMult = profiles.find((p) => p.id.toLowerCase() === previous.toLowerCase())?.mult ?? 1;

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
        className="grid grid-cols-3 p-1 bg-zinc-950/80 border border-zinc-800 rounded-xl text-[11px] gap-1"
      >
        {profiles.map((p) => {
          const isSelected = profile.toLowerCase() === p.id.toLowerCase();

          return (
            <button
              key={p.id}
              role="radio"
              aria-checked={isSelected}
              onClick={() => handleSelect(p.id, p.mult)}
              className={`relative py-1.5 px-1 rounded-lg text-center transition-all ${
                isSelected ? "text-brand-300 font-bold" : "text-zinc-500 hover:text-zinc-300"
              }`}
            >
              <span className="relative z-10 block tracking-tight">{p.label}</span>
              <span className="relative z-10 text-[9px] opacity-75 font-mono">{p.sub}</span>

              {isSelected && (
                <motion.div
                  layoutId={`plateProfile_${date}_${mealType}`}
                  className="absolute inset-0 bg-brand-500/15 border border-brand-500/30 rounded-lg"
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
