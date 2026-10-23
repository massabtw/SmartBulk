# 16: Plano da semana em casa

**What to build:** Montar o plano das refeições de casa da semana: café e shake de segunda a sexta, e refeições completas de sábado, domingo e dias sem refeitório, escolhendo templates.

**Blocked by:** 09: Perfil de prato e HasCafeteria; 11: Templates de refeição

**Status:** ready-for-agent

Padrões: seguir `.agents/skills/backend/SKILL.md` e `.agents/skills/mandatory-rules.md` (Model, `XModelMap`, `Result<T>`, `UnitOfWork`, `AddTransient`, DDL com Id/Uuid/CreatedAt/UpdatedAt/Deleted, soft delete). Sem EF Core e sem AutoMapper. Contexto: `../spec.md`.

- [ ] Tabelas do plano semanal e dos itens (data, refeição, template)
- [ ] Criar e editar o plano de uma semana (segunda a domingo)
- [ ] Consulta indica quais refeições faltam preencher, considerando `HasCafeteria` de cada dia
- [ ] Total nutricional do dia planejado
- [ ] Testes do service e de integração
