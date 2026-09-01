#!/usr/bin/env bash
# Build + deploy rápido do RailMVP no iPhone conectado via cabo.
# Uso: ./scripts/build-and-run-ios.sh
#
# Emite linhas [HEARTBEAT]/[AUTO-FIX]/[STEP-OK]/[STEP-FAILED] em stdout pra dar
# pra acompanhar em tempo real (ex: `tail -f` ou a ferramenta Monitor do Claude).
#
# Auto-heal cobre só falhas de infraestrutura de build conhecidas (cache do
# DerivedData corrompido, export incremental do Unity inconsistente, hiccup de
# provisioning). Erro de compilação/código nunca é "corrigido" automaticamente
# — isso é decisão do dev, não do script.
set -uo pipefail

PROJECT_PATH="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_PATH="/Applications/Unity/Hub/Editor/6000.4.7f1/Unity.app/Contents/MacOS/Unity"
XCODE_EXPORT_DIR="$PROJECT_PATH/Builds/01"
XCODE_PROJECT="$XCODE_EXPORT_DIR/Unity-iPhone.xcodeproj"
DERIVED_DATA="$PROJECT_PATH/Builds/DerivedData"
UNITY_LOG="$PROJECT_PATH/Logs/ios-build.log"
XCODEBUILD_LOG="$PROJECT_PATH/Logs/ios-xcodebuild.log"
DEVICE_NAME="Nosfera"
DEVELOPMENT_TEAM="WEMK49LEWS"
BUNDLE_ID="com.nosfera3d.railmvp"
HEARTBEAT_INTERVAL=10

mkdir -p "$PROJECT_PATH/Logs"

# Roda "$@" em background, gravando tudo em $logfile, e emite um heartbeat
# periódico (última linha do log + tempo decorrido) enquanto o processo vive.
run_with_heartbeat() {
  local name="$1" logfile="$2"
  shift 2
  : > "$logfile"
  ("$@" > "$logfile" 2>&1) &
  local pid=$!
  local start=$SECONDS
  while kill -0 "$pid" 2>/dev/null; do
    sleep "$HEARTBEAT_INTERVAL"
    if kill -0 "$pid" 2>/dev/null; then
      local elapsed=$(( SECONDS - start ))
      local last
      last=$(tail -n 1 "$logfile" 2>/dev/null)
      echo "[HEARTBEAT] step=$name elapsed=${elapsed}s last: ${last}"
    fi
  done
  wait "$pid"
}

run_unity_export() {
  echo "==> [1/4] Exportando projeto Xcode via Unity (batchmode)..."
  if ps aux | grep "Unity.app/Contents/MacOS/Unity" | grep -- "-projectpath $PROJECT_PATH" | grep -iv -- "-batchmode" | grep -v grep | grep -q .; then
    echo "Feche o Unity Editor (a janela do app) neste projeto antes de rodar o build automatizado." >&2
    return 1
  fi

  run_with_heartbeat "unity-export" "$UNITY_LOG" \
    "$UNITY_PATH" -batchmode -nographics -quit \
    -projectPath "$PROJECT_PATH" \
    -buildTarget iOS \
    -executeMethod RailSwitchMVP.EditorTools.BuildIOS.PerformBuild \
    -developmentBuild

  local status=$?
  if [ $status -ne 0 ]; then
    echo "[STEP-FAILED] unity-export"
    echo "Build do Unity falhou (erro de compilação/código — não é algo pra corrigir automaticamente). Últimas linhas:" >&2
    tail -n 60 "$UNITY_LOG" >&2
    return 1
  fi
  echo "[STEP-OK] unity-export"
  return 0
}

attempt_xcodebuild() {
  run_with_heartbeat "xcodebuild" "$XCODEBUILD_LOG" \
    xcodebuild \
    -project "$XCODE_PROJECT" \
    -scheme "Unity-iPhone" \
    -configuration Debug \
    -destination "platform=iOS,name=$DEVICE_FULL_NAME" \
    -derivedDataPath "$DERIVED_DATA" \
    -allowProvisioningUpdates \
    -allowProvisioningDeviceRegistration \
    DEVELOPMENT_TEAM="$DEVELOPMENT_TEAM" \
    CODE_SIGN_STYLE=Automatic \
    build
}

