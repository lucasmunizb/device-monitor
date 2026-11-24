#!/usr/bin/env bash
# PostToolUse Edit|Write: edição em src/ ou tests/ (.cs) roda test.ps1; vermelho devolve decision:block com a saída.
file=$(python3 -c 'import json,sys; print(json.load(sys.stdin).get("tool_input",{}).get("file_path",""))')
case "$file" in
  "$CLAUDE_PROJECT_DIR"/src/*.cs|"$CLAUDE_PROJECT_DIR"/tests/*.cs) ;;
  *) exit 0 ;;
esac
cd "$CLAUDE_PROJECT_DIR" || exit 0
out=$(powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(wslpath -w test.ps1)" 2>&1) && exit 0
printf '%s' "$out" | tail -40 | python3 -c 'import json,sys; print(json.dumps({"decision":"block","reason":"test.ps1 vermelho:\n"+sys.stdin.read()}))'
