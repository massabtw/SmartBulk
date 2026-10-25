# 11: Templates de refeição

**What to build:** Cadastrar templates de refeição de casa (por exemplo café da manhã e shake) com ingredientes e gramas, vendo o total nutricional de cada um.

**Blocked by:** 03: Catálogo de alimentos

**Status:** done

Padrões: seguir `.agents/skills/backend/SKILL.md` e `.agents/skills/mandatory-rules.md` (Model, `XModelMap`, `Result<T>`, `UnitOfWork`, `AddTransient`, DDL com Id/Uuid/CreatedAt/UpdatedAt/Deleted, soft delete). Sem EF Core e sem AutoMapper. Contexto: `../spec.md`.

- [x] Tabelas de template e de ingredientes (alimento e gramas), com tipo da refeição (café, shake, refeição completa)
- [x] CRUD completo do template com seus ingredientes
- [x] Consulta devolve o total nutricional do template
- [x] Marcar alimentos favoritos (usados nas sugestões de ajuste)
- [x] Testes do service e de integração
