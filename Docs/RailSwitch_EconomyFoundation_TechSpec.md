# Rail Switch — Fase A: Fundação da Economia
## Spec técnico para implementação (Claude Code)

> **Escopo:** Fase A do documento de expansão de meta-systems — sistema de 3 currencies,
> catálogo de cosméticos com raridade, tela de Coleção, e loja expandida.
> Battle pass (Fase B) e eventos (Fase C) ficam para depois, mas a arquitetura aqui já
> os antecipa.
>
> **Pré-requisito:** PlayerDataManager existente (coins, best distance, equipped char),
> sistema de persistência local (PlayerPrefs) funcionando.

---

## 1. Resultado Esperado da Fase A

Ao final desta fase:

1. Jogador tem 3 currencies persistidas: Coins, Gems, Event Tokens.
2. Existe um catálogo de cosméticos definido via ScriptableObjects (personagens, trails,
   tile skins, etc.), cada um com raridade e preço.
3. Jogador pode comprar cosméticos com a currency correta, e a compra é atômica e segura.
4. Jogador pode equipar cosméticos por categoria (1 ativo por categoria).
5. Existe uma tela de Coleção mostrando todos os cosméticos (obtidos vs não-obtidos).
6. Existe uma loja com abas (Featured, Personagens, Cosméticos, Currency).
7. Cosméticos equipados se refletem visualmente no gameplay (cor do player, trail, etc.).

---

## 2. Currencies

### 2.1 Modelo de Dados

Estender o `PlayerDataManager` (ou criar `CurrencyManager` separado — recomendado separar
por organização). Recomendação: criar `CurrencyManager` singleton que o PlayerDataManager
referencia.

```
Namespace: RailSwitchMVP.Economy
Path:      Assets/Scripts/RailSwitchMVP/Economy/CurrencyManager.cs
Tipo:      MonoBehaviour singleton, DontDestroyOnLoad
```

### 2.2 Enum de Currency

```csharp
namespace RailSwitchMVP.Economy
{
    public enum CurrencyType
    {
        Coins = 0,
        Gems = 1,
        EventTokens = 2
    }
}
```

### 2.3 Chaves de PlayerPrefs

```
"currency_coins"         → int (default 0)
"currency_gems"          → int (default 0)
"currency_event_tokens"  → int (default 0)
```

### 2.4 API do CurrencyManager

```csharp
public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    public event System.Action<CurrencyType, int> OnCurrencyChanged; // (tipo, novo total)

    public int GetBalance(CurrencyType type);
    public void Add(CurrencyType type, int amount, string source);
    public bool TrySpend(CurrencyType type, int amount, string reason); // false se saldo insuficiente
    public bool CanAfford(CurrencyType type, int amount);

    public void Save();
    void Load();
}
```

### 2.5 Regras de Implementação

- `Add` e `TrySpend` sempre disparam `OnCurrencyChanged` e chamam `Save()`.
- `TrySpend` retorna `false` SEM modificar saldo se não houver fundos. Nunca deixa negativo.
- Todo `Add`/`TrySpend` registra uma transação (ver seção 7).
- Singleton com proteção de duplicata e auto-criação (mesmo padrão do PlayerDataManager:
  se `Instance` for null ao ser acessado, criar GameObject automaticamente).
- `OnApplicationPause(true)` e `OnApplicationFocus(false)` chamam `Save()`.

### 2.6 Migração de Coins Existentes

O PlayerDataManager já persiste coins na chave `"player_coins"`. Na primeira execução do
CurrencyManager, migrar:

```
Se "currency_coins" não existe E "player_coins" existe:
    currency_coins = player_coins
    (manter player_coins por compatibilidade, mas CurrencyManager passa a ser a fonte da verdade)
```

Depois da migração, o PlayerDataManager deve LER coins do CurrencyManager, não mais da
própria chave. Ajustar PlayerDataManager.Coins para delegar:

```csharp
public int Coins => CurrencyManager.Instance.GetBalance(CurrencyType.Coins);
```

---

## 3. Catálogo de Cosméticos

### 3.1 Enum de Categoria

```csharp
namespace RailSwitchMVP.Economy
{
    public enum CosmeticCategory
    {
        Character = 0,
        Trail = 1,
        TileSkin = 2,
        CoinSkin = 3,
        SwitchSkin = 4,
        Background = 5,
        GameOverFX = 6,
        CollectFX = 7
    }

    public enum CosmeticRarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3
    }
}
```

### 3.2 CosmeticItem (ScriptableObject)

```
Namespace: RailSwitchMVP.Economy
Path:      Assets/Scripts/RailSwitchMVP/Economy/CosmeticItem.cs
```

