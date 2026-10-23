# 17: Lista de compras

**What to build:** Gerar a lista de compras da semana apenas com o que é consumido em casa, convertendo peso pronto para peso cru, e marcar itens como comprados.

**Blocked by:** 03: Catálogo de alimentos; 16: Plano da semana em casa

**Status:** ready-for-agent

Padrões: seguir `.agents/skills/backend/SKILL.md` e `.agents/skills/mandatory-rules.md` (Model, `XModelMap`, `Result<T>`, `UnitOfWork`, `AddTransient`, DDL com Id/Uuid/CreatedAt/UpdatedAt/Deleted, soft delete). Sem EF Core e sem AutoMapper. Contexto: `../spec.md`.

- [ ] Gerar a lista a partir do plano da semana, somando gramas por alimento entre todos os templates planejados
- [ ] Alimentos com `RawYieldFactor` têm a quantidade dividida pelo fator; os demais ficam como estão
- [ ] A lista mostra gramas ou unidades por alimento, sem tamanho de embalagem nem despensa
- [ ] Marcar e desmarcar item como comprado; gerar de novo preserva os itens já marcados
- [ ] Testes unitários da conversão e da agregação e teste de integração do fluxo
