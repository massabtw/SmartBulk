# 06: Cliente LLM compatível com OpenAI

**What to build:** Poder chamar qualquer endpoint compatível com `chat/completions` (GitHub Models ou Gemini via endpoint OpenAI) apenas trocando configuração, para obter respostas em JSON a partir de texto e imagem opcional.

**Blocked by:** 01: Solution, banco e DbUp

**Status:** done

Padrões: seguir `.agents/skills/backend/SKILL.md` e `.agents/skills/mandatory-rules.md` (Model, `XModelMap`, `Result<T>`, `UnitOfWork`, `AddTransient`, DDL com Id/Uuid/CreatedAt/UpdatedAt/Deleted, soft delete). Sem EF Core e sem AutoMapper. Contexto: `../spec.md`.

- [x] Interface `ILlmClient` no Domain e implementação com `HttpClient` na Infrastructure, seguindo `external-integrations.md`
- [x] `BaseUrl`, `ApiKey` e `Model` lidos de `appsettings.json` por Options; sem valores secretos no repositório
- [x] Todos os contratos JSON da integração ficam em `Domain/Http/<Sistema>/Models/`
- [x] Suporte a modo JSON e a imagem em base64
- [x] Resposta 429 ou de cota esgotada vira `Result` com falha identificável, sem retry em loop
- [x] Testes com handler HTTP falso cobrindo sucesso, imagem, 429 e erro de rede
