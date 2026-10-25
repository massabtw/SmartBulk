"use client";

import { useState } from "react";
import { Upload, FileText, Image as ImageIcon, X, Loader2 } from "lucide-react";
import { Card } from "@/components/Card";
import { bffSend } from "@/lib/bff";
import { CafeteriaMenuModel } from "@/types/api";

interface MenuIngestModalProps {
  isOpen: boolean;
  onClose: () => void;
  onIngestSuccess: () => void;
  weekStartDate: string;
}

const MAX_IMAGE_BYTES = 4 * 1024 * 1024;

export function MenuIngestModal({ isOpen, onClose, onIngestSuccess, weekStartDate }: MenuIngestModalProps) {
  const [tab, setTab] = useState<"text" | "image">("text");
  const [text, setText] = useState("");
  const [preview, setPreview] = useState<string | null>(null);
  const [imageBase64, setImageBase64] = useState<string | null>(null);
  const [imageMimeType, setImageMimeType] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleImageChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (file.size > MAX_IMAGE_BYTES) {
      setError("Imagem muito grande (máx. 4 MB).");
      return;
    }

    const reader = new FileReader();
    reader.onloadend = () => {
      const dataUrl = reader.result as string;
      // O backend espera base64 puro + mime type separado (não a data URL inteira).
      const commaIdx = dataUrl.indexOf(",");
      setPreview(dataUrl);
      setImageBase64(commaIdx >= 0 ? dataUrl.slice(commaIdx + 1) : dataUrl);
      setImageMimeType(file.type || "image/jpeg");
      setError(null);
    };
    reader.readAsDataURL(file);
  };

  const handleIngest = async () => {
    setError(null);

    const payload: Record<string, unknown> = { weekStartDate };
    if (tab === "text") {
      if (!text.trim()) {
        setError("Digite ou cole o texto (ou JSON) do cardápio.");
        return;
      }
      payload.text = text.trim();
    } else {
      if (!imageBase64) {
        setError("Selecione uma imagem do cardápio.");
        return;
      }
      payload.imageBase64 = imageBase64;
      payload.imageMimeType = imageMimeType;
    }

    setLoading(true);
    const res = await bffSend<CafeteriaMenuModel>("POST", "cafeteriamenu/ingest", payload);
    setLoading(false);

    if (res.error) {
      setError(res.error);
      return;
    }

    setText("");
    setPreview(null);
    setImageBase64(null);
    onIngestSuccess();
    onClose();
  };

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="ingest-title"
      className="fixed inset-0 z-50 bg-black/80 backdrop-blur-sm flex items-end sm:items-center justify-center p-0 sm:p-4"
    >
      <Card className="w-full max-w-md bg-zinc-900 border-zinc-800 rounded-t-3xl sm:rounded-2xl p-5 space-y-4 max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between pb-2 border-b border-zinc-800">
          <div>
            <h2 id="ingest-title" className="text-base font-bold text-zinc-100">
              Enviar cardápio da semana
            </h2>
            <p className="text-xs text-zinc-400 mt-0.5">Semana de {weekStartDate}</p>
          </div>
          <button
            onClick={onClose}
            aria-label="Fechar"
            className="p-1.5 rounded-lg text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800 transition-colors"
          >
            <X size={18} />
          </button>
        </div>

        <div className="grid grid-cols-2 p-1 bg-zinc-950 border border-zinc-800 rounded-xl text-xs font-medium">
          <button
            onClick={() => setTab("text")}
            className={`py-2 rounded-lg flex items-center justify-center gap-2 transition-all ${
              tab === "text"
                ? "bg-brand-500/15 text-brand-400 border border-brand-500/30"
                : "text-zinc-400 hover:text-zinc-200"
            }`}
          >
            <FileText size={15} />
            <span>Texto ou JSON</span>
          </button>
          <button
            onClick={() => setTab("image")}
            className={`py-2 rounded-lg flex items-center justify-center gap-2 transition-all ${
              tab === "image"
                ? "bg-brand-500/15 text-brand-400 border border-brand-500/30"
                : "text-zinc-400 hover:text-zinc-200"
            }`}
          >
            <ImageIcon size={15} />
            <span>Foto / Imagem</span>
          </button>
        </div>

        {tab === "text" ? (
          <textarea
            rows={8}
            value={text}
            onChange={(e) => setText(e.target.value)}
            aria-label="Texto ou JSON do cardápio"
            placeholder="Cole aqui o cardápio (ex: Segunda: Almoço - Arroz, Feijão, Frango grelhado...)"
            className="w-full bg-zinc-950 border border-zinc-800 rounded-xl p-3 text-xs text-zinc-200 placeholder-zinc-600 focus:outline-none focus:border-brand-500 font-mono leading-relaxed resize-none"
          />
        ) : (
          <div className="space-y-3">
            <label className="border-2 border-dashed border-zinc-700 hover:border-brand-500 rounded-2xl p-6 flex flex-col items-center justify-center cursor-pointer transition-colors bg-zinc-950/40">
              <Upload className="w-8 h-8 text-zinc-500 mb-2" />
              <span className="text-xs font-semibold text-zinc-300">Toque para selecionar a foto</span>
              <span className="text-[11px] text-zinc-500 mt-0.5">PNG, JPG ou JPEG (até 4 MB)</span>
              <input type="file" accept="image/*" onChange={handleImageChange} className="hidden" />
            </label>

            {preview && (
              <div className="relative rounded-xl overflow-hidden border border-zinc-800 max-h-40">
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img src={preview} alt="Pré-visualização do cardápio" className="w-full h-auto object-cover" />
              </div>
            )}
          </div>
        )}

        {error && (
          <p role="alert" className="text-xs text-rose-400 bg-rose-950/20 p-2.5 rounded-xl border border-rose-500/20">
            {error}
          </p>
        )}

        <button
          onClick={handleIngest}
          disabled={loading}
          className="w-full py-3 rounded-xl bg-brand-600 hover:bg-brand-500 text-white font-semibold text-xs transition-colors flex items-center justify-center gap-2 shadow-lg shadow-brand-500/10 disabled:opacity-50"
        >
          {loading ? (
            <>
              <Loader2 size={16} className="animate-spin" />
              <span>Extraindo as refeições…</span>
            </>
          ) : (
            <span>Processar cardápio</span>
          )}
        </button>
      </Card>
    </div>
  );
}