```csharp
[CreateAssetMenu(fileName = "Cosmetic_", menuName = "RailSwitchMVP/Cosmetic Item")]
public class CosmeticItem : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("ID único e estável. NUNCA mudar depois de lançado (quebra saves).")]
    public string itemId;            // ex: "char_neon", "trail_synthwave"
    public string displayName;
    [TextArea] public string description;

    [Header("Classification")]
    public CosmeticCategory category;
    public CosmeticRarity rarity;

    [Header("Pricing")]
    public CurrencyType priceCurrency; // Coins, Gems ou EventTokens
    public int price;
    [Tooltip("Se true, não é comprável diretamente — vem de battle pass/evento/conquista")]
    public bool isUnlockableOnly;

    [Header("Default Ownership")]
    [Tooltip("Se true, o jogador já possui este item de início (ex: personagem default)")]
    public bool ownedByDefault;

    [Header("Visual Payload")]
    [Tooltip("Cor primária aplicada ao elemento (player, trail, etc.)")]
    public Color primaryColor = Color.white;
    [Tooltip("Cor secundária/emissiva opcional")]
    public Color secondaryColor = Color.white;
    [Tooltip("Prefab opcional para cosméticos que precisam de mais que cor (partículas, mesh)")]
    public GameObject visualPrefab;
    [Tooltip("Ícone para exibição na loja e coleção")]
    public Sprite icon;
}
```

### 3.3 CosmeticCatalog (ScriptableObject)

Registro central de todos os cosméticos do jogo.

```
Path: Assets/Scripts/RailSwitchMVP/Economy/CosmeticCatalog.cs
```

```csharp
[CreateAssetMenu(fileName = "CosmeticCatalog", menuName = "RailSwitchMVP/Cosmetic Catalog")]
public class CosmeticCatalog : ScriptableObject
{
    public List<CosmeticItem> allItems = new List<CosmeticItem>();

    public CosmeticItem GetById(string itemId)
        => allItems.Find(i => i.itemId == itemId);

    public List<CosmeticItem> GetByCategory(CosmeticCategory cat)
        => allItems.FindAll(i => i.category == cat);

    public List<CosmeticItem> GetDefaultOwned()
        => allItems.FindAll(i => i.ownedByDefault);
}
```

### 3.4 Conteúdo Inicial Mínimo (para popular o catálogo)

Criar como assets no editor. Quantidade mínima para a Fase A:

| Categoria | Quantidade inicial | Distribuição de raridade |
|---|---|---|
| Character | 8 | 3 common, 3 rare, 2 epic |
| Trail | 6 | 3 common, 2 rare, 1 epic |
| TileSkin | 4 | 2 common, 2 rare |
| CoinSkin | 4 | 3 common, 1 rare |
| SwitchSkin | 3 | 2 common, 1 rare |
| Background | 3 | 1 common, 2 epic |

Sempre ao menos 1 item `ownedByDefault = true` por categoria (o visual padrão).

Preços de referência (seguir tabela de raridade do documento de design):
- Common: 1.000-3.000 coins
- Rare: 5.000-12.000 coins
- Epic: 800-2.000 gems (ou 25.000-40.000 coins se quiser caminho free)

---

## 4. Ownership e Equip

### 4.1 InventoryManager

```
Namespace: RailSwitchMVP.Economy
Path:      Assets/Scripts/RailSwitchMVP/Economy/InventoryManager.cs
Tipo:      MonoBehaviour singleton, DontDestroyOnLoad
```

Responsabilidades:
- Rastrear quais cosméticos o jogador possui.
- Rastrear qual cosmético está equipado por categoria (1 por categoria).
- Persistir tudo no PlayerPrefs.

### 4.2 Persistência

Owned items: lista de itemIds, serializada como CSV ou JSON.
```
"inventory_owned"  → string (CSV de itemIds: "char_default,char_neon,trail_basic")
```

Equipped por categoria: um itemId por categoria.
```
"equipped_character"  → string (itemId)
"equipped_trail"      → string (itemId)
"equipped_tileskin"   → string (itemId)
"equipped_coinskin"   → string (itemId)
"equipped_switchskin" → string (itemId)
"equipped_background" → string (itemId)
"equipped_gameoverfx" → string (itemId)
"equipped_collectfx"  → string (itemId)
```

### 4.3 API do InventoryManager

