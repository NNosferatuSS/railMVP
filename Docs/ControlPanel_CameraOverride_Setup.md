# Control Panel (Odin) + Override de Câmera por Tier

> Sessão 2026-06-02.

## Visão geral

Duas coisas nesta sessão:

1. **RailSwitch Control Panel** — uma `EditorWindow` baseada no **Odin Inspector**
   que centraliza os ScriptableObjects de config num lugar só (ver/identificar/
   alterar fácil sem caçar assets pela pasta).
2. **Override de tilt/FOV por tier** — cada `DifficultyTier` pode sobrescrever o
   ângulo (tilt) e o FOV globais da câmera, pra tunar a lente por dificuldade.

> **Diretriz da sessão:** daqui pra frente, todo parâmetro novo de config deve
> funcionar no Control Panel. Como a janela Odin desenha os SOs automaticamente,
> qualquer campo serializado novo já aparece lá sem esforço extra.

---

## 1. RailSwitch Control Panel

`Assets/Scripts/RailSwitchMVP/Editor/RailSwitchControlPanel.cs` —
`OdinMenuEditorWindow`. Abre em **Tools → RailSwitch → Control Panel**.

- Menu/árvore à esquerda, inspector Odin do item selecionado à direita.
- Agrega: **Difficulty** (`DifficultyConfig`), **Generation** (`RailGenConfig`),
  **Revive** (`ReviveConfig`), **Pools/Hazards/\*** (`HazardPool`),
  **Pools/Power-ups/\*** (`PowerUpPool`). Um item por asset encontrado via
  `AssetDatabase.FindAssets("t:Tipo")`.
- Barra de busca ligada (`tree.Config.DrawSearchToolbar`).

**Independente do Inspector normal:** a janela usa o sistema de desenho do Odin,
então os custom editors/drawers existentes (`ValidatedConfigInspector`,
`DifficultyTierDrawer`) continuam intocados e funcionando no Inspector padrão.

Se um config não aparecer no menu, é porque o asset não existe ainda (ex.: nenhum
`ReviveConfig` criado) — não quebra, só não lista.

### Roadmap do painel (próximas fatias)
- **v2:** tabela curada dos tiers via `[TableList]` + `[HideInTables]` +
  `[TableColumnWidth]` no `DifficultyTier` (editar todos os tiers em grid).
- **v3:** página "Play Tools" com `[Button]` de runtime (forçar tier, dar
  coins/gems/XP, toggles de spawn).

---

## 2. Override de tilt/FOV por tier

### Campos novos em `DifficultyTier` (`Config/DifficultyConfig.cs`)

```csharp
[Header("Camera")]
public float cameraZoom;              // já existia — distância da câmera ao foco

public bool  overrideCameraAngle;     // se true, sobrescreve tilt+FOV globais

[ShowIf(nameof(overrideCameraAngle))]
[Range(0f, 90f)]
public float cameraTilt;              // graus (0 = top-down, 90 = lateral)

[ShowIf(nameof(overrideCameraAngle))]
[Range(20f, 100f)]
public float cameraFieldOfView;       // FOV (60 = default)
```

- **Default off** → o tier usa o tilt/FOV globais do `RailGenConfig` (comportamento
  anterior, **zero regressão**). Os campos `cameraTilt`/`cameraFieldOfView` só passam
  a valer quando `overrideCameraAngle = true`.
- `[ShowIf]` é o **primeiro atributo Odin de runtime** do projeto
  (`using Sirenix.OdinInspector;` em `DifficultyConfig.cs`). Esconde os dois campos
  quando o override está off, no Control Panel.

### `PlayerCameraRig` (`Player/PlayerCameraRig.cs`)

No `LateUpdate`, antes do death-cam/zoom:

```csharp
float baseTilt = tier.overrideCameraAngle ? tier.cameraTilt        : config.cameraTilt;
float fov      = tier.overrideCameraAngle ? tier.cameraFieldOfView : config.cameraFieldOfView;
```

`baseTilt` entra no `effectiveTilt` (somado ao death-cam delta); `fov` é aplicado ao
`Camera.fieldOfView`. Sem override, ambos caem nos globais.

### `DifficultyTierDrawer` (Inspector normal)

- **Header do foldout** agora mostra **zoom** (sempre) e **FOV** (só quando override),
  no lugar do antigo `pop %`:

  ```
  ► Tier 2  —  240 m  |  speed 8  |  2-3/row  |  zoom 12  |  FOV 70
  ```

  FOV não entra no header quando o override está off, porque aí o tier usa o FOV
  global (o campo do tier fica 0/ignorado) — mostrar "FOV 0" seria enganoso.
- `cameraTilt`/`cameraFieldOfView` ficam **escondidos no Inspector quando o override
  está off** (`IsCameraOverrideField` + `GetPropertyHeight` ajustado), espelhando o
  `[ShowIf]` do Odin pra manter os dois ambientes consistentes.

---

## Como usar

No tier (pelo **Control Panel** ou pelo Inspector de `DifficultyConfig`):

1. Liga `Override Camera Angle`.
2. Os campos `Camera Tilt` / `Camera Field Of View` aparecem — ajuste.
3. Joga até esse tier: a câmera muda só nele; os outros seguem o global.

Ex.: tiers altos com FOV mais aberto pra dar sensação de velocidade, ou tilt
diferente pra mudar a leitura da pista.

## Validado
- 2026-06-02: compila com o `[ShowIf]` runtime; override aplica por tier; header
  e visibilidade dos campos corretos no Inspector e no Control Panel.
