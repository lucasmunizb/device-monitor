# Monitor de Bateria

Mostra na bandeja do Windows a bateria do mouse DELUX M900 Pro e do headset HyperX Cloud II Core Wireless.

## Instalar

[Baixar Instalar-Monitor-de-Bateria.exe](https://github.com/lucasmunizb/device-monitor/releases/latest/download/Instalar-Monitor-de-Bateria.exe) e dar duplo clique. Não pede administrador.

O exe não é assinado: no primeiro clique o Windows mostra "O Windows protegeu o computador". Clique em **Mais informações → Executar assim mesmo**.

A instalação copia o programa para `%LOCALAPPDATA%\MonitorDeBateria`, cria o atalho no Menu Iniciar e liga "Iniciar com o Windows". Para atualizar, baixe e clique de novo.

Clique direito no ícone: **Atualizar**, **Iniciar com o Windows**, **Sair**. Número vermelho = 20% ou menos; raio = carregando.

## Desinstalar

Configurações → Aplicativos → Aplicativos instalados → **Monitor de Bateria** → Desinstalar.

## Desenvolver

Precisa só do Windows (usa o `csc.exe` do .NET Framework 4, que já vem instalado).

```powershell
.\test.ps1    # testes dos parsers
.\build.ps1   # testes + gera dist\Instalar-Monitor-de-Bateria.exe
```

No WSL: `powershell.exe -ExecutionPolicy Bypass -File "$(wslpath -w test.ps1)"`.

Para publicar: criar uma Release no GitHub e anexar `dist\Instalar-Monitor-de-Bateria.exe` com esse nome exato.