```csharp
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }
    [SerializeField] private CosmeticCatalog catalog;

    public event System.Action<CosmeticCategory, string> OnEquipChanged; // (categoria, itemId)
    public event System.Action<string> OnItemUnlocked;                   // (itemId)

    public bool IsOwned(string itemId);
    public void Unlock(string itemId, string source); // adiciona ao inventário
    public string GetEquipped(CosmeticCategory category); // itemId equipado
    public bool TryEquip(string itemId); // só equipa se for owned; false caso contrário
    public List<CosmeticItem> GetOwnedByCategory(CosmeticCategory cat);

    public void Save();
    void Load();
}
```

### 4.4 Inicialização (primeira execução)

No `Load()`, se o jogador não tem nada salvo:
- Conceder todos os itens com `ownedByDefault = true`.
- Equipar o item default de cada categoria.

```csharp
void EnsureDefaults()
{
    foreach (var item in catalog.GetDefaultOwned())
    {
        if (!IsOwned(item.itemId))
            Unlock(item.itemId, "default");

        // equipa se nenhum equipado naquela categoria
        if (string.IsNullOrEmpty(GetEquipped(item.category)))
            TryEquip(item.itemId);
    }
}
```

### 4.5 Migração do Sistema Antigo de Personagens

O sistema atual usa `equipped_char` como int (0,1,2) e `owned_chars` como CSV de índices.
Migrar para o novo formato baseado em itemId:

```
Mapa de migração (int → itemId):
0 → "char_default"
1 → "char_neon"
2 → "char_ember"

Na primeira execução do InventoryManager:
  Se existe "owned_chars" (formato antigo) E não existe "inventory_owned":
    Para cada índice em owned_chars:
      Unlock(mapa[índice])
    equipped_character = mapa[valor de equipped_char antigo]
```

Garantir que os 3 personagens originais existam como CosmeticItem assets com esses itemIds.

---

## 5. Compra de Cosméticos

### 5.1 ShopService

```
Namespace: RailSwitchMVP.Economy
Path:      Assets/Scripts/RailSwitchMVP/Economy/ShopService.cs
Tipo:      MonoBehaviour singleton
```

### 5.2 Fluxo de Compra (atômico)

```csharp
public enum PurchaseResult { Success, AlreadyOwned, InsufficientFunds, NotPurchasable, InvalidItem }

public PurchaseResult Purchase(string itemId)
{
    var item = catalog.GetById(itemId);
    if (item == null) return PurchaseResult.InvalidItem;
    if (item.isUnlockableOnly) return PurchaseResult.NotPurchasable;
    if (InventoryManager.Instance.IsOwned(itemId)) return PurchaseResult.AlreadyOwned;

    if (!CurrencyManager.Instance.CanAfford(item.priceCurrency, item.price))
        return PurchaseResult.InsufficientFunds;

    // Transação atômica: debita E concede juntos
    bool spent = CurrencyManager.Instance.TrySpend(item.priceCurrency, item.price, "purchase:" + itemId);
    if (!spent) return PurchaseResult.InsufficientFunds;

    InventoryManager.Instance.Unlock(itemId, "purchase");
    return PurchaseResult.Success;
}
```

### 5.3 Regra Crítica de Atomicidade

O `TrySpend` debita e retorna sucesso ANTES do `Unlock`. Se por qualquer razão o Unlock
falhar, a moeda já foi debitada — isso é um bug. Garantir que `Unlock` nunca falha após um
`TrySpend` bem-sucedido (Unlock é uma operação local simples que não pode falhar se o itemId
é válido, o que já foi checado). A ordem é: validar tudo → debitar → conceder.

---

## 6. Aplicação Visual dos Cosméticos no Gameplay

### 6.1 CosmeticApplier

```
Namespace: RailSwitchMVP.Economy
Path:      Assets/Scripts/RailSwitchMVP/Economy/CosmeticApplier.cs
Tipo:      MonoBehaviour, na GameScene
```

Lê os cosméticos equipados do InventoryManager e aplica ao gameplay no início do run.

```csharp
void ApplyAllEquipped()
{
    ApplyCharacter(InventoryManager.Instance.GetEquipped(CosmeticCategory.Character));
    ApplyTrail(InventoryManager.Instance.GetEquipped(CosmeticCategory.Trail));
    ApplyTileSkin(InventoryManager.Instance.GetEquipped(CosmeticCategory.TileSkin));
    ApplyCoinSkin(InventoryManager.Instance.GetEquipped(CosmeticCategory.CoinSkin));
    // etc. para cada categoria
}
```

### 6.2 Padrão de Aplicação por Tipo

- **Character / Coin / Switch (cor):** usar MaterialPropertyBlock para trocar a cor
  emissiva sem instanciar material novo. Ler `primaryColor`/`secondaryColor` do CosmeticItem.
