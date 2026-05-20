# D — High Score Persistente

Saves the 4 best stats across runs via PlayerPrefs:
- Best distance (m)
- Best coins (count)
- Best tier (index 0..5)
- Best time (seconds)

Mostrado no Game Over screen junto com stats da run atual. Quando algum
record é batido, mostra "★" do lado da stat e overlay "NEW RECORD!".

---

## Setup na Editor

### 1. Adicionar `_HighScoreManager` na cena

1. Hierarchy → Create Empty → renomear pra `_HighScoreManager`.
2. Add Component → **High Score Manager**.
3. Zero campos pra preencher — carrega de PlayerPrefs no Awake.

### 2. (Opcional) Adicionar `NewRecordText` no GameOverPanel

Pra ter o overlay "★ NEW RECORD! Distance Tier..." quando bater records:

1. No `_HUD_Canvas → GameOverPanel → Center`, criar novo **UI → Text - TextMeshPro**.
2. Renomeie pra `NewRecordText`.
3. Configurações:
   - Font Size: 32
   - Color: amarelo brilhante ou dourado (`#FFD700`)
   - Font Style: Bold
   - Alignment: Center
4. **Desative o GameObject** (checkbox topo do Inspector ao lado do nome).
   O controller reativa em runtime quando record é batido.
5. Em `_GameOver → Game Over Controller → New Record Text` → arraste `NewRecordText`.

Se você NÃO criar o NewRecordText, o sistema funciona igual — só não tem o
overlay "NEW RECORD" (records ainda são salvos e exibidos inline com ★).

---

## Como funciona

### Save flow
1. Player morre → `GameOverController.HandleGameOver` chamado.
2. Compute stats da run atual (distance, coins, tier, time).
3. `HighScoreManager.TryUpdate(...)` compara com bests salvos:
   - Pra cada stat que é maior, atualiza in-memory + escreve em PlayerPrefs.
   - Retorna `RecordResult` com flags pra quais records foram batidos.
4. `PlayerPrefs.Save()` força commit ao disco.
5. GameOverController exibe labels com formato `Distance: 487 m ★  (Best: 612 m)`.
6. Se `RecordResult.Any` → ativa NewRecordText com lista dos records batidos.

### Load flow
1. Cena abre.
2. `HighScoreManager.Awake` → `LoadFromPrefs()`.
3. Reads 4 ints/floats das chaves `RailMVP.BestDistance/Coins/Tier/Time`.
4. Log no Console: `[HighScore] Loaded best: dist=X coins=Y tier=Z time=Ts`.

### Reset (debug)
- F1 → Debug Panel → seção "High score" → botão **Reset Best Scores**.
- Apaga todas 4 chaves de PlayerPrefs.
- Próximo Game Over re-bateria records facilmente (já que tudo está em 0).

---

## Pra testar

### Cenário 1 — primeiro Game Over
- Play. Anda alguma distância, coleta coins, talvez sobe tier.
- Morre.
- Game Over screen: **TUDO tem ★** porque todos os bests estavam em 0.
- "NEW RECORD! Distance Coins Tier Time" overlay aparece.

### Cenário 2 — segundo Game Over (pior performance)
- Restart. Anda pouco, morre rápido.
- Game Over: SEM ★ (nada batido). Labels mostram current + best.

### Cenário 3 — bater só 1 record
- Restart. Vai mais longe que antes mas coleta menos coins.
- Game Over: ★ só no Distance. NewRecordText mostra "★ NEW RECORD! Distance".

### Cenário 4 — fechar e reabrir o jogo
- Fecha Unity / standalone build.
- Reabre.
- Bests persistem (gravados em PlayerPrefs).

### Cenário 5 — reset via debug
- F1 → Reset Best Scores.
- Bests voltam a 0.
- Próximo Game Over re-bate tudo de novo.

---

## Onde PlayerPrefs salva (no Windows)

Pra debug: `HKCU\Software\Unity\UnityEditor\<CompanyName>\<ProductName>`.

Apagar manualmente:
- Standalone build: aparece em `%APPDATA%\..\LocalLow\<CompanyName>\<ProductName>\...`
- Editor: regedit, navegue pra chave acima.

Ou simplesmente use o botão Reset do Debug Panel.
