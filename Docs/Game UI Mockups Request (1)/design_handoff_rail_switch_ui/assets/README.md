# Rail Switch — UI assets (3×, sem texto)

Exportado de `Rail Switch Export.dc.html`. **Nenhum sprite tem texto ou número embutido** — só moldura, fundo e ícone. Rótulos, contadores e valores entram no Unity com TextMeshPro por cima.

Exceção intencional: `controls/btn_icon_*` (pause, ×, voltar, +, menu, chevron) — o glifo é o próprio ícone, então está desenhado no sprite.

## Import no Unity
- Texture Type **Sprite (2D and UI)** · Sprite Mode Single · PPU **100**
- Filter **Bilinear** · Compression High Quality · **Alpha Is Transparency** marcado
- Mesh Type **Full Rect** (obrigatório para 9-slice)

## 9-slice (Sprite Editor → Border), px @3×
| Sprite | Border |
|---|---|
| panels/panel_lg.png, panels/modal.png | 72 |
| panels/panel_md.png, panels/panel_accent_*.png | 48 |
| panels/row_sm.png, panels/banner_hazard_*.png | 36 |
| panels/pill_dark.png | 57 L/R apenas |
| buttons/btn_*.png (grandes) | 54 |
| buttons/btn_sm_*.png | 36 |
| controls/tabs_track.png | 42 |
| controls/tabs_thumb.png | 33 |
| controls/toggle_on/off.png | 45 L/R |
| controls/slider_track, slider_fill | 9 L/R |
| controls/bar_track, bar_fill_* | 12 L/R |
| economy/chip_*.png | 45 L/R |
| economy/badge_*.png | 18 |
| overlays/toast_*.png | 57 L/R |
| overlays/tooltip.png | 30 |
| overlays/scrim.png | tile/stretch livre |

Não fatie: `frames/*`, `icons/*`, `economy/icon_*`, `economy/puck_*`, `controls/btn_icon_*`, `controls/fab_violet.png`, `controls/slider_knob.png`, `backgrounds/*`.

## Como montar cada componente no Unity
- **Botão** = Image (btn_primary, 9-slice) + TextMeshPro filho, Chakra Petch 700, tracking .10em, cor `#05210D` (verde) / `#2A1600` (dourado) / `#FFFFFF` (violeta/ghost). Estado pressed: troca para o sprite `_pressed` e desloca 3 px em Y.
- **Chip de moeda** = chip_gold (9-slice) + Image do `economy/icon_coin.png` + TMP à direita.
- **Puck de power-up** = puck_ring_* como Image tipo **Filled / Radial 360** (o anel é o timer) + puck_tile_* no centro + `icons/icon_*` por cima + badge_gold com TMP dos segundos.
- **Barra de progresso** = bar_track + bar_fill_* como Image Filled / Horizontal.
- **Slider** = slider_track + slider_fill (Fill Rect) + slider_knob (Handle).
- **Tabs** = tabs_track + tabs_thumb animado por posição + TMP por aba.
- **Banner de hazard** = banner_hazard_[cor] + icon_warning_tri + TMP (título + linha de consequência) + TMP do contador.
- **Card de item** = frames/frame_[raridade] + render do cosmético dentro + badge/TMP de preço.
- **Toast** = toast_green/red + dot_green/red + TMP.

## Pastas
`panels/` containers e banners · `buttons/` CTAs e estados pressed · `controls/` botões de ícone, toggle, tabs, slider, barras, FAB · `economy/` chips, ícones de moeda, pucks, badges · `frames/` raridades · `overlays/` toasts, tooltip, dots, scrim · `icons/` 288×288 (power-ups, hazards, nav) · `backgrounds/` 1170×2532

## Observações
- Glow e sombra interna estão baked. Não adicione outline/shadow no Unity ou dobra.
- Fontes: **Chakra Petch** (números, títulos, CTA) e **Barlow Semi Condensed** (UI, labels). Ambas gratuitas no Google Fonts — gere os atlas TMP.
- Alvo de toque mínimo 44 × 44 pt (132 px @3×). Safe area 44 pt topo / 34 pt base.
- Cores base: `#22E3FF` accent · `#7CFF4F` safe · `#FF4D5E` danger · `#FFC23D` coins · `#7C4DFF` gems · `#FF3EA5` hazard.
