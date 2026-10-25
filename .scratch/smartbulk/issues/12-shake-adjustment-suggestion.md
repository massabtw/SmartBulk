# 12: Sugestão de ajuste do shake

**What to build:** Receber uma sugestão determinística para fechar o Gap da noite: escalar o template do shake e, se ainda faltar, sugerir um item favorito.

**Blocked by:** 10: Registro de refeições e Gap do dia; 11: Templates de refeição

**Status:** done

Padrões: seguir `.agents/skills/backend/SKILL.md` e `.agents/skills/mandatory-rules.md` (Model, `XModelMap`, `Result<T>`, `UnitOfWork`, `AddTransient`, DDL com Id/Uuid/CreatedAt/UpdatedAt/Deleted, soft delete). Sem EF Core e sem AutoMapper. Contexto: `../spec.md`.

- [x] Regra no Domain: escala o template de 0,5x a 2x para aproximar o Gap de kcal, priorizando a proteína quando o prato do dia foi fraco
- [x] Se mesmo no limite de 2x faltar kcal ou proteína, sugere um alimento favorito com gramas calculadas
- [x] Se o Gap já está fechado, informa que não há ajuste necessário
- [x] Endpoint devolve o fator de escala, os ingredientes escalados, o item extra e o Gap resultante
- [x] Sem LLM; testes unitários cobrindo limites 0,5x e 2x, Gap zero e item extra
