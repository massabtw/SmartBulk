# 05: Núcleo de cálculo nutricional (Domain)

**What to build:** Ter as regras nutricionais puras e testadas no Domain, sem nenhuma dependência externa: macros de um prato, multiplicadores do prato do refeitório e o Gap diário.

**Blocked by:** 01: Solution, banco e DbUp

**Status:** done

Padrões: seguir `.agents/skills/backend/SKILL.md` e `.agents/skills/mandatory-rules.md` (Model, `XModelMap`, `Result<T>`, `UnitOfWork`, `AddTransient`, DDL com Id/Uuid/CreatedAt/UpdatedAt/Deleted, soft delete). Sem EF Core e sem AutoMapper. Contexto: `../spec.md`.

- [x] Valor nutricional (kcal, proteína, carboidrato, gordura) com soma e multiplicação por fator
- [x] Cálculo de nutrientes de um item a partir dos valores por 100 g e da quantidade em gramas
- [x] Perfis de prato: Padrão 1.0x, Prato bom 1.25x e Prato ruim 0.75x aplicados ao prato inteiro
- [x] Gap diário = meta − (consumido + garantido no refeitório), em kcal e proteína, nunca abaixo de zero
- [x] Meta de proteína = g/kg × peso mais recente
- [x] Testes unitários cobrindo cada regra e os casos de borda (zero, excesso, sem peso)
