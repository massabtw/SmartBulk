# 24: Integração Open Food Facts API + Fallback Cache

**What to build:** Busca externa de alimentos com fallback automático no Open Food Facts e auto-cache no SQL Server via Dapper, aliviando a LLM de inventar ou calcular macronutrientes e garantindo que ela atue exclusivamente como parser semântico.

**Blocked by:** 03: Catálogo de alimentos; 06: Cliente LLM; 13: Registro de refeição em linguagem natural

**Status:** done

Padrões: seguir `.agents/skills/backend/SKILL.md`, `external-integrations.md` e `.agents/skills/mandatory-rules.md` (Domain/Http/<Sistema>, Infrastructure/Http/<Sistema>, Model, `XModelMap`, `Result<T>`, `UnitOfWork`, `AddTransient`). Sem EF Core e sem AutoMapper.

- [x] Contrato no Domain (`IFoodExternalProvider`, `OpenFoodFactsOptions` e Models em `Domain/Http/OpenFoodFacts/`)
- [x] Implementação na Infrastructure (`OpenFoodFactsClient` com HttpClient tipado e User-Agent)
- [x] Extração de macros normalizados para 100g e descarte de itens incompletos
- [x] Resolução em cascata com auto-cache no SQL Server via UnitOfWork (`IsCustom = 1`)
- [x] Ajuste semântico nos prompts de IA (LLM atua apenas como parser de alimento/gramas)
- [x] Testes unitários para Open Food Facts, fallback cache e resolução de linguagem natural
- [x] Validação com dotnet build e dotnet test (100% passing)

