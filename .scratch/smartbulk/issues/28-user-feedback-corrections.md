# Ticket 28: Business Rules & UI Refinements (Correções de Regras e UI)

## Status: `ready-for-agent`

## Contexto & Objetivos
Com base em testes reais de uso em produção, o sistema precisa de 6 correções pontuais em regras de negócio e interface:

1. **Remover Card "Shake de hoje" da Tela `Hoje` (Next.js):**
   - Na tela principal (`Hoje`), remover o card fixo `ShakeSuggestionCard`. O usuário acompanha suas refeições planejadas na aba `Plano` e o balanço calórico no resumo do `Gap`.
2. **Banir "Ovos" e "Whey Protein" de Planos e Compras:**
   - O usuário não come ovos em nenhuma forma e não consome Whey.
   - Banir 100% de ovos e whey de templates gerados, planos automáticos e listas de compras.
   - Utilizar fontes práticas Zero Fogão: Leite integral, queijo muçarela/prato, peito de peru/presunto cozido, requeijão cremoso, iogurte, pasta de amendoim integral, aveia em flocos, granola, pão de forma e banana.
3. **Exibir Ingredientes Detalhados no `Plano`:**
   - O endpoint da aba `Plano` deve retornar a lista de `Ingredients` de cada refeição (nome, gramas/medida prática, calorias e proteínas).
   - A UI da aba `Plano` deve listar claramente os ingredientes de cada refeição abaixo do título.
4. **Cardápio do Refeitório: Separar "Arroz/Feijão" e Zero "sem alimento associado":**
   - Itens contendo "Arroz/Feijão" ou "Arroz e Feijão" devem ser automaticamente desmembrados em 2 itens: `Arroz branco cozido` e `Feijão carioca cozido`.
   - Se o match da TACO não for de alimento pronto/preparado (nunca associar prato pronto a item cru nem batata frita a batata doce), criar/associar alimento customizado com estimativas reais da LLM, garantindo zero itens com 0 kcal ou sem alimento associado.
5. **Regra Madero/Jeronimo: Dia de Hambúrguer = "Cheeseburger Jeronimo" (P, M, G, GG):**
   - Ao detectar hambúrguer ou seus componentes (`Pão Brioche` + `Hamburger Bovino` + `Maionese`), unificar no item oficial `Cheeseburger Jeronimo`.
   - Suportar tamanhos com macros oficiais:
     - P (135g): ~370 kcal, 17g prot, 29g carb, 20g gord
     - M (195g): ~540 kcal, 29g prot, 30g carb, 34g gord (Padrão)
     - G (255g): ~710 kcal, 41g prot, 31g carb, 47g gord
     - GG (315g): ~880 kcal, 53g prot, 32g carb, 60g gord
   - Na UI do cardápio, exibir seletor rápido `[ P | M | G | GG ]` no item de hambúrguer.
6. **Medidas Caseiras de Refeitório no Cardápio (Colheres, Conchas, Filés) com Botões +/-:**
   - Exibir medidas caseiras ao lado dos gramas: colheres de servir (arroz/macarrão/farofa/legumes - 60g), conchas (feijão/caldo - 140g), filés/pedaços (carnes/frango/peixe - 80g), porções/unidades (batata frita - 100g, sobremesas/frutas).
   - Botões rápidos `-` e `+` para ajustar porções com 1 clique.