run_xcodebuild_with_heal() {
  echo "==> [3/4] Compilando e assinando com xcodebuild..."
  local derived_data_wiped=0 full_reexport_done=0 signing_retry=0 reconnect_attempts=0
  local max_reconnect_attempts=4 reconnect_wait=30

  while true; do
    if attempt_xcodebuild; then
      echo "[STEP-OK] xcodebuild"
      return 0
    fi
    echo "[STEP-FAILED] xcodebuild — checando se é um erro conhecido de infraestrutura..."

    if [ "$derived_data_wiped" -eq 0 ] && grep -qE "Multiple commands produce|couldn.?t be opened|Build input files cannot be found" "$XCODEBUILD_LOG"; then
      echo "[AUTO-FIX] cache do DerivedData parece corrompido — limpando e tentando de novo."
      rm -rf "$DERIVED_DATA"
      derived_data_wiped=1
      continue
    fi

    if [ "$full_reexport_done" -eq 0 ] && grep -qE "Build input files cannot be found|Sandbox:.*deny|[Nn]o such file" "$XCODEBUILD_LOG"; then
      echo "[AUTO-FIX] projeto Xcode incremental parece inconsistente — refazendo export completo do Unity."
      rm -rf "$XCODE_EXPORT_DIR"
      full_reexport_done=1
      if ! run_unity_export; then
        return 1
      fi
      continue
    fi

    if [ "$signing_retry" -eq 0 ] && grep -qE "Failed to register bundle identifier|doesn.?t include the currently selected device|requires a provisioning profile" "$XCODEBUILD_LOG"; then
      echo "[AUTO-FIX] hiccup de provisioning/assinatura — Xcode costuma resolver isso numa segunda tentativa, retentando."
      signing_retry=1
      continue
    fi

    # "Device is busy / Waiting to reconnect" é transitório — acontece sobretudo
    # logo após ativar o Developer Mode (o aparelho reinicia e o Xcode leva um
    # tempo pra re-preparar o device pra debug). Diferente do caso abaixo
    # ("precisa ativar Developer Mode"), aqui não falta nenhuma ação humana —
    # só esperar a reconexão terminar.
    if [ "$reconnect_attempts" -lt "$max_reconnect_attempts" ] && grep -qE "Device is busy|Waiting to reconnect" "$XCODEBUILD_LOG"; then
      reconnect_attempts=$((reconnect_attempts + 1))
      echo "[AUTO-FIX] iPhone ainda reconectando/preparando pro Xcode (tentativa $reconnect_attempts/$max_reconnect_attempts) — esperando ${reconnect_wait}s e retentando."
      sleep "$reconnect_wait"
      continue
    fi

    if grep -qE "needs to be prepared for development|Developer Mode|Timed out waiting for all destinations" "$XCODEBUILD_LOG"; then
      echo "[ACTION-NEEDED] O iPhone precisa ser preparado pra desenvolvimento — isso exige ação manual no aparelho, não dá pra automatizar:" >&2
      echo "  1. Desbloqueie o iPhone e mantenha a tela acesa/conectada." >&2
      echo "  2. No iPhone: Settings > Privacy & Security > Developer Mode > ative." >&2
      echo "  3. O aparelho vai pedir reinício; depois de reiniciar, desbloqueie e confirme 'Turn On' quando perguntado." >&2
      echo "  4. Rode o build de novo." >&2
      return 1
    fi

    echo "Build falhou e não bate com nenhum padrão conhecido pra correção automática. Últimas linhas:" >&2
    tail -n 60 "$XCODEBUILD_LOG" >&2
    return 1
  done
}

resolve_device_id() {
  echo "==> [2/4] Localizando o iPhone pareado..."
  local device_json="$PROJECT_PATH/Builds/devices.json"
  xcrun devicectl list devices --json-output "$device_json" > /dev/null
  local py_out
  py_out=$(python3 -c "
import json
with open('$device_json') as f:
    data = json.load(f)
for d in data['result']['devices']:
    name = d['deviceProperties']['name']
    if '$DEVICE_NAME' in name:
        print(d['identifier'])
        print(name)
        break
")
  rm -f "$device_json"
  DEVICE_ID=$(sed -n '1p' <<< "$py_out")
  # -destination do xcodebuild usa o UDID clássico (não o identifier do devicectl,
  # que é um namespace diferente e só serve pra "devicectl device install/launch"),
  # então resolvemos por nome em vez de id pra evitar esse descompasso.
  DEVICE_FULL_NAME=$(sed -n '2p' <<< "$py_out")
  if [ -z "$DEVICE_ID" ]; then
    echo "iPhone '$DEVICE_NAME' não encontrado. Conecte via cabo, desbloqueie a tela e confie no computador." >&2
    return 1
  fi
  echo "Device: $DEVICE_FULL_NAME ($DEVICE_ID)"
  return 0
}

install_and_launch() {
  echo "==> [4/4] Instalando e abrindo no iPhone..."
  local app_path
  app_path=$(find "$DERIVED_DATA/Build/Products/Debug-iphoneos" -maxdepth 1 -name "*.app" -print -quit)
  if [ -z "$app_path" ]; then
    echo "Não encontrei o .app gerado em $DERIVED_DATA/Build/Products/Debug-iphoneos" >&2
    return 1
  fi
  echo ".app: $app_path"

  if ! xcrun devicectl device install app --device "$DEVICE_ID" "$app_path"; then
    echo "Instalação falhou — confira se o iPhone está desbloqueado e confiando neste Mac." >&2
    return 1
  fi
  if ! xcrun devicectl device process launch --device "$DEVICE_ID" "$BUNDLE_ID"; then
    echo "App instalado, mas não abriu automaticamente — abra manualmente no iPhone." >&2
  fi
  return 0
}

main() {
  run_unity_export || exit 1
  resolve_device_id || exit 1
  run_xcodebuild_with_heal || exit 1
  install_and_launch || exit 1
  echo "Pronto! App instalado e aberto no iPhone."
}

main
