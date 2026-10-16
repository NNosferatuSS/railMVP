# Combo, Fever, Zonas e Risco/Recompensa

Camada de jogo criada pra resolver o diagnóstico de `Docs/Balance/Balance_2026-10.md`
("um verbo só, caminho telegrafado, sem risco/recompensa, platô de conteúdo").
Nada disso mexe na regra de morte nem no gerador de caminho crítico.

## Ações de habilidade → barra de Fever (`Gameplay/ComboFeverManager.cs`)

| Ação | Como acontece | Ganho base |
|---|---|---|
| **CLUTCH!** | a troca de conector que te levou ao próximo trilho foi feita no último terço (progresso ≥ 0.68) | 12 |
| **RISCO!** | pousar num decoy (fora do caminho crítico garantido) e seguir | 14 |
| **RASPÃO!** | passar por um hazard na lane vizinha | 8 |
| Moeda bônus / Gema | coletar | 10 / 25 |
| Coin / power-up | coletar (não conta pra corrente) | 2.5 / 6 |

- **Corrente**: ações dentro de 2.6 s somam elos; cada elo dá +8% de ganho (até +150%).
- A barra escoa 4.5/s depois de 3.5 s sem ação. Impacto (shake ≥ médio) tira 15 e quebra a corrente.
- **Barra cheia = Fever pronto. O jogador escolhe quando soltar**: botão "SOLTAR FEVER!" na borda
  inferior, ou Espaço/↑/W. Não é item guardado (respeita a diretriz "sem inventário").
- **Fever (8 s)**: coins ×2, ímã automático, aurora/bloom/poça de luz dourados, camada `music_fever`.

## Zonas (`Gameplay/ZoneDirector.cs`)

A partir do tier 2: primeira zona 260 m após o GO; depois a cada 420 m **contados do fim da zona
anterior**, sem repetir a última. Duração 20 s. Completar vivo = +25 coins e +30 de barra.

| Zona | Regra | Visual / som |
|---|---|---|
| **RUSH** | velocidade ×1.18, coins ×2 | aurora rosa, mais partículas, música +6% de pitch com o lead do Fever |
| **CHUVA DE OURO** | coins em quase todo tile, moeda bônus em 45% dos decoys vazios | aurora dourada, brilho agudo no máximo |
| **ECLIPSE** | a parte não emissiva escurece 85%; menos hazards | aurora violeta; leitura só por conectores, contornos e coletáveis |

## Risco/recompensa

- **Moeda bônus** (`Resource_CoinBonus`, vale 5, rosa-dourada, pulsa): aparece em decoys sem
  coins, sem hazard e sem power-up — 10% a partir do tier 2, 45% na Chuva de Ouro. Spawn no
  `ProceduralRailGenerator` (campo `bonusCoinPrefab`).
- RISCO! e RASPÃO! recompensam exatamente o comportamento arriscado.

## Integração

- `Core/RunModifiers.cs`: ponto único de modificadores temporários (velocidade, coins, ímã,
  chance de moeda bônus, tier da zona). Lido por `PlayerRailRider`, `PowerUpManager`
  (`CoinMultiplier`, `HasMagnet`) e `ProceduralRailGenerator` (`ModifyTier`).
- `UI/RunHUD.cs`: Canvas próprio criado em código (barra, corrente, timer de zona, popups, banners, botão).
- Visual: `AuroraJuice` (Fever/zonas, `_AuroraDim` do Eclipse), `AuroraPlayerJuice` (crescente
  dourado no Fever; mola do squash com sub-passos — estável com engasgo de frame).
- Áudio: `sfx_clutch`, `sfx_risky`, `sfx_nearmiss`, `sfx_fever_ready/start/end`,
  `sfx_zone_start/end`, `sfx_bonus_coin` e a camada `music_fever` (6ª camada).
- Setup na cena: `Tools → RailSwitch → Art → Aurora — Apply All` (cria `Aurora_Gameplay` e liga o config).
- Parâmetros: `Config/GameplayLayerConfig.cs` → asset `ScriptableObjects/RailSwitchMVP/GameplayLayerConfig_Default.asset`,
  editável no Control Panel (`Gameplay Layer (Combo · Fever · Zonas)`). `ComboFeverManager` e `ZoneDirector`
  leem dele via campo `config`; sem asset ligado, caem nos defaults do código (com warning).
- Debug: `ZoneDirector.Instance.Begin(ZoneKind.X)`, `ComboFeverManager.Instance.AddMeter(100)`.

## Pendente

- Calibrar no aparelho: frequência de CLUTCH/RISCO num jogo real, duração do Fever, intervalo das zonas.
- Testado no Editor com piloto automático + input simulado; falta jogar com a mão.
