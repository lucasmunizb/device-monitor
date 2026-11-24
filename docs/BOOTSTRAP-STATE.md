# Estado do bootstrap
Fase concluída: 3 (primeira fatia implementada, 33 testes, revisada)   Próxima: 4 .claude/ do projeto   Atualizado: 2026-11-24

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
- nada ainda (hooks da Fase 4 vão exigir code.claude.com/docs/en/hooks)

## Fase 3 — notas
- Sem /health, k6 nem EXAMPLE-TDD.md (versão enxuta; tests/Tests.cs é o exemplo vivo).
- Minors adiados da revisão: Atualizar apaga o raio do headset por até 30 s; "Sem leitura" mantém lastMv; ícone fantasma após Kill; Uninstall sem try/catch; instalação parcial se falhar depois da cópia.

## Próximo passo exato
- Fase 4 enxuta: CLAUDE.md (<150 linhas, comandos test.ps1/build.ps1 via powershell.exe), rules tdd.md + ia-limites.md, hook PostToolUse rodando test.ps1 ao editar src/tests (conferir code.claude.com/docs/en/hooks antes).
