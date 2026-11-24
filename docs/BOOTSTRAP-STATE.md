# Estado do bootstrap
Fase concluída: 7 — BOOTSTRAP CONCLUÍDO   Atualizado: 2026-11-24

## Decisões tomadas (não reperguntar)
- Produto: tray pessoal Windows, bateria do mouse DELUX M900 Pro (receptor 1D57:FA65) e headset HyperX Cloud II Core Wireless (03F0:0995). Design: docs/superpowers/specs/2026-11-24-device-monitor-design.md
- Execução do plano: nativa (superpowers:executing-plans), um revisor independente no fim do branch.
- Versão enxuta: sem Docker, deploy, LGPD, k6, /health, staging. Fases 5/6 da skill não se aplicam.
- Stack: C# 5 / .NET Framework 4.x via csc.exe, WinForms NotifyIcon, P/Invoke hid.dll. Zero pacotes.
- Testes: tests/Tests.cs console com asserts, rodado por test.ps1 (sem NuGet).
- Distribuição: um exe `Instalar-Monitor-de-Bateria.exe` que se auto-instala (HKCU, sem admin), link fixo no README via GitHub Release latest.
- Projeto base rick1135/wireless-devices-monitor sem licença: só referência, código próprio.
- Dev no WSL: compilar/rodar via powershell.exe numa pasta do Windows (%TEMP% = /mnt/c/Users/Muniz/AppData/Local/Temp). csc não confia em caminho \\wsl$; copiar fontes antes.

## Medido no hardware (2026-11-24)
- Mouse: interface UsagePage 000A, report 5 bytes `03 30 40 ESTADO PCT` (01 bateria, 02 cheio, 03 carregando). No cabo vira outro aparelho `1D57:FA73` (mesma interface 000A) e manda `03 30 40 03 64`; receptor FA65 fica mudo. Ler FA73 primeiro, depois FA65.
- Headset: UsagePage FF13, in/out 62. Envia `66 89` → `66 89 HI LO PCT`; HI<<8|LO = mV. Cabo: +27 mV/10s; sem cabo: −25 mV/10s. Bytes 5+ lixo.
- Probe de referência: C:\Users\Muniz\AppData\Local\Temp\devmon-probe (Probe.cs + Hid.cs).

## Pendências do usuário
- Commitar o branch feat/primeira-fatia (blocos na conversa) e publicar Release com dist\Instalar-Monitor-de-Bateria.exe.
- [M] Checar na mão: tirar o cabo do mouse com o monitor aberto (vira % sem raio), desligar o headset no meio (texto "Sem leitura"/"Receptor não encontrado", sem fechar), menu e Sair na bandeja.
- Medir: o mouse sem fio manda report parado? Se sim, dá para mostrar "Sem leitura" com o mouse desligado (achado #3 da revisão).

## Verificado na doc
- hooks (code.claude.com/docs/en/hooks) em 2026-11-24: PostToolUse matcher Edit|Write, stdin tool_input.file_path, stdout {"decision":"block","reason"} com exit 0, timeout em segundos, ${CLAUDE_PROJECT_DIR}.

## Fase 3 — notas
- Sem /health, k6 nem EXAMPLE-TDD.md (versão enxuta; tests/Tests.cs é o exemplo vivo).
- Minors adiados da revisão: Atualizar apaga o raio do headset por até 30 s; "Sem leitura" mantém lastMv; ícone fantasma após Kill; Uninstall sem try/catch; instalação parcial se falhar depois da cópia.

## Fase 4 — notas
- Criado: CLAUDE.md, .claude/settings.json, .claude/hooks/test-on-edit.sh, .claude/rules/tdd.md, .claude/rules/ia-limites.md.
- Hook testado à mão: verde = silêncio; vermelho = decision:block com a saída do test.ps1; arquivo fora de src/tests = ignorado.
- Sem skills/agents do projeto, PreToolUse, Stop, PreCompact, SessionStart (versão enxuta; regras globais já barram git).

## Fase 7 — verificação (2026-11-24)
- [x] CLAUDE.md 25 linhas; comandos teste e build rodam (OK 33 testes; build gera dist\Instalar-Monitor-de-Bateria.exe).
- [x] Hook dispara de verdade em sessão nova: Edit quebrando um assert em tests/Tests.cs → bloqueio "FALHA mouse 97: esperado 98, veio 97"; revertido, suíte verde.
- [x] hooks verificados na doc (ver acima); design e plano da fatia em docs/superpowers/.
- N/A (versão enxuta): /health, k6, CI, Docker, LGPD, skills/agents do projeto.

## Próximo passo exato
- Bootstrap fechado. Próximas mudanças: branch nova + TDD pelo CLAUDE.md. Pendências do usuário acima continuam valendo.
