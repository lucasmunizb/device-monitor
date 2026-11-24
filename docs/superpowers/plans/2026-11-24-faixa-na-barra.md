# Faixa na barra de tarefas — Plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (execução nativa, decidida no bootstrap). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mostrar `🎧 70%  🖱 86%` (cor por nível, raio ao carregar) numa faixa dentro da barra de tarefas, à esquerda da bandeja, com os ícones de bandeja como fallback.

**Architecture:** Regras puras em `Battery.cs` (nível/texto) e `Strip.cs` (posição, quando cair para ícones), testadas em `tests/Tests.cs`. A janela é um `Form` sem borda, `WS_EX_LAYERED`, filha de `Shell_TrayWnd` via `SetParent`, desenhada com `UpdateLayeredWindow` (alfa por pixel). `Tray` passa a ser o dono: alimenta faixa e ícones, e alterna entre eles.

**Tech Stack:** C# 5, .NET Framework 4, csc.exe, WinForms, GDI+, P/Invoke user32/gdi32. Zero pacotes.

**Spec:** `docs/superpowers/specs/2026-11-24-faixa-na-barra-design.md`

## Global Constraints
- Cores: vermelho 0–20, amarelo 21–60, verde 61–100; sem leitura = `—` cinza.
- Ordem: headset, depois mouse. Símbolos da fonte `Segoe Fluent Icons` (instalada na máquina, conferido 2026-11-24).
- Fallback para ícones só após **10 s seguidos** sem barra; volta para a faixa quando a barra reaparece.
- Poll de posição a cada 1 s. Manifest Windows 8+ embutido (`csc /win32manifest`), senão janela em camadas filha não funciona.
- Só monitor principal, barra embaixo. Nada de pacote, nada de mexer no registro além do que já existe.
- `test.ps1` compila `src/*.cs` menos `Program.cs`: código novo em `src/` precisa compilar sem `Program`.

## Review Focus
- Explorer reinicia: `Shell_TrayWnd` vira outra janela → a faixa precisa reanexar (handle velho inválido), sem ícone piscar antes de 10 s. Teste: `Strip.IconsVisible` (Task 2).
- Barra na lateral/topo ou ocultar automaticamente com barra escondida: retângulo estranho → `Place` devolve null e a faixa se esconde, sem exceção. Teste: `Place` com barra vertical e bandeja fora da barra (Task 2).
- Leitura chega enquanto a faixa está desanexada/sendo destruída no Sair → descartar, como `Tray.Post` já faz.
- `Percent` 0 e 100 nos limites das cores, e leitura de carregando com `Percent` null (mouse no cabo sem anterior) → raio + `—`. Teste: Task 1.
- Tema claro: amarelo/verde ilegíveis sobre barra clara → paleta escura quando `SystemUsesLightTheme = 1` (checagem na mão trocando o tema).

---

### Task 1: Nível e texto da bateria

**Files:**
- Modify: `src/Battery.cs`
- Test: `tests/Tests.cs` (novo bloco `Faixa()` chamado de `Main`)

**Interfaces:**
- Produces: `enum Level { None, Low, Mid, High }`; `static Level Battery.LevelOf(Reading r)`; `static string Battery.PercentText(Reading r)` → `"70%"` ou `"—"`.

- [ ] **Step 1: Teste falhando**
```csharp
static void Faixa()
{
    Eq(Level.Low,  Battery.LevelOf(P(0)),   "0 vermelho");
    Eq(Level.Low,  Battery.LevelOf(P(20)),  "20 vermelho");
    Eq(Level.Mid,  Battery.LevelOf(P(21)),  "21 amarelo");
    Eq(Level.Mid,  Battery.LevelOf(P(60)),  "60 amarelo");
    Eq(Level.High, Battery.LevelOf(P(61)),  "61 verde");
    Eq(Level.High, Battery.LevelOf(P(100)), "100 verde");
    Eq(Level.None, Battery.LevelOf(P(null)), "sem % cinza");
    Eq(Level.None, Battery.LevelOf(null),   "sem leitura cinza");
    Eq(Level.None, Battery.LevelOf(Battery.Missing()), "receptor ausente cinza");
    Eq("70%", Battery.PercentText(P(70)), "texto 70%");
    Eq("—",   Battery.PercentText(null),  "texto sem leitura");
    Eq("—",   Battery.PercentText(new Reading { Charging = true }), "carregando sem % traço");
}
```
- [ ] **Step 2:** rodar `test.ps1` → FALHA de compilação (`Level` não existe).
- [ ] **Step 3:** implementar em `Battery.cs`. `Low()` e `Label()` ficam (usados pelos ícones de fallback).
- [ ] **Step 4:** rodar `test.ps1` → `OK N testes`.

### Task 2: Posição e regra de fallback

**Files:**
- Create: `src/Strip.cs` (só a classe estática de regras nesta task)
- Test: `tests/Tests.cs` (no bloco `Faixa()`)

**Interfaces:**
- Produces: `static class StripRules { public const int Gap = 4; public static Rectangle? Place(Rectangle bar, Rectangle tray, Size strip); public static bool IconsVisible(DateTime? missingSince, DateTime now); }`
  - `Place` devolve o retângulo **em coordenadas do cliente da barra** (a faixa é filha dela): `x = tray.Left - bar.Left - strip.Width - Gap`, `y = (bar.Height - strip.Height) / 2`. `null` se a barra for vertical (`bar.Height > bar.Width`), se a bandeja não estiver dentro da barra, ou se `x < 0`.
  - `IconsVisible` = `missingSince != null && now - missingSince >= 10 s`.

