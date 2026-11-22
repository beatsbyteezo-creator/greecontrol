# Gree Control

Aplicativo nativo para Windows que controla aparelhos Gree pela rede local. A interface roda em WebView2 dentro de uma janela WinForms; o Edge não abre como navegador.

## Instalação para usuários

Baixe `GreeControl-Setup-x64.exe` na página **Releases** do GitHub e execute. O instalador é por usuário, aplica o ícone do app, instala o WebView2 se necessário, inclui o runtime .NET e Node.js, cria atalhos no Menu Iniciar e oferece início automático junto ao Windows.

Ao abrir, o aplicativo consulta a versão estável mais recente do repositório público. Se encontrar uma versão mais nova, valida o SHA-256 publicado pelo GitHub, instala em modo silencioso e reabre o Gree Control. A instalação não precisa de privilégios de administrador.

## Desenvolvimento

Requisitos: Windows 10/11, .NET SDK 8, Node.js 20+ e Microsoft Edge WebView2 Runtime.

```powershell
dotnet publish .\GreeControl.Native\GreeControl.Native.csproj -c Release -r win-x64 --self-contained false -o .\dist-dev
```

O arquivo `Abrir Gree Control.bat` é um lançador de desenvolvimento local e requer Node.js instalado. A versão distribuída usa runtime próprio e é produzida pelo workflow do GitHub Actions.

## Criar uma versão

Altere `VERSION` para a próxima versão estável (formato `MAJOR.MINOR.PATCH`) e envie a alteração à branch principal. Por exemplo, para publicar a versão `1.0.1`:

```powershell
Set-Content VERSION '1.0.1'
git add VERSION
git commit -m "Release v1.0.1"
git push
```

O workflow `.github/workflows/release-windows.yml` publica o instalador `GreeControl-Setup-x64.exe` como release. Mantenha o repositório público para que o atualizador integrado acesse as releases sem token embutido no aplicativo.

## Estrutura

- `GreeControl.Native`: janela nativa, bandeja, WebView2, ponte e atualização.
- `js`, `css`, `index.html`: interface, controles, assistente e histórico local.
- `gree.cjs`, `servidor.cjs`: descoberta, protocolo local e comandos ao Gree.
- `installer/GreeControl.iss`: configuração do instalador Inno Setup.
- `.github/workflows/release-windows.yml`: build e publicação da release.
- `gree-control.ico`: ícone do aplicativo e do instalador.

## Conexão e privacidade

Os comandos para o ar-condicionado usam a rede local e dependem do aparelho estar acessível no Wi-Fi. O assistente consulta clima externo via Open-Meteo. O atualizador contata a API de Releases do GitHub. Não coloque tokens, PINs ou arquivos locais de diagnóstico no repositório.

O instalador e o executável ainda não têm assinatura digital de editor; o Windows pode exibir o aviso do SmartScreen na primeira execução. Remover esse aviso exige um certificado de assinatura de código.
