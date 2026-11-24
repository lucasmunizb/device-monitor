# Faixa na barra de tarefas — design

## Intenção
Ver a bateria do headset e do mouse de relance, como no print do usuário: `🎧 70%  🖱 86%` escrito na barra de tarefas, ao lado da bandeja. O ícone de bandeja (16×16 px) não comporta símbolo + número + %. Uso pessoal, uma máquina.

## Máquina medida (2026-11-24, leitura)
Windows 11 Pro, tema escuro, 96 DPI. Monitor principal 2560×1080; barra `Shell_TrayWnd` em y 1032–1080 (48 px); bandeja `TrayNotifyWnd` começa em x 2206. Segundo monitor 1920×1080 com barra própria (`Shell_SecondaryTrayWnd`).

## Abordagens
- **A — janela própria dentro da barra (escolhida pelo usuário).** Igual ao print. Sem API oficial no Windows 11: depende de achar a bandeja e se reposicionar. Mesmo caminho do TrafficMonitor.
- B — continuar no ícone 16 px com número colorido e letra/símbolo minúsculo. Estável, mas não chega no visual pedido.
- (descartada) Deskband/toolbar: removidos no Windows 11.

## Como fica
```
 … [apps fixados] …         🎧 70%  🖱 86%  │ ^  ⟳  🎤  POR  📶 🔊  13:49
                            └── faixa ──┘     └── bandeja (TrayNotifyWnd) ──┘
 carregando:  🎧⚡ 45%     sem leitura:  🖱 —  (cinza)
```
- Símbolos da fonte do sistema **Segoe Fluent Icons** (headphone, mouse, raio): mesmo estilo dos ícones do Windows 11. Ordem: headset, mouse (como no print).
- Cor do número: vermelho 0–20, amarelo 21–60, verde 61–100. Símbolo na cor do texto da barra (branco no tema escuro).
- Carregando: raio amarelo entre o símbolo e o número. Sem leitura / receptor não encontrado: `—` em cinza.
- Passar o mouse: tooltip com o texto de hoje (`Mouse 86% — Carga completa`).
- Clique direito: o mesmo menu de hoje (status, Atualizar, Iniciar com o Windows, Sair).

## Comportamento
- A faixa vira **filha da barra** (`SetParent` em `Shell_TrayWnd`): some junto com a barra em tela cheia e com ocultar automaticamente; não fica por cima de jogo.
- Posição: colada à esquerda de `TrayNotifyWnd`, centralizada na altura da barra. Checa a cada 1 s; se a bandeja mudou de largura (ícone novo, idioma) ou o Explorer reiniciou (janela nova), reposiciona/reanexa.
- Fundo transparente para mostrar o acrílico da barra: janela em camadas com **alfa por pixel** (`UpdateLayeredWindow`, bitmap 32 bits pré-multiplicado). Cor-chave descartada: deixa halo no texto suavizado sobre o acrílico. Exige manifest declarando Windows 8+ embutido via `csc /win32manifest`. *(decisão do agente)*
- Tema claro: cores mais escuras para o amarelo/verde e símbolo preto, lidos de `SystemUsesLightTheme` na hora de desenhar.
- **Ícones de bandeja somem** enquanto a faixa estiver presa na barra. Se a barra não for achada por **10 s seguidos**, volta aos ícones atuais (fallback, código que já funciona); se ela reaparecer depois, a faixa volta e os ícones saem. Reinício do Explorer (barra some por poucos segundos) não pisca ícone. *(decisão do agente)*

## Metas / não-metas / anti-metas
- Meta: legível de relance a 100% de escala, tema escuro; não atrapalhar a barra.
- Não-metas: segundo monitor; barra na lateral/topo; Windows 10; arrastar a faixa; configurar cores.
- Anti-meta: cobrir os apps fixados ou a bandeja; roubar foco; travar ou piscar a barra. Se em teste na mão cobrir algo, o design volta para cá.

## Testes (TDD) e checagem na mão
- Automático (`tests/Tests.cs`): regra de cor por faixa (0, 20, 21, 60, 61, 100, sem leitura); texto do número (`70%`, `—`); cálculo da posição dado o retângulo da barra e da bandeja e a largura da faixa.
- Na mão: aparece colada à bandeja; muda ao conectar cabo/desligar headset; reposiciona ao surgir ícone novo na bandeja; reinício do Explorer (`taskkill /f /im explorer.exe` + abrir de novo) reanexa; tela cheia esconde; clique direito abre o menu.

## Riscos
- Atualização do Windows pode mudar `Shell_TrayWnd`/`TrayNotifyWnd` → cai no fallback dos ícones.
- Barra cheia de apps fixados pode encostar na faixa. Sem proteção automática: a barra do Windows 11 não expõe onde os apps terminam sem UI Automation, e hoje sobram ~1000 px. Fica na checagem na mão; se acontecer, volta para este design. *(decisão do agente)*

## Fica com quem constrói
Tamanho da fonte e espaçamentos exatos, nomes de classes, como desenhar (GDI+), intervalo exato do poll.
