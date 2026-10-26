"use client";

import { useState, useRef } from "react";
import { Upload, FileText, Image as ImageIcon, X, Loader2, RefreshCw } from "lucide-react";
import { Card } from "@/components/Card";
import { bffSend } from "@/lib/bff";
import { CafeteriaMenuModel } from "@/types/api";

interface MenuIngestModalProps {
  isOpen: boolean;
  onClose: () => void;
  onIngestSuccess: () => void;
  weekStartDate: string;
}

const MAX_IMAGE_BYTES = 15 * 1024 * 1024;

async function compressImage(
  file: File,
  maxDimension = 1280,
  quality = 0.8
): Promise<{ base64: string; mimeType: string; dataUrl: string }> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(new Error("Falha ao ler o arquivo de imagem."));
    reader.onload = () => {
      const img = new Image();
      img.onerror = () => reject(new Error("Falha ao carregar a imagem selecionada."));
      img.onload = () => {
        let { width, height } = img;
        if (width > maxDimension || height > maxDimension) {
          if (width >= height) {
            height = Math.round((height * maxDimension) / width);
            width = maxDimension;
          } else {
            width = Math.round((width * maxDimension) / height);
            height = maxDimension;
          }
        }

        const canvas = document.createElement("canvas");
        canvas.width = width;
        canvas.height = height;

        const ctx = canvas.getContext("2d");
        if (!ctx) {
          reject(new Error("Não foi possível preparar o processador de imagem."));
          return;
        }

        // Fundo branco para garantir que transparência não fique preta ao converter para JPEG
        ctx.fillStyle = "#FFFFFF";
        ctx.fillRect(0, 0, width, height);
        ctx.drawImage(img, 0, 0, width, height);

        const mimeType = "image/jpeg";
        const dataUrl = canvas.toDataURL(mimeType, quality);
        const commaIdx = dataUrl.indexOf(",");
        const base64 = commaIdx >= 0 ? dataUrl.slice(commaIdx + 1) : dataUrl;

        resolve({ base64, mimeType, dataUrl });
      };
      img.src = reader.result as string;
    };
    reader.readAsDataURL(file);
  });
}

export function MenuIngestModal({ isOpen, onClose, onIngestSuccess, weekStartDate }: MenuIngestModalProps) {
  const [tab, setTab] = useState<"text" | "image">("text");
  const [text, setText] = useState("");
  const [preview, setPreview] = useState<string | null>(null);
  const [imageBase64, setImageBase64] = useState<string | null>(null);
  const [imageMimeType, setImageMimeType] = useState<string | null>(null);
  const [compressing, setCompressing] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fileInputRef = useRef<HTMLInputElement>(null);

  if (!isOpen) return null;

  const handleImageChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!file.type.startsWith("image/")) {
      setError("Por favor, selecione um arquivo de imagem válido.");
      return;
    }

    if (file.size > MAX_IMAGE_BYTES) {
      setError("Imagem muito grande (máx. 15 MB).");
      return;
    }

    setCompressing(true);
    setError(null);

    try {
      const compressed = await compressImage(file, 1280, 0.8);
      setPreview(compressed.dataUrl);
      setImageBase64(compressed.base64);
      setImageMimeType(compressed.mimeType);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Falha ao processar e comprimir imagem.");
    } finally {
      setCompressing(false);
      if (fileInputRef.current) {
        fileInputRef.current.value = "";
      }
    }
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
    setImageMimeType(null);
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
      <Card className="w-full max-w-md bg-zinc-900 border-zinc-800 rounded-t-3xl sm:rounded-2xl p-5 max-h-[90vh] flex flex-col">
        {/* Cabeçalho fixo */}
        <div className="flex items-center justify-between pb-3 border-b border-zinc-800 shrink-0">
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

        {/* Miolo rolável */}
        <div className="flex-1 overflow-y-auto py-3 space-y-4 pr-1 min-h-0">
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
          ) : preview ? (
            <div className="space-y-3">
              <div className="relative rounded-2xl overflow-hidden border border-zinc-800 bg-zinc-950 flex items-center justify-center p-2">
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img
                  src={preview}
                  alt="Pré-visualização do cardápio"
                  className="w-full max-h-60 object-contain rounded-lg"
                />
              </div>

              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => fileInputRef.current?.click()}
                  className="flex-1 py-2 px-3 rounded-xl bg-zinc-800 hover:bg-zinc-700 text-zinc-200 text-xs font-medium transition-colors flex items-center justify-center gap-1.5"
                >
                  <RefreshCw size={14} />
                  <span>Trocar imagem</span>
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setPreview(null);
                    setImageBase64(null);
                    setImageMimeType(null);
                  }}
                  className="py-2 px-3 rounded-xl border border-zinc-800 hover:bg-zinc-800/60 text-zinc-400 hover:text-zinc-200 text-xs font-medium transition-colors"
                >
                  Remover
                </button>
                <input
                  ref={fileInputRef}
                  type="file"
                  accept="image/*"
                  onChange={handleImageChange}
                  className="hidden"
                />
              </div>
            </div>
          ) : (
            <label className="border-2 border-dashed border-zinc-700 hover:border-brand-500 rounded-2xl p-6 flex flex-col items-center justify-center cursor-pointer transition-colors bg-zinc-950/40">
              <Upload className="w-8 h-8 text-zinc-500 mb-2" />
              <span className="text-xs font-semibold text-zinc-300">Toque para selecionar a foto</span>
              <span className="text-[11px] text-zinc-500 mt-0.5">PNG, JPG ou JPEG</span>
              <input
                ref={fileInputRef}
                type="file"
                accept="image/*"
                onChange={handleImageChange}
                className="hidden"
              />
            </label>
          )}
        </div>

        {/* Rodapé fixo */}
        <div className="pt-3 pb-4 border-t border-zinc-800/80 shrink-0 space-y-2">
          {error && (
            <p role="alert" className="text-xs text-rose-400 bg-rose-950/20 p-2.5 rounded-xl border border-rose-500/20">
              {error}
            </p>
          )}

          <button
            onClick={handleIngest}
            disabled={loading || compressing}
            className="w-full py-3 rounded-xl bg-brand-600 hover:bg-brand-500 text-white font-semibold text-xs transition-colors flex items-center justify-center gap-2 shadow-lg shadow-brand-500/10 disabled:opacity-50"
          >
            {loading ? (
              <>
                <Loader2 size={16} className="animate-spin" />
                <span>Extraindo as refeições…</span>
              </>
            ) : compressing ? (
              <>
                <Loader2 size={16} className="animate-spin" />
                <span>Otimizando imagem…</span>
              </>
            ) : (
              <span>Processar cardápio</span>
            )}
          </button>
        </div>
      </Card>
    </div>
  );
}
