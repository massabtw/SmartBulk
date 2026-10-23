# 15: Lembretes de almoço e jantar

**What to build:** Receber no Telegram, no horário do almoço e do jantar, uma pergunta com botões Padrão, Prato bom, Prato ruim e Não comi, que grava o perfil da refeição.

**Blocked by:** 09: Perfil de prato e HasCafeteria; 14: Telegram: transporte e registro por mensagem

**Status:** ready-for-agent

Padrões: seguir `.agents/skills/backend/SKILL.md` e `.agents/skills/mandatory-rules.md` (Model, `XModelMap`, `Result<T>`, `UnitOfWork`, `AddTransient`, DDL com Id/Uuid/CreatedAt/UpdatedAt/Deleted, soft delete). Sem EF Core e sem AutoMapper. Contexto: `../spec.md`.

- [ ] `IHostedService` que dispara nos horários e no fuso das configurações, apenas em dias com `HasCafeteria` verdadeiro e cardápio confirmado
- [ ] Mensagem cita o prato do dia e oferece os quatro botões
- [ ] Cada botão grava o perfil correspondente; Não comi zera o garantido daquela refeição
- [ ] Sem resposta, vale Padrão 1.0x; a mesma refeição não é perguntada duas vezes
- [ ] Testes do agendamento com relógio falso e do tratamento dos botões
