# 03: Catálogo de alimentos (TACO)

**What to build:** Ter um catálogo de alimentos com valores nutricionais da Tabela TACO, buscável por nome, mais alimentos customizados cadastrados por mim. Cada alimento pode ter um fator de conversão de peso cru para pronto.

**Blocked by:** 01: Solution, banco e DbUp

**Status:** done

Padrões: seguir `.agents/skills/backend/SKILL.md` e `.agents/skills/mandatory-rules.md` (Model, `XModelMap`, `Result<T>`, `UnitOfWork`, `AddTransient`, DDL com Id/Uuid/CreatedAt/UpdatedAt/Deleted, soft delete). Sem EF Core e sem AutoMapper. Contexto: `../spec.md`.

- [x] Tabela de alimentos com kcal, proteína, carboidrato e gordura por 100 g, flag `IsCustom` e `RawYieldFactor` opcional
- [x] Seed único da TACO aplicado por script versionado pelo DbUp (arquivo de dados dentro do repositório)
- [x] Seed de `RawYieldFactor` para itens comuns (arroz, feijão, macarrão, frango, carne, batata)
- [x] Busca por nome (sem diferenciar acento e caixa) com paginação simples
- [x] CRUD de alimento customizado; alimentos da TACO não podem ser alterados nem removidos
- [x] Testes de integração da busca e do CRUD
