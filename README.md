# Gree Control

Aplicativo nativo para Windows que controla aparelhos Gree pela rede local. A interface roda em WebView2 dentro de uma janela WinForms; o Edge n�o abre como navegador.

## Instala��o para usu�rios

Baixe `GreeControl-Setup-x64.exe` na p�gina **Releases** do GitHub e execute. O instalador � por usu�rio, aplica o �cone do app, instala o WebView2 se necess�rio, inclui o runtime .NET e Node.js, cria atalhos no Menu Iniciar e n�o configura inicializa��o autom�tica com o Windows.

Ao abrir, o aplicativo consulta a vers�o est�vel mais recente do reposit�rio p�blico. Se encontrar uma vers�o mais nova, valida o SHA-256 publicado pelo GitHub, instala em modo silencioso e reabre o Gree Control. A instala��o n�o precisa de privil�gios de administrador.

## Desenvolvimento

Requisitos: Windows 10/11, .NET SDK 8, Node.js 20+ e Microsoft Edge WebView2 Runtime.

```powershell
dotnet publish .\GreeControl.Native\GreeControl.Native.csproj -c Release -r win-x64 --self-contained false -o .\dist-dev
```

O arquivo `Abrir Gree Control.bat` � um lan�ador de desenvolvimento local e requer Node.js instalado. A vers�o distribu�da usa runtime pr�prio e � produzida pelo workflow do GitHub Actions.

## Criar uma vers�o

Altere `VERSION` para a pr�xima vers�o est�vel (formato `MAJOR.MINOR.PATCH`) e envie a altera��o � branch principal. Por exemplo, para publicar a vers�o `1.2.2`:

```powershell
Set-Content VERSION '1.2.2'
git add VERSION
git commit -m "Release v1.2.2"
git push
```

O workflow `.github/workflows/release-windows.yml` publica o instalador `GreeControl-Setup-x64.exe` como release. Mantenha o reposit�rio p�blico para que o atualizador integrado acesse as releases sem token embutido no aplicativo.

## Estrutura

- `GreeControl.Native`: janela nativa, bandeja, WebView2, ponte e atualiza��o.
- `js`, `css`, `index.html`: interface, controles, assistente e hist�rico local.
- `gree.cjs`, `servidor.cjs`: descoberta, protocolo local e comandos ao Gree.
- `installer/GreeControl.iss`: configura��o do instalador Inno Setup.
- `.github/workflows/release-windows.yml`: build e publica��o da release.
- `gree-control.ico`: �cone do aplicativo e do instalador.

## Conex�o e privacidade

Os comandos para o ar-condicionado usam a rede local e dependem do aparelho estar acess�vel no Wi-Fi. O assistente consulta clima externo via Open-Meteo. O atualizador contata a API de Releases do GitHub. N�o coloque tokens, PINs ou arquivos locais de diagn�stico no reposit�rio.

O instalador e o execut�vel ainda n�o t�m assinatura digital de editor; o Windows pode exibir o aviso do SmartScreen na primeira execu��o. Remover esse aviso exige um certificado de assinatura de c�digo.

