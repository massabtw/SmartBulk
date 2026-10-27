import { NextRequest, NextResponse } from "next/server";

/**
 * BFF: repassa as chamadas do navegador para a API .NET adicionando a API key,
 * que existe apenas aqui no servidor (nunca é enviada ao navegador).
 * BACKEND_API_URL e BACKEND_API_KEY são obrigatórias (ver .env.example).
 */
async function forward(req: NextRequest, { params }: { params: { path: string[] } }) {
  const baseUrl = process.env.BACKEND_API_URL;
  const apiKey = process.env.BACKEND_API_KEY;

  if (!baseUrl || !apiKey) {
    return NextResponse.json(
      {
        success: false,
        message: "BFF não configurado.",
        errors: ["Defina BACKEND_API_URL e BACKEND_API_KEY no ambiente do servidor Next.js."],
      },
      { status: 500 }
    );
  }

  try {
    const subPath = params.path ? params.path.join("/") : "";
    const cleanBase = baseUrl.replace(/\/+$/, "");
    const targetUrl = `${cleanBase}/api/${subPath}${req.nextUrl.search}`;

    const headers = new Headers();
    headers.set("X-Api-Key", apiKey);

    const contentType = req.headers.get("content-type");
    if (contentType) {
      headers.set("Content-Type", contentType);
    }

    let body: string | undefined;
    if (req.method !== "GET" && req.method !== "HEAD") {
      body = await req.text();
    }

    const response = await fetch(targetUrl, {
      method: req.method,
      headers,
      body,
      cache: "no-store",
    });

    const responseData = response.status === 204 ? null : await response.text();

    return new NextResponse(responseData, {
      status: response.status,
      headers: {
        "Content-Type": response.headers.get("content-type") || "application/json",
      },
    });
  } catch (error: unknown) {
    const message = error instanceof Error ? error.message : "Erro desconhecido";
    return NextResponse.json(
      {
        success: false,
        message: "Erro no BFF ao se comunicar com o backend.",
        errors: [message],
      },
      { status: 502 }
    );
  }
}

export const GET = forward;
export const POST = forward;
export const PUT = forward;
export const DELETE = forward;
export const PATCH = forward;
