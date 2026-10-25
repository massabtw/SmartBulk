"use client";

import { motion, useReducedMotion } from "framer-motion";

interface MacroProgressBarProps {
  label: string;
  unit: string;
  target: number;
  consumed: number;
  guaranteed: number;
  gap: number;
  consumedColor?: string;
  guaranteedColor?: string;
}

export function MacroProgressBar({
  label,
  unit,
  target,
  consumed,
  guaranteed,
  gap,
  consumedColor = "bg-brand-500",
  guaranteedColor = "bg-sky-400",
}: MacroProgressBarProps) {
  const prefersReduced = useReducedMotion();

  const safeTarget = target > 0 ? target : 1;
  const consumedPct = Math.min(100, Math.max(0, (consumed / safeTarget) * 100));
  const guaranteedPct = Math.min(100 - consumedPct, Math.max(0, (guaranteed / safeTarget) * 100));
  const totalAccountedPct = Math.min(100, consumedPct + guaranteedPct);

  return (
    <div className="space-y-1.5">
      <div className="flex items-center justify-between text-xs">
        <span className="font-medium text-zinc-300">{label}</span>
        <div className="text-right">
          <span className="font-bold text-zinc-100">
            {Math.round(consumed + guaranteed)}
          </span>
          <span className="text-zinc-500 font-normal"> / {Math.round(target)} {unit}</span>
        </div>
      </div>

      {/* Progress Track */}
      <div className="h-3 w-full bg-zinc-800/80 rounded-full overflow-hidden flex relative p-0.5 border border-zinc-800">
        {/* Consumed segment */}
        <motion.div
          initial={{ width: 0 }}
          animate={{ width: `${consumedPct}%` }}
          transition={
            prefersReduced
              ? { duration: 0 }
              : { type: "spring", stiffness: 100, damping: 20 }
          }
          className={`h-full ${consumedColor} rounded-l-full relative`}
        />

        {/* Guaranteed segment */}
        <motion.div
          initial={{ width: 0 }}
          animate={{ width: `${guaranteedPct}%` }}
          transition={
            prefersReduced
              ? { duration: 0 }
              : { type: "spring", stiffness: 100, damping: 20, delay: 0.1 }
          }
          className={`h-full ${guaranteedColor} ${consumedPct === 0 ? "rounded-l-full" : ""} ${
            totalAccountedPct >= 99 ? "rounded-r-full" : ""
          }`}
        />
      </div>

      {/* Legend & Gap breakdown */}
      <div className="flex items-center justify-between text-[11px] text-zinc-400 pt-0.5">
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-1.5">
            <span className={`w-2 h-2 rounded-full ${consumedColor}`} />
            <span>Comido: {Math.round(consumed)}{unit}</span>
          </div>
          {guaranteed > 0 && (
            <div className="flex items-center gap-1.5">
              <span className={`w-2 h-2 rounded-full ${guaranteedColor}`} />
              <span>Refeitório: {Math.round(guaranteed)}{unit}</span>
            </div>
          )}
        </div>

        <div className="text-zinc-300 font-medium">
          {gap > 0 ? (
            <span className="text-amber-400">Falta {Math.round(gap)}{unit}</span>
          ) : (
            <span className="text-emerald-400 font-semibold">Meta batida! 🎉</span>
          )}
        </div>
      </div>
    </div>
  );
}