- [ ] **Step 1: Teste falhando** (valores medidos na máquina em 2026-11-24)
```csharp
var bar = new Rectangle(0, 1032, 2560, 48); var tray = new Rectangle(2206, 1032, 354, 48);
Eq((Rectangle?)new Rectangle(2206 - 140 - 4, 4, 140, 40), StripRules.Place(bar, tray, new Size(140, 40)), "colada à bandeja");
Eq(null, StripRules.Place(new Rectangle(0, 0, 48, 1080), new Rectangle(0, 900, 48, 180), new Size(140, 40)), "barra vertical");
Eq(null, StripRules.Place(bar, new Rectangle(100, 1032, 50, 48), new Size(140, 40)), "sem espaço");
Eq(null, StripRules.Place(bar, new Rectangle(0, 0, 0, 0), new Size(140, 40)), "bandeja fora da barra");
var t0 = new DateTime(2026, 11, 24, 12, 0, 0);
Eq(false, StripRules.IconsVisible(null, t0), "barra presente");
Eq(false, StripRules.IconsVisible(t0, t0.AddSeconds(9)), "explorer reiniciando não pisca");
Eq(true,  StripRules.IconsVisible(t0, t0.AddSeconds(10)), "10 s sem barra: ícones");
```
- [ ] **Step 2:** rodar → FALHA (`StripRules` não existe).
- [ ] **Step 3:** implementar em `src/Strip.cs` (`using System.Drawing;`).
- [ ] **Step 4:** rodar → OK.

### Task 3: Janela da faixa

**Files:**
- Modify: `src/Strip.cs` (adiciona `class Strip : Form`)
- Create: `src/app.manifest` (compatibility `supportedOS` Windows 8, 8.1, 10/11)
- Modify: `build.ps1` (`/win32manifest:app.manifest`; copiar `src\*.manifest` junto). `test.ps1` não muda.

**Interfaces:**
- Consumes: `Battery.LevelOf`, `Battery.PercentText`, `StripRules.Place`, `Reading`.
- Produces: `class Strip : Form { public Strip(ContextMenuStrip menu); public bool Attached { get; } public event Action AttachedChanged; public void Show(string device /* "Headset"|"Mouse" */, Reading r, string tooltip); }`

- [ ] **Step 1:** `CreateParams`: `WS_POPUP` trocado por nada até anexar; ExStyle `WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`. `ShowWithoutActivation = true`, `ShowInTaskbar = false`, sem borda.
- [ ] **Step 2:** Timer de 1 s: `FindWindow("Shell_TrayWnd")` e `FindWindowEx(bar, "TrayNotifyWnd")`; se o handle da barra mudou, `SetParent(Handle, bar)` + estilo `WS_CHILD`; `GetWindowRect` dos dois → `StripRules.Place` → `SetWindowPos` (`SWP_NOACTIVATE | SWP_NOZORDER`) ou `Hide()` se null. Anexado = barra achada e `Place != null`; disparar `AttachedChanged` só na mudança.
- [ ] **Step 3:** Desenho: bitmap 32bpp `PArgb` do tamanho da faixa, fundo transparente; por aparelho: glifo (headset ``, mouse ``, `Segoe Fluent Icons` 12 px), raio `` amarelo se `Charging`, texto `Segoe UI` semibold 9 pt na cor do `Level`. Paleta escura (tema escuro): branco/`#FF5A5A`/`#FFD23F`/`#5BD15B`/cinza `#9A9A9A`; clara (`HKCU\...\Themes\Personalize\SystemUsesLightTheme = 1`): preto/`#C42B1C`/`#9D5D00`/`#0F7B0F`/`#6E6E6E`. Largura calculada pelo texto medido. Enviar com `UpdateLayeredWindow(..., ULW_ALPHA)`, `BLENDFUNCTION { AlphaFormat = AC_SRC_ALPHA, SourceConstantAlpha = 255 }`; liberar `hBitmap`/DC.
- [ ] **Step 4:** Mouse: `ToolTip` com as duas linhas (`Headset 70%` / `Mouse 86% — Carga completa`); clique direito → `menu.Show(Cursor.Position)`.
- [ ] **Step 5:** `build.ps1` compila; `test.ps1` continua OK (Strip.cs entra na compilação de teste).

### Task 4: Tray alterna faixa ↔ ícones

**Files:**
- Modify: `src/Tray.cs`

**Interfaces:**
- Consumes: `Strip` (Task 3), `StripRules.IconsVisible` (Task 2).

- [ ] **Step 1:** `Tray` cria um `Strip` com o mesmo menu (um `ContextMenuStrip` com os dois status no topo); `OnMouse`/`OnHeadset` atualizam slot **e** faixa (via `Post`, como hoje); o texto de tooltip sai do mesmo código que monta `icon.Text`.
- [ ] **Step 2:** Ícones começam ocultos. No tick de 1 s: `missingSince` = agora se `!strip.Attached` e ainda null; null se anexado. `icon.Visible = StripRules.IconsVisible(missingSince, DateTime.Now)` nos dois slots.
- [ ] **Step 3:** `Dispose` fecha a faixa (`SetParent` de volta a `IntPtr.Zero` antes de destruir, para não deixar filho órfão na barra).
- [ ] **Step 4:** `build.ps1` → `OK` + exe. Checagem na mão (usuário, exe novo instalado): faixa colada à bandeja; cabo do mouse liga raio; headset desligado vira `—` cinza; ícone novo na bandeja reposiciona; `taskkill /f /im explorer.exe` + `explorer.exe` reanexa sem ícones piscarem; jogo em tela cheia esconde; clique direito abre menu; tema claro legível.
- [ ] **Step 5:** README: trocar o parágrafo dos ícones pela faixa. Entregar blocos de commit (um `git add` por arquivo).
