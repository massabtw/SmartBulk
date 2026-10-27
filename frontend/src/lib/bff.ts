/**
 * Cliente tipado do navegador para o BFF (/api/bff/*).
 * Nunca lança: retorna { data, error, status }. Trata erro HTTP, corpo vazio (204) e falha de rede.
 */
export interface BffResult<T> {
  data: T | null;
  error: string | null;
  status: number;
}

export async function bff<T>(path: string, init: RequestInit = {}): Promise<BffResult<T>> {
  try {
    const headers = new Headers(init.headers);
    if (init.body && !headers.has("Content-Type")) {
      headers.set("Content-Type", "application/json");
    }

    const res = await fetch(`/api/bff/${path.replace(/^\/+/, "")}`, {
      ...init,
      headers,
      cache: "no-store",
    });

    const text = await res.text();
    let json: unknown = null;
    if (text) {
      try {
        json = JSON.parse(text);
      } catch {
        json = null;
      }
    }

    if (!res.ok) {
      const body = json as { message?: string; errors?: string[] } | null;
      const message =
        (body?.errors && body.errors.length > 0 ? body.errors.join(" ") : body?.message) ||
        `Erro ${res.status}`;
      return { data: null, error: message, status: res.status };
    }

    return { data: (json as T) ?? null, error: null, status: res.status };
  } catch (err: unknown) {
    if (err instanceof DOMException && err.name === "AbortError") {
      return { data: null, error: null, status: 0 };
    }
    const message = err instanceof Error ? err.message : "Falha de conexão com o servidor.";
    return { data: null, error: message, status: 0 };
  }
}

export const bffGet = <T,>(path: string) => bff<T>(path);

export const bffSend = <T,>(method: "POST" | "PUT" | "DELETE", path: string, body?: unknown) =>
  bff<T>(path, {
    method,
    body: body === undefined ? undefined : JSON.stringify(body),
  });
