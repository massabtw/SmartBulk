# 10: Registro de refeições e Gap do dia

**What to build:** Registrar o que comi em casa de forma estruturada (alimento e gramas), desfazer um registro e consultar a visão do dia com meta, consumido, garantido no refeitório e Gap restante em kcal e proteína.

**Blocked by:** 02: API key e configurações; 03: Catálogo de alimentos; 05: Núcleo de cálculo nutricional; 09: Perfil de prato e HasCafeteria

**Status:** done

Padrões: seguir `.agents/skills/backend/SKILL.md` e `.agents/skills/mandatory-rules.md` (Model, `XModelMap`, `Result<T>`, `UnitOfWork`, `AddTransient`, DDL com Id/Uuid/CreatedAt/UpdatedAt/Deleted, soft delete). Sem EF Core e sem AutoMapper. Contexto: `../spec.md`.

- [x] Tabela de registros de refeição com data/hora, alimento, gramas e nutrientes calculados
- [x] Registrar vários itens de uma vez como uma refeição e desfazer o último registro ou uma refeição inteira (soft delete)
- [x] Endpoint do dia devolve meta, consumido, garantido no refeitório e Gap, em kcal e proteína
- [x] A meta de proteína usa o peso mais recente; sem peso, usa um aviso e ignora a meta de proteína
- [x] Testes de integração do cenário completo: refeitório confirmado, registro em casa e Gap esperado
