# 04: Peso corporal e creatina

**What to build:** Registrar o peso do dia e marcar o hábito diário de creatina, e consultar o histórico de ambos.

**Blocked by:** 01: Solution, banco e DbUp

**Status:** done

Padrões: seguir `.agents/skills/backend/SKILL.md` e `.agents/skills/mandatory-rules.md` (Model, `XModelMap`, `Result<T>`, `UnitOfWork`, `AddTransient`, DDL com Id/Uuid/CreatedAt/UpdatedAt/Deleted, soft delete). Sem EF Core e sem AutoMapper. Contexto: `../spec.md`.

- [x] Registro de peso por dia (um por dia; novo registro no mesmo dia substitui o anterior)
- [x] Check de creatina por dia, com marcar e desmarcar
- [x] Consulta de histórico por intervalo de datas para peso e creatina
- [x] Endpoint que devolve o peso mais recente (usado no cálculo da meta de proteína)
- [x] Validação do peso em faixa plausível
- [x] Testes de integração do repositório e do service
