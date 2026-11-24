# Device Monitor — design (primeira fatia)

## Problema
Ver a bateria do mouse DELUX M900 Pro e do headset HyperX Cloud II Core Wireless na barra de tarefas do Windows, sem abrir app de fabricante. Uso pessoal, um PC só.

## Primeira fatia: tray com dois percentuais e raio de carga
Executável único `DeviceMonitor.exe`, compilado com o `csc.exe` do .NET Framework (já vem no Windows, sem pacotes).

Critérios de aceite (cada um vira teste dos parsers ou checagem manual marcada [M]):
1. Mouse `03 30 40 01 61` → 97%, sem raio.
2. Mouse estado `03` → raio; mantém o último % lido sem cabo (firmware infla o valor na carga).
3. Mouse estado `02` → 100%, "Carga completa", sem raio.
4. Headset `66 89 HI LO PCT` → `PCT`%; `PCT > 100` → sem leitura.
5. Headset: tensão `HI<<8|LO` (mV) subiu ≥ 15 mV desde a leitura anterior → raio; caiu ou estável → sem raio.
6. Receptor ausente → "—" no lugar do % e texto "Receptor não encontrado" no menu.
7. % ≤ 20 → número em vermelho.
8. [M] Dois ícones na bandeja (mouse, headset) com o % desenhado; clique direito: Atualizar, Iniciar com o Windows, Sair.
9. [M] `install.ps1` copia o exe para `%LOCALAPPDATA%\DeviceMonitor` e liga "Iniciar com o Windows" (chave `HKCU\...\Run`); `install.ps1 -Remove` desfaz.

Leitura: mouse ouvindo o report empurrado (interface UsagePage `000A`); headset consulta `66 89` a cada 30s (UsagePage `FF13`, 62 bytes).

## Fora de escopo
Faixa desenhada dentro da barra de tarefas (o projeto base tem; aqui ícone de bandeja basta), outros aparelhos, detecção genérica de qualquer marca, Bluetooth, instalador MSI, assinatura do exe, auto-update, tema claro/escuro além do contraste do ícone.

## Riscos
- Detecção de carga do headset é por tendência de tensão (medido: +27 mV/10s no cabo, −25 mV/10s logo após tirar). Perto de 100% no cabo a tensão estabiliza → raio some; aceitável, mostra "Carga completa" se 100%.
- Protocolo do headset não é oficial; firmware novo pode mudar os bytes → parser devolve "sem leitura", não quebra.
- Projeto base (rick1135/wireless-devices-monitor) não tem licença: só referência; código daqui é próprio e o repo não deve ser publicado com trechos copiados.
- LGPD/segurança/carga: não se aplica (nada sai da máquina, sem rede, sem dado pessoal).

## Stack
C# 5 / .NET Framework 4.x via `csc.exe` + WinForms `NotifyIcon` + P/Invoke `hid.dll`/`setupapi.dll`. Zero dependência e build em uma linha. Testes: `tests/Tests.cs` compilado como console com asserts (sem NuGet), rodado por `test.ps1`. Desenvolvimento no WSL chamando `powershell.exe`.
