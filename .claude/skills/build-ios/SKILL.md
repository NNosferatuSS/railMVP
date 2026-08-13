---
name: build-ios
description: Builda o RailMVP para iOS e instala no iPhone conectado, para teste rápido, com monitoramento em tempo real e correção automática de falhas de infraestrutura de build conhecidas. Use quando o usuário digitar "/build-ios", ou pedir para "compilar/buildar pro iphone", "testar no celular", "dar deploy no iphone".
---

# /build-ios — build + deploy rápido no iPhone, com monitor ao vivo

O script `scripts/build-and-run-ios.sh` já se auto-corrige para um catálogo
pequeno de falhas conhecidas de infraestrutura (cache do DerivedData
corrompido, export incremental do Unity inconsistente, hiccup de
provisioning — cada uma tentada no máximo uma vez, emitindo uma linha
`[AUTO-FIX] ...` quando age). Erro de compilação/código real **nunca** é
"corrigido" pelo script — isso é sempre decisão sua ou do usuário. Sua parte
como skill é: rodar em background, acompanhar ao vivo, e cobrir o que o
script não sabe resolver sozinho.

## Passo 1 — iniciar o build em background

```bash
./scripts/build-and-run-ios.sh > Logs/build-ios-run.log 2>&1
```

Use o Bash tool com `run_in_background: true`. Guarde o `task_id` retornado.

## Passo 2 — armar o Monitor

Chame a ferramenta `Monitor` (via ToolSearch com `select:Monitor` se ainda não
estiver carregada) apontando pro mesmo arquivo de log:

```bash
tail -n +1 -f Logs/build-ios-run.log | grep -E --line-buffered "^\[HEARTBEAT\]|^\[AUTO-FIX\]|^\[ACTION-NEEDED\]|^\[STEP-OK\]|^\[STEP-FAILED\]|Pronto!|falhou|não encontrado|Feche o Unity Editor"
```

- `description`: "build iOS do RailMVP"
- `persistent: false`, `timeout_ms`: 1800000 (30 min é de sobra até pra um export completo do Unity)

O filtro cobre progresso (`HEARTBEAT`), correções automáticas (`AUTO-FIX`),
sucesso/falha de cada etapa e as mensagens de erro conhecidas do script — não
estreite mais que isso, senão uma falha nova passa em silêncio.

## Passo 3 — reagir aos eventos que chegarem

- `[HEARTBEAT]` — só repasse pro usuário se ele perguntar o status; não precisa narrar cada um.
- `[AUTO-FIX]` — avise o usuário em uma linha do que foi auto-corrigido (é informação relevante, não ruído).
- `[STEP-FAILED]` seguido de "Últimas linhas" — o script já esgotou as correções conhecidas dele. Leia o log completo (`Logs/ios-build.log` ou `Logs/ios-xcodebuild.log`) pra diagnosticar a causa real:
  - Se for um erro de código/compilação (C#) → aponte o arquivo/linha pro usuário, não tente "corrigir" você mesmo sem concordância dele.
  - Se for algo de infraestrutura que o script não cobre mas que você reconhece (ex: Podfile faltando, entitlement errado, certificado expirado) → pode aplicar o fix você mesmo e rodar o script de novo — mas só uma vez automaticamente; se falhar de novo, pare e explique ao usuário em vez de ficar retentando.
  - "Feche o Unity Editor..." / "iPhone '...' não encontrado" / `[ACTION-NEEDED]` (ex: Developer Mode desligado no iPhone) — não são bugs, são ação humana pendente. Avise o usuário com os passos exatos e pare, não fique re-tentando em loop.
- Linha final `Pronto! App instalado e aberto no iPhone.` → sucesso.

## Passo 4 — reportar

Confirme pro usuário: quais (se algum) auto-fixes rodaram, nome/estado do
dispositivo, e que o app foi instalado e aberto no iPhone — ou, em caso de
falha, o diagnóstico real (não só "build falhou").
