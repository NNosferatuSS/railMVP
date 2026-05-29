# Difficulty Tools — Inspector Labels + Tier Lock + rowsAhead per Tier

> Sessão 2026-05-28.

## O que mudou

### 1. DifficultyTier — foldouts com label no Inspector

`DifficultyTierDrawer` (`Editor/DifficultyTierDrawer.cs`) é um `PropertyDrawer`
para `DifficultyTier`. A lista de tiers no Inspector de `DifficultyConfig` agora
exibe cada item colapsado com o resumo:

```
► Tier 0  —  0 m  |  speed 5  |  1-3/row  |  pop 0%
► Tier 1  —  30 m  |  speed 5  |  2-3/row  |  pop 55%
...
```

Clique no foldout pra expandir só o tier que quer editar. `[Range]`, `[Header]`
e `[Tooltip]` dentro do struct continuam funcionando normalmente quando expandido.
Estado de foldout persiste na sessão do editor (dicionário estático por `propertyPath`).

### 2. Tier Lock — atalho de teclado configurável

`SpawnOverrideController` ganhou:

```csharp
public Key tierLockKey = Key.F3;
```

Configurável no Inspector. Comportamento do shortcut:

- **F3 (lock):** captura `DifficultyManager.CurrentTierIndex` no momento e
  ativa `tierLockEnabled = true`. `lockedTierIndex` é escrito **só aqui** —
  nunca muda sozinho em outro momento.
- **F3 (unlock):** desativa `tierLockEnabled`. `DifficultyManager` volta a
  avançar tier por distância normalmente.
- `lockedTierIndex` no Inspector só muda se você editar manualmente.

O painel F2 mostra a tecla no label: `Tier lock: OFF (auto-advance, current: 2) [F3]`.

### 3. Tier Lock — feedback visual no HUD

`HUDController.LateUpdate` verifica `SpawnOverrideController.Instance.tierLockEnabled`
todo frame e muda a cor do `tierText`:

- **Vermelho** quando lock ON
- **Branco** quando lock OFF

Reage instantaneamente ao F3, independente de eventos de tier.

### 4. rowsAhead por tier em `DifficultyTier`

Novo campo no struct:

```csharp
[Header("Streaming")]
[Min(1)]
public int rowsAhead;
```

**Motivação:** tiers rápidos precisam de mais rows pré-spawnadas pra evitar
pop-in visível. Tiers lentos no início do jogo se beneficiam de valor menor
(resultados aparecem mais rápido no editor, menos memória).

**Prioridade no `RailManager.GetEffectiveRowsAhead()`:**

```
SpawnOverrideController (debug F2) > tier.rowsAhead > config.rowsAhead (fallback)
```

`config.rowsAhead` (`RailGenConfig`) é usado apenas se `tier.rowsAhead < 1`
(migração — tiers sem o campo serializados como 0 caem no fallback).

**Defaults em `DifficultyConfig_Default.asset`:**

| Tier | Trigger | Speed | rowsAhead |
|------|---------|-------|-----------|
| 0    | 0 m     | 5     | 6         |
| 1    | 30 m    | 5     | 8         |
| 2    | 150 m   | 7     | 10        |
| 3    | 500 m   | 14    | 14        |
| 4    | 800 m   | 16    | 16        |
| 5    | 1200 m  | 18    | 18        |

## Setup no Editor

Nenhum setup manual necessário — tudo via Inspector.

- **Trocar tecla do Tier Lock:** `SpawnOverrideController` → campo `Tier Lock Key`.
- **Ajustar rowsAhead por tier:** `DifficultyConfig_Default` → abrir tier desejado
  → campo `Rows Ahead`.
- **Override de debug pontual:** painel F2 → `rowsAhead override: ON` — tem
  prioridade sobre o tier.

## Tuning

- `rowsAhead` deve ser pelo menos `playerSpeed × tempo_de_reação / tile_length`.
  Com tile length ~5 e 0.5s de reação: tier speed 18 → mínimo ≈ 2 rows.
  O valor real precisa ser maior pra esconder o pop-in (18 rows é conservador).
- Se aparecer pop-in num tier específico, aumente `rowsAhead` daquele tier.
  Se o editor ficar lento no tier 0, reduza (6 já é bem leve).
- O debug override do F2 é o lugar certo pra testar o valor ideal antes de
  commitar no asset.

## Troubleshooting

- **Tier lock não travar no tier certo:** verifique se `DifficultyManager.Instance`
  não é null no momento do F3. Em cenas sem DifficultyManager o lock ignora a
  captura e `lockedTierIndex` fica em 0.
- **rowsAhead ignorado:** confere se `tier.rowsAhead >= 1`. Valor 0 = fallback
  para `config.rowsAhead`. Unity serializa int não inicializado como 0 em assets
  antigos — abra o tier no Inspector e coloque o valor correto.
- **HUD não fica vermelho:** confere se `tierText` está assignado no `HUDController`
  e se `SpawnOverrideController.Instance` não é null na cena.
