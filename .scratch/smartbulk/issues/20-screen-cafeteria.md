# 20: Tela Cardápio da semana

**What to build:** Enviar o cardápio da semana, revisar as porções propostas, confirmar e ajustar o perfil do prato e o dia sem refeitório.

**Blocked by:** 08: Revisar e confirmar cardápio; 09: Perfil de prato e HasCafeteria; 18: Fundação do frontend Next.js

**Status:** ready-for-agent

Padrões: seguir as skills de frontend listadas no `AGENTS.md` (Next.js App Router, React/Vercel, Tailwind, Framer Motion, web-design-guidelines). Dark mode, mobile-first, microinterações suaves. Contexto: `../spec.md`.

- [ ] Envio do cardápio por texto, JSON ou imagem
- [ ] Revisão por refeição com edição de gramas, troca de alimento e pendências destacadas
- [ ] Botão de confirmar habilitado só sem pendências
- [ ] Seletor de perfil (Padrão, Bom, Ruim) por refeição e alternância de `HasCafeteria` por dia
- [ ] Total nutricional estimado visível por refeição
