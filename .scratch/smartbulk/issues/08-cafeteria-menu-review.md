# 08: Revisar e confirmar cardápio

**What to build:** Revisar o rascunho do cardápio, corrigir porções e alimentos de cada componente e confirmar o cardápio da semana, mostrando o total nutricional estimado do prato Padrão de cada refeição.

**Blocked by:** 05: Núcleo de cálculo nutricional; 07: Ingestão do cardápio do refeitório

**Status:** done

Padrões: seguir `.agents/skills/backend/SKILL.md` e `.agents/skills/mandatory-rules.md` (Model, `XModelMap`, `Result<T>`, `UnitOfWork`, `AddTransient`, DDL com Id/Uuid/CreatedAt/UpdatedAt/Deleted, soft delete). Sem EF Core e sem AutoMapper. Contexto: `../spec.md`.

- [x] Consultar um cardápio com componentes e total nutricional por refeição (prato Padrão 1.0x)
- [x] Editar gramas, trocar o alimento casado, adicionar e remover componentes
- [x] Confirmar o cardápio só é permitido sem componentes pendentes; muda o estado para Confirmado
- [x] Cardápio confirmado não aceita mais edição de componentes (precisa reabrir explicitamente)
- [x] Testes do service e de integração dos casos acima
