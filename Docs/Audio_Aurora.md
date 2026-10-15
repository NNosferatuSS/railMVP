# Áudio — Aurora

Todo o áudio do jogo é **sintetizado** por `Art/Source/Audio/aurora_synth.py` (numpy/scipy), sem
samples externos. Tudo em **Ré menor** (melodias de SFX na pentatônica D F G A C) a **124 BPM**, pra
que qualquer efeito caia "dentro" da música.

```
python Art/Source/Audio/aurora_synth.py Assets/Audio/Resources/AuroraAudio        # tudo
python Art/Source/Audio/aurora_synth.py Assets/Audio/Resources/AuroraAudio sfx    # só SFX
python Art/Source/Audio/aurora_synth.py Assets/Audio/Resources/AuroraAudio music  # só música
```

Previews pra ouvir fora do Unity: `Art/Source/Audio/preview_full_mix.wav` (mix da corrida) e
`preview_menu_mix.wav` (mix do menu).

## Runtime

`Scripts/RailSwitchMVP/Audio/AuroraAudio.cs` se cria sozinho (`RuntimeInitializeOnLoadMethod`),
sobrevive entre cenas e só **escuta eventos** — não toca em gameplay.

Volumes do usuário: sliders de Música e SFX do Pause (`settings.music` / `settings.sfx`). O slider
de música não mexe mais no `AudioListener.volume` global.

Importação automática: `Editor/Art/AuroraAudioImporter.cs` (música Vorbis comprimida em memória;
SFX descomprimidos no load, mono quando não precisam de estéreo).

## Música adaptativa

5 loops de 16 compassos (31 s), tocando sincronizados (`PlayScheduled` no mesmo dspTime):

| Camada | Conteúdo | Menu | Warmup | Corrida | Morte |
|---|---|---|---|---|---|
| `music_pad` | supersaw de aurora, Dm–B♭–F–C | ✔ | ✔ | ✔ | ✔ |
| `music_arp` | arpejo pluck hipnótico com delay ping-pong | ✔ | ✔ | ✔ | ✔ baixo |
| `music_drums` | kick, clap, hats, viradas | — | — | ✔ | — |
| `music_bass` | baixo com sidechain do kick | — | leve | ✔ | leve |
| `music_sparkle` | sinos FM agudos | ✔ | leve | entra com o tier | leve |

- Menu: low-pass 3.2 kHz (sonhador). Warmup: 6 kHz, sem bateria — a bateria "cai" no GO.
- Morte: low-pass 520 Hz + pitch 0.86 (a música "desliga"). Pause: low-pass 900 Hz.

## SFX

| Evento | Clip | Detalhe |
|---|---|---|
| Coin | `sfx_coin_0..9` | **combo**: cada coin dentro de 1.4 s sobe uma nota da escala |
| Gem | `sfx_gem` | arpejo de sinos |
| Pegar power-up | `sfx_powerup_collect` | riser + acorde |
| Power-up ativa | `sfx_powerup_activate` | pitch por tipo (intervalos da escala) |
| Debuff | `sfx_debuff` | warp descendente |
| Power-up acaba / escudo quebra | `sfx_powerdown` / `sfx_shield_break` | vidro estilhaçando |
| Troca de conector (input) | `sfx_switch_left/mid/right` | grave → agudo, panorâmico |
| Lacuna / pouso | `sfx_whoosh` / `sfx_land` | sutis |
| Tier | `sfx_tier_up` | riser + impacto + acorde Dm add9 |
| Impacto (shake ≥ médio) | `sfx_impact` | |
| Morte / Revive | `sfx_death` / `sfx_revive` | |
| Countdown | `sfx_count` (3, 2, 1) / `sfx_go` | |
| Qualquer botão de UI | `sfx_ui_click` | ligado automaticamente em todos os `Button` |

Ganchos adicionados (só notificam): `SwitchController.AnyNudged`, `WarmupController.CountdownStep`
(+ os já usados pelo juice visual).
