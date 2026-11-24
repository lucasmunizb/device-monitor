# Monitor de Bateria

Tray do Windows (uso pessoal) que mostra a bateria do mouse DELUX M900 Pro e do headset HyperX Cloud II Core Wireless. Um exe que se auto-instala em HKCU, sem admin.

## Stack
C# 5 / .NET Framework 4 compilado com `csc.exe` (já vem no Windows), WinForms `NotifyIcon`, P/Invoke `hid.dll`. Zero pacotes.

## Comandos (no WSL)
- teste: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(wslpath -w test.ps1)"`
- build: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(wslpath -w build.ps1)"` → `dist\Instalar-Monitor-de-Bateria.exe`
- Os scripts copiam as fontes para `%TEMP%` porque o csc não aceita caminho `\\wsl$`.

## Layout
- `src/` — `Hid.cs` (P/Invoke), `Battery.cs` (parsers), `Devices.cs` (leitura), `Tray.cs`, `Installer.cs`, `Program.cs`
- `tests/Tests.cs` — console com asserts (sem framework); é o exemplo vivo de TDD
- `docs/superpowers/` — design e plano por fatia (ler sob demanda)
- `docs/BOOTSTRAP-STATE.md` — decisões e protocolo HID medido

## Workflow — TDD estrito, sempre nesta ordem
1. RED: escreva o teste que descreve o comportamento. Rode. Confirme que FALHA.
2. GREEN: código mínimo para passar. Rode. Confirme que PASSA.
3. REFACTOR: limpe sem mudar comportamento. Rode de novo.
Nunca escreva implementação antes do teste. Nunca ajuste o teste para caber na implementação.
Toda mudança nasce em branch. O hook `.claude/hooks/test-on-edit.sh` roda o teste a cada edição em `src/` ou `tests/`.
Regras: `.claude/rules/`.
