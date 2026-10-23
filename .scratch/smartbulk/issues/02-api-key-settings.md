# 02: API key e configurações do usuário

**What to build:** Proteger a API com uma API key única e entregar a primeira fatia completa: as configurações do usuário (usuário único, sem tabela de usuários). Permite ler e alterar meta calórica, meta de proteína, fuso e horários dos lembretes.

**Blocked by:** 01: Solution, banco e DbUp

**Status:** done

Padrões: seguir `.agents/skills/backend/SKILL.md` e `.agents/skills/mandatory-rules.md` (Model, `XModelMap`, `Result<T>`, `UnitOfWork`, `AddTransient`, DDL com Id/Uuid/CreatedAt/UpdatedAt/Deleted, soft delete). Sem EF Core e sem AutoMapper. Contexto: `../spec.md`.

- [x] Requisições sem a API key válida recebem 401; o endpoint de saúde continua público
- [x] Tabela de configurações com meta de kcal (padrão 3000), proteína em g/kg (padrão 2,0), fuso (padrão America/Sao_Paulo) e horários de lembrete (padrão 12:30 e 19:30)
- [x] GET devolve as configurações (cria o registro padrão se não existir) e PUT altera, com validação em FluentValidation
- [x] Entity, repositório, Model, `XModelMap`, service com `Result<T>` e controller seguindo a skill backend
- [x] Testes de integração do repositório e testes do service/validação
