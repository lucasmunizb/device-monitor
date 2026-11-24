# Monitor de Bateria (primeira fatia) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Um exe Windows que mostra na bandeja a bateria do mouse M900 Pro e do headset Cloud II Core, e que se auto-instala por duplo clique.

**Architecture:** Parsers puros (`Battery.cs`) recebem bytes e devolvem `Reading`; são a parte testada. `Hid.cs` (já escrito no probe) lê os aparelhos; `Tray.cs` desenha os ícones; `Installer.cs` cuida de cópia/registro. `Program.cs` liga tudo num timer.

**Tech Stack:** C# 5, .NET Framework 4.x, `csc.exe` (`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`), WinForms, P/Invoke `hid.dll`/`setupapi.dll`. Zero pacotes.

**Spec:** `docs/superpowers/specs/2026-11-24-device-monitor-design.md`

## Global Constraints

- C# 5 só: sem interpolação `$""`, sem `?.`, sem `=>` em membros, sem `nameof`.
- Zero NuGet/pacotes. Testes = `tests/Tests.cs` console com asserts, exit code ≠ 0 se falhar.
- Dev no WSL: `test.ps1`/`build.ps1` copiam `src/` e `tests/` para `%TEMP%\devmon-build` antes de chamar `csc` (csc não confia em `\\wsl$`). Rodar com `powershell.exe -ExecutionPolicy Bypass -File "$(wslpath -w test.ps1)"`.
- Mouse: cabo `vid_1d57&pid_fa73&` primeiro, senão receptor `vid_1d57&pid_fa65&`; interface `UsagePage == 0x000A`, input 5 bytes `03 30 40 ESTADO PCT` (01 bateria, 02 cheio, 03 carregando).
- Headset: `vid_03f0&pid_0995&`, `UsagePage == 0xFF13`, out/in 62; envia `66 89` → `66 89 HI LO PCT`; consulta a cada 30 s.
- Textos da UI: "—", "Receptor não encontrado", "Carga completa", "Atualizar", "Iniciar com o Windows", "Sair", "Instalado".
- Nome do exe gerado: `Instalar-Monitor-de-Bateria.exe`. Instalado em `%LOCALAPPDATA%\MonitorDeBateria\MonitorDeBateria.exe`. Tudo em HKCU, sem admin.
- Git: o agente não commita. Cada "Commit" abaixo = entregar o bloco `git add` (um arquivo por linha) + `git commit -m` para o usuário.

## Review Focus

1. Report curto ou de outro tipo (mouse com < 5 bytes ou sem `03 30 40`; headset com resposta que não começa `66 89`) → "sem leitura", nunca exceção. Teste na Task 1 e 2.
2. Mouse no cabo logo ao abrir (sem % anterior sem cabo) → `Percent = null`, raio ligado (não mostrar o valor inflado). Teste na Task 1.
3. Estado de mouse desconhecido (ex.: `04`) → sem leitura. Teste na Task 1.
4. Primeira leitura do headset (sem mV anterior) → sem raio. Teste na Task 2.
5. Aparelho desconectado no meio da leitura (Transfer/Open lança) → vira `Battery.Missing()`, o timer segue. Checagem manual na Task 4.

---

### Task 1: Harness de teste + parser do mouse (critérios 1–3)

**Files:**
- Create: `test.ps1`, `tests/Tests.cs`, `src/Battery.cs`

**Interfaces:**
- Produces:
  - `class Reading { public int? Percent; public bool Charging; public bool Full; public string Status; }` (`Status` = null quando nada a dizer)
  - `static Reading Battery.Mouse(byte[] r, int n, Reading last)` — `last` pode ser null; devolve null se o report não serve
  - `tests/Tests.cs`: `static void Eq(object esperado, object real, string nome)` acumula falhas; `Main` retorna 1 se houver alguma e imprime `OK n testes` ou a lista.

- [ ] **Step 1: Escrever `test.ps1`** — copia `src\*.cs` e `tests\*.cs` para `%TEMP%\devmon-build`, compila `/target:exe /out:tests.exe` com todos (exceto `Program.cs`, que tem o `Main` do app), roda e propaga `$LASTEXITCODE`.
- [ ] **Step 2: Testes que falham**

```csharp
var a = Battery.Mouse(B(0x03,0x30,0x40,0x01,0x61), 5, null);
Eq(97, a.Percent, "mouse 97"); Eq(false, a.Charging, "mouse 97 sem raio");
var c = Battery.Mouse(B(0x03,0x30,0x40,0x03,0x64), 5, a);
Eq(97, c.Percent, "carregando mantém último %"); Eq(true, c.Charging, "raio");
var c0 = Battery.Mouse(B(0x03,0x30,0x40,0x03,0x64), 5, null);
Eq(null, c0.Percent, "carregando sem anterior"); Eq(true, c0.Charging, "raio sem anterior");
var f = Battery.Mouse(B(0x03,0x30,0x40,0x02,0x64), 5, a);
Eq(100, f.Percent, "cheio"); Eq("Carga completa", f.Status, "cheio texto"); Eq(false, f.Charging, "cheio sem raio");
Eq(null, Battery.Mouse(B(0x03,0x30,0x40,0x01), 4, null), "curto");
Eq(null, Battery.Mouse(B(0x01,0x30,0x40,0x01,0x61), 5, null), "outro report");
Eq(null, Battery.Mouse(B(0x03,0x30,0x40,0x04,0x61), 5, null), "estado desconhecido");
```

