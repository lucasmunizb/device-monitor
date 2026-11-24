# Device Monitor — design (primeira fatia)

## Problema
Ver a bateria do mouse DELUX M900 Pro e do headset HyperX Cloud II Core Wireless na barra de tarefas do Windows, sem abrir app de fabricante. Uso pessoal, um PC só.

## Primeira fatia: tray com dois percentuais e raio de carga
Executável único que é instalador e monitor ao mesmo tempo, compilado com o `csc.exe` do .NET Framework (já vem no Windows, sem pacotes).

Critérios de aceite (cada um vira teste dos parsers ou checagem manual marcada [M]):
1. Mouse `03 30 40 01 61` → 97%, sem raio.
2. Mouse estado `03` → raio; mantém o último % lido sem cabo (firmware infla o valor na carga).
3. Mouse estado `02` → 100%, "Carga completa", sem raio.
4. Headset `66 89 HI LO PCT` → `PCT`%; `PCT > 100` → sem leitura.
5. Headset: tensão `HI<<8|LO` (mV) subiu ≥ 15 mV desde a leitura anterior → raio; caiu ou estável → sem raio.
6. Receptor ausente → "—" no lugar do % e texto "Receptor não encontrado" no menu.
7. % ≤ 20 → número em vermelho.
8. [M] Dois ícones na bandeja (mouse, headset) com o % desenhado; clique direito: Atualizar, Iniciar com o Windows, Sair.
9. [M] Instalação por duplo clique: baixar `Instalar-Monitor-de-Bateria.exe` pelo link do README (vai para Downloads) e clicar. Rodando fora da pasta de instalação, o exe se copia para `%LOCALAPPDATA%\MonitorDeBateria\MonitorDeBateria.exe`, cria atalho no Menu Iniciar, liga "Iniciar com o Windows" (`HKCU\...\Run`), registra em "Aplicativos instalados" (`HKCU\...\Uninstall`, desinstalar = `--desinstalar`), abre o monitor e avisa "Instalado". Sem admin (tudo em HKCU/LocalAppData). Clicar de novo com versão já instalada = atualiza (fecha a instância aberta, substitui, reabre).
10. [M] O README tem o link fixo `.../releases/latest/download/Instalar-Monitor-de-Bateria.exe`; o `build.ps1` gera o arquivo com esse nome.

Leitura: mouse ouvindo o report empurrado (interface UsagePage `000A`, 5 bytes) no cabo `1D57:FA73` se presente, senão no receptor `1D57:FA65` (no cabo o receptor fica mudo; medido: `03 30 40 03 64`); headset consulta `66 89` a cada 30s (UsagePage `FF13`, 62 bytes).

## Fora de escopo
Faixa desenhada dentro da barra de tarefas (o projeto base tem; aqui ícone de bandeja basta), outros aparelhos, detecção genérica de qualquer marca, Bluetooth, instalador MSI/Inno Setup, assinatura do exe, publicação automática da Release pelo CI, auto-update, tema claro/escuro além do contraste do ícone.

## Riscos
- Detecção de carga do headset é por tendência de tensão (medido: +27 mV/10s no cabo, −25 mV/10s logo após tirar). Perto de 100% no cabo a tensão estabiliza → raio some; aceitável, mostra "Carga completa" se 100%.
- Protocolo do headset não é oficial; firmware novo pode mudar os bytes → parser devolve "sem leitura", não quebra.
- Projeto base (rick1135/wireless-devices-monitor) não tem licença: só referência; código daqui é próprio e o repo não deve ser publicado com trechos copiados.
- Exe sem assinatura baixado da internet → SmartScreen mostra "O Windows protegeu o computador" no 1º clique ("Mais informações → Executar assim mesmo"). Assinar custa certificado pago; fora de escopo.
- Link de download depende do repo no GitHub com Release publicada (pendência do usuário: criar o repo).
- LGPD/segurança/carga: não se aplica (nada sai da máquina, sem rede, sem dado pessoal).

## Stack
C# 5 / .NET Framework 4.x via `csc.exe` + WinForms `NotifyIcon` + P/Invoke `hid.dll`/`setupapi.dll`. Zero dependência e build em uma linha. Testes: `tests/Tests.cs` compilado como console com asserts (sem NuGet), rodado por `test.ps1`. Desenvolvimento no WSL chamando `powershell.exe`.
