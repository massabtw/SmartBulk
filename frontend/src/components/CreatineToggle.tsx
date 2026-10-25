"use client";

import { useEffect, useState } from "react";
import { motion } from "framer-motion";
import { Sparkles, Check } from "lucide-react";
import { Card } from "@/components/Card";
import { bffSend } from "@/lib/bff";
import { CreatineLogModel } from "@/types/api";

interface CreatineToggleProps {
  initialTaken: boolean;
  dateStr: string;
}

export function CreatineToggle({ initialTaken, dateStr }: CreatineToggleProps) {
  const [taken, setTaken] = useState(initialTaken);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Sincroniza quando os dados chegam do servidor ou a data muda.
  useEffect(() => {
    setTaken(initialTaken);
    setError(null);
  }, [initialTaken, dateStr]);

  const handleToggle = async () => {
    if (loading) return;
    const next = !taken;
    setTaken(next);
    setLoading(true);
    setError(null);

    const res = await bffSend<CreatineLogModel>("POST", "creatine", { date: dateStr, taken: next });
    if (res.error) {
      setTaken(!next);
      setError("Não foi possível salvar.");
    }
    setLoading(false);
  };

  return (
    <Card
      onClick={handleToggle}
      role="switch"
      aria-checked={taken}
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          handleToggle();
        }
      }}
      className={`cursor-pointer transition-all duration-200 select-none flex items-center justify-between p-3.5 ${
        taken
          ? "bg-purple-950/20 border-purple-500/30"
          : "bg-surface-card hover:bg-surface-hover border-border"
      }`}
    >
      <div className="flex items-center gap-3">
        <div
          className={`w-9 h-9 rounded-xl flex items-center justify-center transition-colors ${
            taken ? "bg-purple-500/20 text-purple-400" : "bg-zinc-800 text-zinc-400"
          }`}
        >
          <Sparkles size={18} />
        </div>
        <div>
          <p className="text-xs font-semibold text-zinc-200">Creatina</p>
          <p className={`text-[11px] ${error ? "text-rose-400" : "text-zinc-400"}`}>
            {error ?? (taken ? "Tomada ✓" : "Não registrada")}
          </p>
        </div>
      </div>

      <motion.div
        whileTap={{ scale: 0.85 }}
        className={`w-7 h-7 rounded-full flex items-center justify-center border transition-all ${
          taken
            ? "bg-purple-500 border-purple-400 text-white shadow-[0_0_10px_rgba(168,85,247,0.4)]"
            : "border-zinc-700 bg-zinc-900/50"
        }`}
      >
        {taken && <Check size={14} strokeWidth={3} />}
      </motion.div>
    </Card>
  );
}
