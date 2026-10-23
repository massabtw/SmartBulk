# 13: Registro de refeição em linguagem natural

**What to build:** Enviar uma frase como "comi 3 ovos e 2 pães com requeijão" e ter a refeição registrada na hora, com resumo, kcal e novo Gap, e a possibilidade de desfazer.

**Blocked by:** 06: Cliente LLM compatível com OpenAI; 10: Registro de refeições e Gap do dia

**Status:** ready-for-agent

Padrões: seguir `.agents/skills/backend/SKILL.md` e `.agents/skills/mandatory-rules.md` (Model, `XModelMap`, `Result<T>`, `UnitOfWork`, `AddTransient`, DDL com Id/Uuid/CreatedAt/UpdatedAt/Deleted, soft delete). Sem EF Core e sem AutoMapper. Contexto: `../spec.md`.

- [ ] Service recebe o texto, usa o `ILlmClient` para extrair itens e quantidades e valida o JSON com uma nova tentativa
- [ ] Cada item é casado com o catálogo e a refeição é salva imediatamente
- [ ] A resposta traz resumo da refeição, kcal, o novo Gap e um identificador para desfazer
- [ ] Itens sem correspondência não são salvos e voltam como pergunta de qual alimento escolher ou cadastrar
- [ ] Falha de cota da LLM devolve mensagem amigável
- [ ] Testes com `ILlmClient` falso cobrindo sucesso, item desconhecido e cota esgotada