- [ ] **Step 3: Rodar** `powershell.exe -ExecutionPolicy Bypass -File "$(wslpath -w test.ps1)"` → falha de compilação (`Battery` não existe).
- [ ] **Step 4: Implementar `Reading` e `Battery.Mouse` em `src/Battery.cs`.** Carregando: `Percent = last == null ? null : last.Percent` (o último já carrega o % sem cabo adiante). Estado `02` → `Full = true`.
- [ ] **Step 5: Rodar** → `OK 12 testes`, exit 0.
- [ ] **Step 6: Commit** `test: harness e parser do mouse` (test.ps1, tests/Tests.cs, src/Battery.cs).

### Task 2: Parser do headset (critérios 4–5)

**Files:** Modify `src/Battery.cs`, `tests/Tests.cs`

**Interfaces:**
- Produces: `static Reading Battery.Headset(byte[] r, int n, int? lastMv, out int mv)` — null se não serve; `mv = HI<<8|LO` quando serve (0 caso contrário). Constante `const int ChargeRiseMv = 15`.

- [ ] **Step 1: Testes que falham**

```csharp
int mv;
var h = Battery.Headset(B(0x66,0x89,0x0F,0xA0,0x50), 62, null, out mv);
Eq(80, h.Percent, "headset 80"); Eq(4000, mv, "mV"); Eq(false, h.Charging, "1a leitura sem raio");
Eq(true,  Battery.Headset(B(0x66,0x89,0x0F,0xAF,0x50), 62, 4000, out mv).Charging, "+15 mV raio");
Eq(false, Battery.Headset(B(0x66,0x89,0x0F,0xAE,0x50), 62, 4000, out mv).Charging, "+14 mV sem raio");
Eq(false, Battery.Headset(B(0x66,0x89,0x0F,0x90,0x50), 62, 4000, out mv).Charging, "caiu sem raio");
Eq(null, Battery.Headset(B(0x66,0x89,0x0F,0xA0,0x65), 62, null, out mv), "PCT > 100");
Eq(null, Battery.Headset(B(0x66,0x8A,0x0F,0xA0,0x50), 62, null, out mv), "outra resposta");
Eq(null, Battery.Headset(B(0x66,0x89,0x0F), 3, null, out mv), "curto");
```
`B(params byte[])` devolve só os bytes dados; `n` diz quantos valem.

- [ ] **Step 2: Rodar** → falha de compilação.
- [ ] **Step 3: Implementar `Battery.Headset`.** PCT 100 → `Status = "Carga completa"`.
- [ ] **Step 4: Rodar** → OK, exit 0.
- [ ] **Step 5: Commit** `feat: parser do headset por tensão`.

### Task 3: Ausente, rótulo e alerta (critérios 6–7)

**Files:** Modify `src/Battery.cs`, `tests/Tests.cs`

**Interfaces:**
- Produces: `static Reading Battery.Missing()`; `static string Battery.Label(Reading r)` (`"97"` ou `"—"` se r ou Percent null); `static bool Battery.Low(Reading r)` (Percent ≤ 20).

- [ ] **Step 1: Testes que falham** — `Eq("—", Battery.Label(Battery.Missing()))`, `Eq("Receptor não encontrado", Battery.Missing().Status)`, `Eq("—", Battery.Label(null))`, `Eq(true, Low(20))`, `Eq(false, Low(21))`, `Eq(false, Low(Missing()))`.
- [ ] **Step 2: Rodar** → falha. **Step 3: Implementar.** **Step 4: Rodar** → OK.
- [ ] **Step 5: Commit** `feat: estado ausente, rótulo e alerta de bateria baixa`.

### Task 4: Leitura real dos aparelhos

**Files:** Create `src/Hid.cs` (copiar de `C:\Users\Muniz\AppData\Local\Temp\devmon-probe\Hid.cs`, código próprio da sessão anterior), `src/Devices.cs`

**Interfaces:**
- Consumes: `Hid.Find(string usbId) -> List<HidDevice>` (`d.Caps.UsagePage/Input/Output`), `Hid.Open(HidDevice, bool write) -> SafeFileHandle`, `Hid.Transfer(handle, byte[], bool write, int timeoutMs) -> int` (0 = timeout); `Battery.*`.
- Produces: `class Devices { public event Action<Reading> MouseChanged, HeadsetChanged; public void Start(); public void RefreshNow(); public void Stop(); }` — mouse numa thread que fica em `Transfer` (cabo FA73 primeiro, depois FA65; reabre a cada 5 s se nenhum); headset por `System.Threading.Timer` 30 s. Qualquer exceção de Open/Transfer → dispara `Battery.Missing()` e segue.

