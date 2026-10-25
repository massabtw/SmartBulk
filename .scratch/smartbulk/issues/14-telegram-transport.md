# 14: Telegram: transporte e registro por mensagem

**What to build:** Falar com o sistema pelo Telegram: mensagens de texto registram refeições e o botão Desfazer funciona. Em desenvolvimento o bot usa long polling e em produção usa webhook, com o mesmo tratamento de mensagens.

**Blocked by:** 13: Registro de refeição em linguagem natural

**Status:** done

Padrões: seguir `.agents/skills/backend/SKILL.md` e `.agents/skills/mandatory-rules.md` (Model, `XModelMap`, `Result<T>`, `UnitOfWork`, `AddTransient`, DDL com Id/Uuid/CreatedAt/UpdatedAt/Deleted, soft delete). Sem EF Core e sem AutoMapper. Contexto: `../spec.md`.

- [x] Cliente HTTP do Telegram atrás de interface no Domain, com contratos em `Domain/Http/Telegram/Models/`
- [x] Handler único de mensagens e callbacks usado pelos dois transportes
- [x] Long polling em `IHostedService` ativado por configuração (desenvolvimento)
- [x] Endpoint de webhook protegido por secret token (produção)
- [x] Apenas o `chat_id` configurado é atendido; os demais são ignorados sem resposta
- [x] Texto livre chama o registro em linguagem natural e responde com resumo, Gap e botão Desfazer
- [x] Token do bot e `chat_id` via configuração, sem segredos no repositório
- [x] Testes do handler com cliente Telegram falso
