# 07: Ingestão do cardápio do refeitório

**What to build:** Enviar o cardápio semanal da empresa (texto, JSON ou imagem) e receber um rascunho de cardápio, com almoço e janta de segunda a sexta decompostos em componentes com porção estimada em gramas, cada um casado com um alimento da TACO.

**Blocked by:** 03: Catálogo de alimentos; 06: Cliente LLM compatível com OpenAI

**Status:** done

Padrões: seguir `.agents/skills/backend/SKILL.md` e `.agents/skills/mandatory-rules.md` (Model, `XModelMap`, `Result<T>`, `UnitOfWork`, `AddTransient`, DDL com Id/Uuid/CreatedAt/UpdatedAt/Deleted, soft delete). Sem EF Core e sem AutoMapper. Contexto: `../spec.md`.

- [x] Tabelas do cardápio semanal, das refeições (almoço e janta por dia) e dos componentes com gramas e alimento associado
- [x] Endpoint que aceita texto, JSON ou imagem e usa o `ILlmClient` para propor os componentes
- [x] Resposta da LLM validada com FluentValidation, com uma única nova tentativa se for inválida
- [x] Casamento de cada componente com o catálogo; componente sem correspondência é salvo marcado como pendente
- [x] O cardápio é salvo com estado Rascunho e só estados confirmados entrarão no cálculo
- [x] Testes do service com `ILlmClient` falso e testes de integração da persistência
