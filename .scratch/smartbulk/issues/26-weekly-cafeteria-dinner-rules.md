# 26: Ingestão Semanal de 5 Dias em 1 Print, Confirmação em Lote e Regras da Janta (0.75x e Skip)

**What to build:** Ajustar o fluxo do cardápio do refeitório corporativo à rotina real do usuário em 3 pontos essenciais:
1. **Ingestão de 1 Print para os 5 Dias Úteis + Confirmação da Semana:** Envio de apenas 1 print/foto contendo a semana toda, extraindo Almoço e Jantar de Segunda a Sexta com mapeamento explícito de datas na LLM, persistindo os 5 dias e permitindo confirmar a semana inteira em 1 clique ("Confirmar Semana Inteira"), com indicadores visuais por dia nas abas (`Seg`, `Ter`, `Qua`, `Qui`, `Sex`).
2. **Opção "Não Jantar no Refeitório" em Dias Específicos:** Possibilidade de desmarcar individualmente uma refeição (especialmente a Janta) como "Não comi / Não vou jantar lá" (`PlateProfile.None` / multiplicador `0.0x`), garantindo que as calorias e proteínas correspondentes retornem integralmente ao Gap diário a ser batido em casa.
3. **Regra Fixa da Janta (Padrão 0.75x e Sem Repetição):** Toda refeição do tipo Janta (`Dinner`) nasce automaticamente com o perfil/multiplicador reduzido de `0.75x` (em relação à porção base do almoço de `1.0x`), sem opção de repetição (`1.25x`), e com botão rápido de 1 clique "Hoje não janto lá" no card e no Telegram.

**Blocked by:** 07: Ingestão do cardápio do refeitório; 08: Revisar e confirmar cardápio; 09: Perfil de prato e HasCafeteria; 20: Tela Cardápio da semana

**Status:** done

Padrões: seguir `.agents/skills/backend/SKILL.md` (Domain/Http/<Sistema>, Infrastructure/Http/<Sistema>, `Result<T>`, isolamento de camadas, manual mapping, Dapper) e frontend standards Next.js App Router, Tailwind, Framer Motion.

- [x] Prompt de extração semanal atualizado para instruir o modelo a extrair os 5 dias úteis (Segunda a Sexta, Almoço e Jantar) mapeados para as datas reais da semana
- [x] Endpoint e serviço de confirmação em lote da semana (`ConfirmWeekAsync` / `POST /api/cafeteriamenu/confirm-week`)
- [x] Aba de dias com indicadores visuais no Next.js (dias com cardápio preenchido vs pendências vs vazios)
- [x] Regra padrão da Janta (`Dinner` nasce como `PlateProfile.Small` / `0.75x`, Almoço nasce como `PlateProfile.Standard` / `1.0x`)
- [x] Opção "Hoje não janto lá" / "Não comi" (`PlateProfile.None` / `0.0x`), devolvendo 100% dos macros da janta ao Gap diário
- [x] Remoção da opção `1.25x` no card da Janta e no teclado do bot do Telegram
- [x] Botão rápido de 1 clique "Hoje não janto lá" no card da janta na tela `/cardapio`
- [x] Testes unitários e de integração atualizados e 100% aprovados

