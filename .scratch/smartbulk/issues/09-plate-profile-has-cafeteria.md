# 09: Perfil de prato e HasCafeteria

**What to build:** Registrar o perfil do prato (Padrão, Bom ou Ruim) por refeição do refeitório e marcar por dia se há refeitório ou não, para que o Gap use o que está realmente garantido.

**Blocked by:** 05: Núcleo de cálculo nutricional; 08: Revisar e confirmar cardápio

**Status:** done

Padrões: seguir `.agents/skills/backend/SKILL.md` e `.agents/skills/mandatory-rules.md` (Model, `XModelMap`, `Result<T>`, `UnitOfWork`, `AddTransient`, DDL com Id/Uuid/CreatedAt/UpdatedAt/Deleted, soft delete). Sem EF Core e sem AutoMapper. Contexto: `../spec.md`.

- [x] Registro do perfil por refeição (almoço e janta, por data); sem registro vale Padrão 1.0x
- [x] Flag `HasCafeteria` por data, com padrão verdadeiro de segunda a sexta e falso aos fins de semana, e possibilidade de sobrescrever
- [x] Consulta do garantido no refeitório em um dia: soma das refeições do cardápio confirmado × multiplicador do perfil, zero se `HasCafeteria` for falso ou se não houver cardápio confirmado
- [x] Testes do service e do cálculo com os três perfis e com dia sem refeitório