- [ ] **Step 1: Copiar `Hid.cs`**; `test.ps1` continua verde (compila junto).
- [ ] **Step 2: Implementar `Devices`.** Guarda `last` (mouse) e `lastMv` (headset) para os parsers.
- [ ] **Step 3: [M] Checagem** com um `Program.cs` provisório que imprime as leituras: receptor ligado → % do mouse; cabo no mouse → raio; desligar headset no meio → "Receptor não encontrado" sem fechar.
- [ ] **Step 4: Commit** `feat: leitura HID do mouse e do headset`.

### Task 5: Bandeja + build (critérios 8 e parte do 10)

**Files:** Create `src/Tray.cs`, `src/Program.cs`, `build.ps1`

**Interfaces:**
- Consumes: `Devices`, `Battery.Label/Low`, `Installer.SetAutoStart(bool)/IsAutoStart()` (Task 6; até lá o item fica sem ação).
- Produces: `build.ps1` gera `dist\Instalar-Monitor-de-Bateria.exe` com `/target:winexe`.

- [ ] **Step 1: `Tray`** — dois `NotifyIcon` (tooltip "Mouse 97%" / "Headset 80% — Carga completa"), ícone 16×16 com o rótulo desenhado (branco com contorno escuro; vermelho se `Low`; raio pequeno se `Charging`). Menu: Atualizar (`RefreshNow`), Iniciar com o Windows (checkable), Sair. Item de status com `Reading.Status` quando não nulo. Eventos de `Devices` chegam em outra thread → `Control.BeginInvoke`. Descartar o `Icon` anterior (`DestroyIcon`) a cada troca.
- [ ] **Step 2: `Program.Main`** — `Mutex` nomeado `MonitorDeBateria` (uma instância só), `Application.Run`.
- [ ] **Step 3: `build.ps1`** e `test.ps1` antes do build (aborta se vermelho).
- [ ] **Step 4: [M]** Rodar o exe: dois ícones, % certo, menu funciona, Sair remove os ícones.
- [ ] **Step 5: Commit** `feat: ícones na bandeja e build`.

### Task 6: Auto-instalação (critério 9)

**Files:** Create `src/Installer.cs`; Modify `src/Program.cs`, `tests/Tests.cs`

**Interfaces:**
- Produces: `static string Installer.Target` (= `%LOCALAPPDATA%\MonitorDeBateria\MonitorDeBateria.exe`); `static bool Installer.IsInstalledCopy(string exePath)` (comparação de caminho sem caixa, com `Path.GetFullPath`); `static void Installer.Install()`; `static void Installer.Uninstall()`; `static void SetAutoStart(bool)`, `static bool IsAutoStart()`.

- [ ] **Step 1: Teste que falha** — `IsInstalledCopy(Target)` true, `IsInstalledCopy(Target.ToUpper())` true, `IsInstalledCopy(@"C:\Users\x\Downloads\Instalar-Monitor-de-Bateria.exe")` false.
- [ ] **Step 2: Rodar** → falha. **Step 3: Implementar** `IsInstalledCopy` → **Step 4:** verde.
- [ ] **Step 5: Implementar `Install`** — fecha instâncias `MonitorDeBateria` rodando (`Process.GetProcessesByName` + `Kill`, espera até 5 s), copia, atalho no Menu Iniciar (`.lnk` via `WScript.Shell` por `Type.GetTypeFromProgID`), `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` valor `MonitorDeBateria`, `HKCU\...\Uninstall\MonitorDeBateria` (`DisplayName` "Monitor de Bateria", `UninstallString` = `"<Target>" --desinstalar`, `DisplayIcon`, `NoModify`=1, `NoRepair`=1), inicia o `Target`, `MessageBox` "Instalado". `Uninstall`: remove as três chaves/atalho, mata a instância, apaga a pasta com `cmd /c timeout 2 & rmdir /s /q` (o exe não apaga a si mesmo).
- [ ] **Step 6: `Program.Main`** — `--desinstalar` → `Uninstall`; senão, se `!IsInstalledCopy(Application.ExecutablePath)` → `Install` e sai; senão monitor.
- [ ] **Step 7: [M]** Baixar o exe para Downloads, duplo clique → instalado e rodando; de novo → atualiza; "Aplicativos instalados" → desinstalar limpa tudo.
- [ ] **Step 8: Commit** `feat: auto-instalação sem admin`.

### Task 7: README com link (critério 10)

**Files:** Modify `README.md`

- [ ] **Step 1:** Link `https://github.com/<usuario>/<repo>/releases/latest/download/Instalar-Monitor-de-Bateria.exe` (pendência: nome do repo), aviso do SmartScreen ("Mais informações → Executar assim mesmo"), como desinstalar, `test.ps1`/`build.ps1`.
- [ ] **Step 2: Commit** `docs: link de download e uso`.
