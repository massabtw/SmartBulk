"use client";

import { useEffect, useState } from "react";
import { Search, Loader2 } from "lucide-react";
import { bffGet } from "@/lib/bff";
import { FoodModel, PagedResultModel } from "@/types/api";

interface FoodSearchPickerProps {
  onPick: (food: FoodModel) => void;
  placeholder?: string;
  autoFocus?: boolean;
  renderAction?: (food: FoodModel) => React.ReactNode;
}

/** Busca alimentos do catálogo (GET /api/Food?name=) com debounce. */
export function FoodSearchPicker({
  onPick,
  placeholder = "Buscar alimento (ex: aveia, banana, frango...)",
  autoFocus = false,
  renderAction,
}: FoodSearchPickerProps) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<FoodModel[]>([]);
  const [searching, setSearching] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const term = query.trim();
    if (term.length < 2) {
      setResults([]);
      setError(null);
      return;
    }

    let cancelled = false;
    const timer = setTimeout(async () => {
      setSearching(true);
      const res = await bffGet<PagedResultModel<FoodModel>>(
        `food?name=${encodeURIComponent(term)}&pageNumber=1&pageSize=15`
      );
      if (cancelled) return;
      setSearching(false);
      if (res.error) {
        setError(res.error);
        setResults([]);
      } else {
        setError(null);
        setResults(res.data?.items ?? []);
      }
    }, 250);

    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [query]);

  return (
    <div className="space-y-2">
      <div className="relative">
        <Search className="w-4 h-4 absolute left-3 top-2.5 text-zinc-500" />
        <input
          type="text"
          autoFocus={autoFocus}
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder={placeholder}
          aria-label="Buscar alimento"
          className="w-full bg-zinc-950 border border-zinc-800 rounded-xl pl-9 pr-9 py-2 text-xs text-zinc-100 placeholder-zinc-600 focus:outline-none focus:border-brand-500"
        />
        {searching && <Loader2 className="w-4 h-4 absolute right-3 top-2.5 text-brand-500 animate-spin" />}
      </div>

      {error && <p className="text-[11px] text-rose-400">{error}</p>}

      {query.trim().length >= 2 && !searching && !error && results.length === 0 && (
        <p className="text-[11px] text-zinc-500">Nenhum alimento encontrado.</p>
      )}

      {results.length > 0 && (
        <ul className="max-h-44 overflow-y-auto rounded-xl border border-zinc-800 bg-zinc-950 p-1 space-y-1">
          {results.map((f) => (
            <li
              key={f.uuid}
              className="flex items-center justify-between p-2 rounded-lg hover:bg-zinc-900 text-xs transition-colors"
            >
              <button
                type="button"
                onClick={() => {
                  onPick(f);
                  setQuery("");
                  setResults([]);
                }}
                className="flex-1 text-left pr-2 min-w-0"
              >
                <span className="font-semibold text-zinc-200 block truncate">{f.name}</span>
                <span className="text-[10px] text-zinc-500">
                  {Math.round(f.calories)} kcal • {Math.round(f.protein)}g prot (por 100g)
                </span>
              </button>
              {renderAction?.(f)}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