- **Trail / CollectFX (partículas):** instanciar o `visualPrefab` ou ajustar cor do
  ParticleSystem existente.
- **TileSkin / Background:** trocar o material/prefab aplicado aos tiles gerados. O
  ProceduralRailGenerator deve consultar o CosmeticApplier ao instanciar tiles.

### 6.3 Integração com o ProceduralRailGenerator

O gerador instancia tiles continuamente durante o run. Ele precisa aplicar o tile skin
equipado a cada tile novo:

```csharp
// No ProceduralRailGenerator, ao instanciar um tile:
var tile = Instantiate(tilePrefab, position, Quaternion.identity);
CosmeticApplier.Instance.ApplyTileSkinToTile(tile); // aplica material do skin equipado
```

Manter referência ao skin equipado em cache no início do run (não consultar
InventoryManager a cada tile — ler uma vez no StartRun e guardar).

---

## 7. Log de Transações

### 7.1 Propósito

Registro de toda mudança de currency, para suporte e auditoria futura. Local por enquanto
(PlayerPrefs limitado), migra para backend depois.

### 7.2 Implementação Simples

Como PlayerPrefs não é ideal para listas grandes, manter um buffer circular dos últimos
N (ex: 50) eventos, serializado em JSON.

```
"transaction_log" → string (JSON array dos últimos 50 eventos)
```

```csharp
[System.Serializable]
public struct TransactionEntry
{
    public string currency;   // "Coins", "Gems", "EventTokens"
    public int amount;        // positivo ou negativo
    public string reason;     // "run_reward", "purchase:char_neon", etc.
    public string timestamp;  // ISO
}
```

Cada `Add`/`TrySpend` do CurrencyManager adiciona uma entrada. Quando passa de 50, remove a
mais antiga.

---

## 8. Tela de Coleção

### 8.1 CollectionScreenController

```
Namespace: RailSwitchMVP.UI
Path:      Assets/Scripts/RailSwitchMVP/UI/CollectionScreenController.cs
```

### 8.2 Comportamento

- Abas/filtros por categoria (Personagens, Trails, Tile Skins, etc.).
- Cada cosmético exibido como card: ícone, nome, raridade (cor da borda).
- Itens não-obtidos: mostrados em silhueta/escurecidos, com o preço ou a forma de obtenção.
- Itens obtidos: ícone colorido normal, com botão "Equipar" (ou "Equipado" se ativo).
- Clicar em item obtido equipa via `InventoryManager.TryEquip`.
- Indicador de progresso por categoria: "Personagens 4/8".

### 8.3 Recompensa de Completar Categoria (opcional nesta fase)

Se o jogador possui todos os itens de uma categoria, conceder uma recompensa de prestígio
(badge no perfil). Pode ficar para depois, mas a checagem `IsCategoryComplete(cat)` deve
existir desde já.

---

## 9. Loja Expandida

### 9.1 ShopScreenController

```
Namespace: RailSwitchMVP.UI
Path:      Assets/Scripts/RailSwitchMVP/UI/ShopScreenController.cs
```

### 9.2 Abas

| Aba | Conteúdo |
|---|---|
| Featured | Daily Deal (1 item 50% off, rotação diária por data) + bundles em destaque |
| Personagens | Catálogo de personagens, agrupado por raridade |
| Cosméticos | Trails, tile skins, coin skins, etc. com filtro por categoria |
| Currency | Pacotes de gems (IAP) e conversões — stub nesta fase, integra IAP na Fase B |

### 9.3 Daily Deal

```
dealSeed = DateTime.UtcNow.DayOfYear
dealIndex = dealSeed % totalPurchasableItems
→ item do dia = lista ordenada de itens compráveis [dealIndex]
→ preço com 50% de desconto
→ salvar "daily_deal_date" para detectar troca de dia
```

### 9.4 Card de Compra

Cada item na loja:
- Ícone, nome, raridade.
- Preço + ícone da currency.
- Botão "Comprar" → chama `ShopService.Purchase(itemId)`.
- Tratar cada `PurchaseResult`:
  - `Success`: feedback positivo, atualiza UI, item vai para "Equipar".
  - `InsufficientFunds`: feedback "saldo insuficiente" + (opcional) levar à aba Currency.
  - `AlreadyOwned`: botão mostra "Possuído / Equipar".
  - `NotPurchasable`: não exibir botão comprar (item de unlock).

---

## 10. Arquitetura de Scripts (resumo)

