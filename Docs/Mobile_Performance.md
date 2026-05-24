# Mobile Performance — diagnóstico + tunables

> Notas após o primeiro build Android (2026-05-24). FPS abaixo do esperado.
> Este doc lista causas prováveis em ordem de impacto + onde mexer.

## Mudanças já aplicadas no código

- **`PerformanceBootstrapper.cs`** — trava `targetFrameRate = 60`, desliga vSync,
  desliga sleep timeout em Android/iOS. Roda antes de qualquer cena
  (`RuntimeInitializeOnLoadMethod`). Sem isso, Android pode oscilar livremente
  entre 30-60 fps causando hitches percebidos.
- **`CollectibleCoin.Collect()`** — cache estático do PlayerRailRider. Antes,
  cada moeda coletada fazia `FindFirstObjectByType<PlayerRailRider>()`, que
  varre a cena inteira. Numa run típica com Magnet ativo isso chamava várias
  vezes por segundo.

Pra mudar fps target em runtime (futuro settings menu):
```cs
PerformanceBootstrapper.SetTarget(30);  // ou 60, 120
```

---

## Diagnóstico — primeiro passo

**Não tente otimizar antes de medir.** O Profiler te diz onde está o gargalo.

1. Build com **Development Build** ✅ + **Autoconnect Profiler** ✅.
2. Em Window → Analysis → **Profiler**, ativar.
3. Rodar no device. Profiler conecta automaticamente.
4. Olhar o gráfico CPU. As 3 categorias relevantes:
   - **Rendering** (cyan) — se for o maior, problema é GPU/shaders/draw calls.
   - **Scripts** (laranja) — se for o maior, problema é código (Update loops).
   - **Garbage Collector** picos verdes → alocação demais por frame.

Se você ainda não viu o profiler, vale 10min pra confirmar onde estamos antes
de mexer em coisa de URP às cegas.

---

## Tunables — em ordem de provável impacto

### 1. Shadows (geralmente o #1 vilão em mobile)

`Assets/Settings/Mobile_RPAsset.asset` está com `m_MainLightShadowmapResolution: 1024`
e `m_ShadowDistance: 50`. QualitySettings mobile com `shadows: 2` (Hard).

**Opções (no Editor, abre o asset Mobile_RPAsset no Inspector):**
- **Desligar shadows totalmente:** Main Light → Cast Shadows = OFF.
  Visual fica menos profundo mas ganha 30-50% de GPU típico.
- **Shadowmap 512:** se quiser manter shadows, baixar de 1024 → 512.
- **Shadow Distance 25:** baixar de 50 → 25 (sombras mais próximas = shadowmap
  com mais detalhe na área visível).
- **Cascades 1:** já está em 2, dá pra cair pra 1 cascade.

Recomendação: comece desligando totalmente e veja o ganho. Se ficou feio,
reabilita com resolução 512 e distance 25.

### 2. HDR

`Mobile_RPAsset.asset` tem `m_SupportsHDR: 1`. Em mobile sem post-FX de bloom/
tone mapping, HDR só adiciona custo de bandwidth e uma render pass extra.

**Onde mexer:** Mobile_RPAsset → Quality → **HDR = OFF**.

Ganho típico: 10-20% em GPU-bound. Mais em devices mid/low.

### 3. Render Scale

Mobile_RPAsset já está com 0.8 (renderiza a 80% e upscale). Bom baseline.

**Se ainda tá pesado:** baixar pra 0.7 ou 0.65. Visual fica mais soft mas
em jogo de movimento rápido (runner) o user mal nota.

**Se está fluido visualmente mas você quer mais nitidez:** subir pra 0.9.
Trade-off ~15% perf.

### 4. Additional Lights

`Mobile_RPAsset.asset` tem `m_AdditionalLightsRenderingMode: 1` (Per Pixel).

Se a cena não tem nenhuma luz adicional (só Main Directional), trocar pra
**Per Vertex** (= 2) ou **Disabled** (= 0). Economia varia conforme a cena.

### 5. MSAA

Já está desligado (`m_MSAA: 1` no URP, `antiAliasing: 0` em QualitySettings).
OK. Não mexer.

### 6. Quality preset por plataforma

`ProjectSettings/QualitySettings.asset` tem Android default = 0 = Mobile. ✅
Confirmado correto. Se o app tá rodando em qualidade PC por engano (Editor
Player Settings podem sobrescrever), é o primeiro lugar pra checar.

---

## Tunables menos óbvios

### 7. Pooling de coins / power-ups

Tiles já são pooled. Coins e power-ups são `Instantiate`/`Destroy` por tile.
Numa run longa, isso gera GC contínuo. Pooling de coins é uma melhoria média
(~50 alocs/seg evitadas) que vale fazer SE o profiler mostrar GC picos.

Não fazer ainda sem confirmar via profiler. Fix only what's broken.

### 8. OnGUI overhead em Debug Build

`DebugPanelController` e `SpawnOverrideController` têm `OnGUI()` chamado todo
frame pelo Unity (mesmo quando `_show == false`). Em release builds, ambos
auto-desabilitam (early return). Em **Development Build**, ambos rodam.

Impacto: pequeno (~0.2ms/frame), mas mensurável. Se quiser eliminar quando
não tá mostrando, dá pra setar `enabled = false` enquanto fechado e
re-habilitar via `Toggle()` (mas precisa de input alternativo pra reabilitar).

Adiar — não vale o esforço enquanto outros itens dominam.

### 9. Player.transform.position em hot path

PowerUpManager faz OverlapSphere por frame em Magnet ativo. Não é problema
em desktop. Em mobile com cache miss em Bullet/PhysX, pode pesar. Profiler
mostra se for relevante.

### 10. Shader bloat (URP defaults)

URP usa `Lit` shader complexo por default. Pra primitives coloridos do MVP,
`Unlit` ou `Simple Lit` serve. Trocar materials dos primitivos → ganho
modesto mas real.

Não fazer até material/visual definitivo entrar no jogo.

---

## Checklist rápido (5min)

Antes do próximo build, mexer só nestes 3 e medir:

- [ ] `Mobile_RPAsset` → desligar shadows (Main Light Cast Shadows = OFF).
- [ ] `Mobile_RPAsset` → HDR = OFF.
- [ ] Confirmar que `PerformanceBootstrapper` rodou: log "Application.targetFrameRate"
  aparece como 60. Se Editor permitir, breakpoint no Init() pra garantir.

Se ainda tá ruim após isso, **profiler obrigatório** antes de continuar.

---

## Não-otimizações (não vale o esforço agora)

- Mudar `Update()` pra `FixedUpdate()` — irrelevante, FixedUpdate roda 50hz fixo.
- Pooling de coins — só se profiler apontar GC.
- Burst/Jobs/ECS — overkill pra runner simples.
- Compressão de texturas — só se loading time/RAM forem problema.
- Static batching — Unity já faz com objetos `Static`. Tiles são dinâmicos por design.
