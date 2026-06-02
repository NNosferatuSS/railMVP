# Editor: botão "Open" ao lado de todo campo de ScriptableObject

> Sessão 2026-06-02. Poupar cliques ao editar configs que apontam pra SOs.

## O que faz

`Assets/Scripts/RailSwitchMVP/Editor/ScriptableObjectFieldDrawer.cs` é um
`PropertyDrawer` **global** registrado com:

```csharp
[CustomPropertyDrawer(typeof(ScriptableObject), true)]
```

O `useForChildren: true` faz valer pra **qualquer subclasse de ScriptableObject**. Em todo
campo que referencia um SO, o Inspector passa a desenhar o object field normal **+ um botão
"Open"** ao lado. Clicar abre as **properties do asset numa janela** (via
`OdinEditorWindow.InspectObject`), sem precisar achar o asset no Project e selecionar.

O botão só aparece **quando há um asset atribuído** (campo vazio = sem botão).

## Onde aparece

- Inspector normal de qualquer componente/SO com campo de SO (ex.: `ProceduralRailGenerator.config`,
  refs de `RailGenConfig`/`DifficultyConfig`/`ReviveConfig`, `CosmeticCatalog`, etc.).
- **Dentro do `DifficultyTierDrawer`**: ao expandir um tier, os campos `hazardPool`,
  `powerUpPool` e `mysteryBoxPool` ganham o botão (o drawer desenha os filhos via
  `EditorGUI.PropertyField`, então o drawer global pega).

## Caveat — Control Panel (Odin)

No **Control Panel** (janela Odin), os object fields são desenhados pelo **próprio Odin**, que
ignora `CustomPropertyDrawer` de object reference — então o botão "Open" **pode não aparecer
lá**. Mas no Control Panel os pools/configs já são **itens diretos da árvore** à esquerda
(1 clique pra abrir), então a dor já está resolvida por lá.

Se quiser o mesmo botão **dentro dos campos no Control Panel**, dá pra adicionar um
`OdinAttributeProcessor` global que injeta `[InlineButton]` (ou `[InlineEditor]` pra editar
inline) em todo membro do tipo ScriptableObject. Não foi feito ainda porque precisa de
validação no Unity (comportamento da expressão Odin). Pedir se quiser.

## Escopo

É **project-wide**: vale pra TODO campo de SO em qualquer Inspector do projeto (inclusive de
outros plugins). É o que foi pedido ("em todos os lugares"). Se algum campo específico não
quiser o botão, dá pra excluir por tipo depois.