```
Assets/Scripts/RailSwitchMVP/
├── Economy/
│   ├── CurrencyType.cs            [enum]
│   ├── CurrencyManager.cs         [singleton, 3 currencies]
│   ├── CosmeticCategory.cs        [enums: categoria + raridade]
│   ├── CosmeticItem.cs            [ScriptableObject]
│   ├── CosmeticCatalog.cs         [ScriptableObject, registro central]
│   ├── InventoryManager.cs        [singleton, ownership + equip]
│   ├── ShopService.cs             [singleton, lógica de compra]
│   └── CosmeticApplier.cs         [aplica visual no gameplay]
└── UI/
    ├── CollectionScreenController.cs
    └── ShopScreenController.cs
```

---

## 11. Ordem de Implementação

1. **Enums** (CurrencyType, CosmeticCategory, CosmeticRarity) — sem dependências.
2. **CurrencyManager** — com migração de coins do PlayerDataManager. Testar Add/Spend/Save.
3. **CosmeticItem + CosmeticCatalog** (ScriptableObjects) — criar os assets de conteúdo
   inicial (seção 3.4).
4. **InventoryManager** — com defaults e migração do sistema antigo de personagens.
   Testar ownership e equip persistindo.
5. **ShopService** — compra atômica. Testar todos os PurchaseResult.
6. **CosmeticApplier** — aplicar cor do personagem equipado primeiro (mais simples),
   depois trail, depois tile skin. Integrar com ProceduralRailGenerator.
7. **CollectionScreenController** — UI de coleção, equipar funcionando.
8. **ShopScreenController** — abas, daily deal, compra pela UI.
9. **Log de transações** — adicionar ao CurrencyManager por último.

Testar cada passo antes do próximo. Os passos 2-6 são backend/lógica (testáveis sem UI
bonita); 7-8 são UI.

---

## 12. Checklist de Entrega

### Currencies
- [ ] CurrencyManager com 3 currencies persistindo
- [ ] Migração de coins do sistema antigo sem perda
- [ ] Add/TrySpend atômicos, nunca deixam saldo negativo
- [ ] OnCurrencyChanged disparado corretamente
- [ ] Save em OnApplicationPause/Focus

### Cosméticos
- [ ] CosmeticItem e CosmeticCatalog funcionando
- [ ] Conteúdo inicial criado (≥28 itens conforme seção 3.4)
- [ ] Pelo menos 1 ownedByDefault por categoria

### Inventário
- [ ] Ownership persistindo
- [ ] Equip por categoria (1 ativo por categoria)
- [ ] Defaults concedidos na primeira execução
- [ ] Migração do sistema antigo de personagens (int → itemId)

### Compra
- [ ] ShopService.Purchase atômico
- [ ] Todos os PurchaseResult tratados
- [ ] Moeda debitada exatamente uma vez por compra

### Visual
- [ ] Personagem equipado muda cor no gameplay
- [ ] Trail equipado aplicado
- [ ] Tile skin equipado aplicado a tiles gerados
- [ ] CosmeticApplier lê equipado uma vez no StartRun (cache)

### UI
- [ ] Tela de Coleção com filtro por categoria, obtidos vs silhueta
- [ ] Equipar pela coleção funciona
- [ ] Loja com 4 abas
- [ ] Daily deal rotaciona por dia
- [ ] Feedback de compra (sucesso / saldo insuficiente / possuído)

---

## 13. Cuidados Críticos

1. **itemId é imutável.** Uma vez lançado, mudar um itemId quebra os saves de quem possui
   aquele item. Tratar itemId como chave de banco de dados.

2. **Compra é atômica: validar → debitar → conceder.** Nunca conceder antes de debitar,
   nunca debitar sem garantir que o conceder vai funcionar.

3. **CosmeticApplier lê equipado uma vez por run, não por frame/tile.** Cachear no StartRun.
   Consultar InventoryManager para cada tile gerado seria custo desnecessário.

4. **Migração roda uma vez.** Proteger com flag para não re-migrar e duplicar/sobrescrever.

5. **Singletons com auto-criação e proteção de duplicata**, mesmo padrão do PlayerDataManager.

6. **Antecipação de battle pass/evento:** o campo `isUnlockableOnly` e a currency
   `EventTokens` já existem para que a Fase B e C encaixem sem refatorar este sistema.

7. **Pedir checkout no Perforce** antes de editar PlayerDataManager e ProceduralRailGenerator
   (arquivos existentes). Os demais são novos. Sem debug logs nem comentários nos scripts
   finais; NRE guards e checagem de objeto ativo em coroutines, conforme padrão do projeto.

---

*Spec da Fase A. Implementar na ordem da seção 11, testando cada passo.*
*Fases B (battle pass) e C (eventos) virão em documentos separados, encaixando nesta base.*
