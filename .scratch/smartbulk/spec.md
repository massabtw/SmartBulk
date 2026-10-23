# SmartBulk: Spec

Sistema pessoal (usuário único) de automação nutricional para bulking (~3000 kcal/dia), com rastreamento de dieta e planejador de compras. Diferencial: **cálculo reverso via refeitório corporativo**. Almoço e janta de dias úteis são comidos no refeitório, então o sistema desconta o que está garantido lá da meta diária e calcula o **Gap** a fechar em casa (café da manhã e shake noturno), além de gerar a lista de compras só do que é consumido em casa.

## Vocabulário

- **Refeitório**: refeitório da empresa; almoço e janta de segunda a sexta.
- **Cardápio**: cardápio semanal do refeitório, composto por refeições (almoço/janta por dia) e componentes (alimento + gramas). Estados: Rascunho, Confirmado.
- **Perfil do prato**: multiplicador aplicado ao prato inteiro do refeitório: Padrão 1.0x, Prato bom 1.25x, Prato ruim 0.75x. Escolhido por refeição; sem registro vale Padrão.
- **Garantido no refeitório**: nutrientes do cardápio confirmado × Perfil do prato, zero se o dia não tem refeitório.
- **Gap**: `meta diária − (consumido + garantido no refeitório)`, em kcal e proteína, nunca abaixo de zero.
- **HasCafeteria**: flag por dia (padrão verdadeiro seg–sex, falso sáb–dom), sobrescrevível para feriado, home office e férias.
- **Template de refeição**: receita de casa (café, shake, refeição completa) com ingredientes e gramas.
- **Plano da semana**: escolha de templates para cada refeição de casa da semana (segunda a domingo).
- **RawYieldFactor**: fator opcional por alimento para converter peso pronto em peso cru de compra.

## Decisões

### Arquitetura e padrões
- Backend .NET em 4 camadas: `SmartBulk.Domain`, `Application`, `Infrastructure`, `Api`. Dependência: Api → Application → Domain ← Infrastructure.
- **Os padrões Madero da skill backend prevalecem 100%** (`.agents/skills/backend/SKILL.md`): objetos de entrada/saída se chamam `Model` (classes POCO, nunca DTO), mappers estáticos `XModelMap` com `ToModel()`/`ToEntity()`, serviços retornam `Result<T>`, acesso via `UnitOfWork`, serviços com `AddTransient`, validação com FluentValidation, tabelas com `Id`, `Uuid`, `CreatedAt`, `UpdatedAt`, `Deleted` e soft delete.
- **Dapper com SQL Server. Proibido Entity Framework Core. Proibido AutoMapper.**
- Regras de cálculo nutricional puras no Domain, sem dependências externas.
- Migrations: scripts SQL numerados em `Database/`, aplicados pelo **DbUp** na inicialização da API. SQL Server local via `docker-compose`.
- Testes: xUnit para regras puras; testes de integração dos repositórios Dapper contra SQL Server em Docker.

### Usuário e segurança
- Usuário único, sem login nem tabela de usuários. API protegida por API key. Webhook do Telegram protegido por secret token e allowlist do `chat_id`.
- O frontend guarda a API key apenas no servidor (BFF), nunca no navegador.

### LLM
- `ILlmClient` no Domain, implementação HTTP na Infrastructure, contratos JSON em `Domain/Http/<Sistema>/Models/`.
- Endpoint compatível com OpenAI `chat/completions` configurável em `appsettings.json` (`BaseUrl`, `ApiKey`, `Model`). Objetivo: custo zero, usando GitHub Models (GPT-4o com PAT) ou Gemini 2.5 Pro (free tier do AI Studio) sem mudar código.
- Suporta modo JSON e imagem em base64. A validação do JSON fica na Application (FluentValidation) com uma única nova tentativa.
- Erro 429 ou de cota vira `Result` com falha, sem retry em loop; o Telegram responde que está sem cota.

### Nutrição
- Base de dados: Tabela TACO importada por seed único para o SQL Server, com alimentos customizados na mesma tabela (`IsCustom`).
- A LLM propõe componentes e gramas do cardápio; o usuário revisa e confirma. Só cardápio confirmado entra no Gap.
- Meta de kcal configurável (padrão 3000). Meta de proteína em g/kg (padrão 2,0) sobre o peso mais recente. Carboidrato e gordura sem metas rígidas.
- Sugestão do shake é determinística no Domain: escala o template de 0,5x a 2x e, se ainda faltar, sugere um alimento favorito. Sem LLM.
- Conversão pronto→cru: divide o peso pronto pelo `RawYieldFactor`; sem fator, compra como está.

### Telegram
- Mesmo handler de mensagens para dois transportes: long polling em `IHostedService` em desenvolvimento e webhook em produção.
- Registro em linguagem natural: salva direto, responde com resumo, kcal e novo Gap, com botão Desfazer. Item sem correspondência vira pergunta.
- Lembretes de almoço e jantar (padrão 12:30 e 19:30, fuso `America/Sao_Paulo`) só em dias com refeitório, com botões Padrão, Prato bom, Prato ruim e Não comi. Sem resposta vale Padrão 1.0x.

### Planejador de mercado
- Plano manual com templates para café e shake de seg–sex e refeições completas de sáb–dom e dias sem refeitório. Sem geração por IA no MVP.
- Lista da próxima semana (segunda a domingo), agregada por alimento, com check de comprado. Sem tamanho de embalagem nem controle de despensa.

### Frontend
- Next.js (App Router), React, TypeScript, Tailwind CSS e Framer Motion. Dark mode elegante, mobile-first, minimalista, com barras de progresso animadas e microinterações suaves.
- Telas do MVP: Hoje, Cardápio da semana, Compras, Templates e Plano, Configurações.

## Fora do escopo do MVP
- Geração automática de plano por IA, tamanho de embalagem, controle de despensa, multiusuário e login.

## Tickets

Ordem de dependência em `issues/`. Telegram (13–15) vem antes do planejador de mercado (16–17). O frontend (18–23) só depende do ticket 02 para começar e pode rodar em paralelo ao backend.

| # | Ticket | Bloqueado por |
|---|--------|---------------|
| 01 | Solution, banco e DbUp | nenhum |
| 02 | API key e configurações do usuário | 01 |
| 03 | Catálogo de alimentos (TACO) | 01 |
| 04 | Peso corporal e creatina | 01 |
| 05 | Núcleo de cálculo nutricional | 01 |
| 06 | Cliente LLM compatível com OpenAI | 01 |
| 07 | Ingestão do cardápio do refeitório | 03, 06 |
| 08 | Revisar e confirmar cardápio | 05, 07 |
| 09 | Perfil de prato e HasCafeteria | 05, 08 |
| 10 | Registro de refeições e Gap do dia | 02, 03, 05, 09 |
| 11 | Templates de refeição | 03 |
| 12 | Sugestão de ajuste do shake | 10, 11 |
| 13 | Registro de refeição em linguagem natural | 06, 10 |
| 14 | Telegram: transporte e registro por mensagem | 13 |
| 15 | Lembretes de almoço e jantar | 09, 14 |
| 16 | Plano da semana em casa | 09, 11 |
| 17 | Lista de compras | 03, 16 |
| 18 | Fundação do frontend Next.js | 02 |
| 19 | Tela Hoje | 04, 10, 12, 18 |
| 20 | Tela Cardápio da semana | 08, 09, 18 |
| 21 | Tela Compras | 17, 18 |
| 22 | Tela Templates e Plano | 11, 16, 18 |
| 23 | Tela Configurações | 02, 18 |

