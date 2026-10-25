# 25: Roteamento Híbrido LLM (Texto vs. Visão) + Fallback Inteligente (Groq + GitHub Models)

**What to build:** Evolução do `ILlmClient` na camada Infrastructure para arquitetura multi-provider com roteamento inteligente baseado no tipo de requisição (texto vs. visão) e auto-fallback transparente mantendo custo zero. Chamadas de visão vão direto para GitHub Models (GPT-4o), chamadas de texto vão primariamente para Groq (Llama 3.3 70B) em altíssima velocidade e, caso ocorra 429 (rate limit), 5xx ou timeout, é acionado fallback automático de 1 tentativa para GitHub Models.

**Blocked by:** 06: Cliente LLM compatível com OpenAI

**Status:** done

Padrões: seguir `.agents/skills/backend/SKILL.md`, `external-integrations.md` e `.agents/skills/mandatory-rules.md` (Domain/Http/<Sistema>, Infrastructure/Http/<Sistema>, Options, Models de contrato em arquivos próprios, `Result<T>`, isolamento de camadas). Sem EF Core e sem AutoMapper.

- [x] Configuração Multi-Provider (`appsettings.json`, `LlmOptions`, `LlmProviderOptions`, `LlmSettings` retrocompatível com provedor único legado)
- [x] Contrato `ILlmClient` limpo e invariante no Domain (Application desacoplada de detalhes dos provedores)
- [x] Roteamento Inteligente na Infrastructure (`LlmClient`):
  - [x] **Regra 1 (Visão):** Requisições com imagem (`imageBase64` / `image_url`) roteadas diretamente para `VisionAndFallbackProvider` (`gpt-4o`)
  - [x] **Regra 2 (Texto Rápido):** Requisições somente texto roteadas primariamente para `FastTextProvider` (`llama-3.3-70b-versatile` no Groq)
  - [x] **Regra 3 (Auto-Fallback):** Falhas 429, 5xx ou timeout no provedor primário disparam fallback transparente (1 tentativa) para o `VisionAndFallbackProvider`
- [x] Prevenção de loop/retry excessivo e respeito a provedor único legado
- [x] Suíte de testes unitários dedicada cobrindo roteamento de texto, visão, fallback em 429, fallback em 503, fallback em timeout e falha dupla sem loop
- [x] Validação completa com `dotnet build` e `dotnet test` (100% de aprovação)

