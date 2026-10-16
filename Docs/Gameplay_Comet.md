# COMET — power-up overpower

**Fantasia:** por alguns trilhos o jogador vira um cometa. Nada pode pará-lo, e o que antes era ameaça vira recompensa.

## O que faz (por `cometTiles` trilhos, padrão 10)
- **Mais rápido**: velocidade ×1,6 com rampa de entrada (0,35 s) e de saída (0,8 s).
- **Piloto automático**: o jogo escolhe a rota. No cometa ele **mira hazards e moedas**: hazard vale 3, moeda vale 1, e no empate segue reto (`AutoCriticalFollower.FindCometOffset`).
- **Não cai**: um conector pro vazio vira sozinho pra um trilho que existe (`RunModifiers.CometProtection`).
- **SMASH!**: hazard ou debuff tocado é esmagado e vira **+5 moedas** (× multiplicadores), com corrente `SMASH! x3`, faíscas, tremida de câmera e um pouco de Fever.
- **Ímã gigante**: raio 9 (o normal é 4), que puxa as moedas das lanes vizinhas.
- **Fim — SHOCKWAVE**: todos os hazards até 45 m à frente viram moedas. Depois vêm 2 trilhos de carência sem queda, e o controle volta ao jogador.
- **Feedback**: rastro dourado 2,6× mais largo, chuva de faíscas, pérola em brasa, brilho dourado na pista e a camada Fever da música.

## Onde está
- Estado (duração em tiles): `PowerUpManager.GrantComet` / `HasComet` (`PowerUpType.Comet`).
- Efeitos: `Gameplay/CometController.cs`. O SMASH sai de `ObstacleBase.Smash`.
- Pickup: `PowerUps/CometPickup.cs`. Prefab: `Prefabs/Spawnables/PowerUpVariant_Comet Variant.prefab`.
- Parâmetros: `GameplayLayerConfig`, seção "Cometa" (Control Panel).
- Spawn: pools Tier4 (0,2), Tier5 (0,25) e Tier6 (0,3, usado do tier 6 em diante), com cooldown de 50–60 rows. Também pode sair da Mystery Box.
- Setup idempotente: `Editor/Gameplay/CometSetup.cs`, chamado pelo Aurora Apply All. Cria o prefab, os pools, o binding no gerador e o ícone no HUD.
