# 18: Fundação do frontend Next.js

**What to build:** Ter o app web rodando com o visual e a infraestrutura base: navegação mobile-first em tema escuro, e um BFF no servidor que fala com a API sem expor a chave no navegador.

**Blocked by:** 02: API key e configurações do usuário

**Status:** ready-for-agent

Padrões: seguir as skills de frontend listadas no `AGENTS.md` (Next.js App Router, React/Vercel, Tailwind, Framer Motion, web-design-guidelines). Dark mode, mobile-first, microinterações suaves. Contexto: `../spec.md`.

- [ ] Projeto Next.js (App Router, TypeScript, Tailwind) com tema escuro, tipografia e tokens de design
- [ ] Layout com navegação inferior para Hoje, Cardápio, Compras, Plano e Configurações, com transições suaves
- [ ] BFF com Route Handlers ou Server Actions que repassam as chamadas à API; a API key só existe no servidor
- [ ] Cliente tipado da API e tratamento padrão de erro e carregamento
- [ ] Variáveis de ambiente documentadas e sem segredos no repositório
- [ ] Páginas vazias de cada seção navegáveis
