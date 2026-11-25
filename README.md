# Gree Control

Aplicativo para Windows que controla aparelhos Gree pela rede local. A interface roda em uma janela nativa com WebView2; o Edge não abre como navegador.

## Instalação no Windows

Baixe `GreeControl-Setup-x64.exe` na página **Releases** do GitHub e execute. O instalador é por usuário, aplica o ícone do app, instala o WebView2 se necessário, inclui o runtime .NET, cria atalhos no Menu Iniciar e não configura inicialização automática com o Windows.

Ao abrir, o aplicativo consulta a versão estável mais recente do repositório público. Se encontrar uma versão mais nova, valida o SHA-256 publicado pelo GitHub, instala em modo silencioso e reabre o Gree Control.

O painel abre acima do relógio do Windows. Use o ícone da bandeja para abrir o painel e o botão de fechar para ocultá-lo. Na bandeja, o aplicativo mantém o acesso pelo iPhone e as rotinas. Para encerrar, use **Sair** no menu do ícone da bandeja. As sugestões do Assistente aparecem na aba **Assistente**.

## Usar como app no iPhone

O Gree Control também serve a interface pela rede local. Deixe o app aberto no Windows e, na tela **Configurações**, use o endereço exibido em “Gree Control no iPhone”. O iPhone e o PC precisam estar na mesma rede Wi-Fi; redes de convidados podem bloquear a comunicação entre aparelhos.

No Safari, abra esse endereço, informe o código de pareamento mostrado no Windows e escolha **Compartilhar → Adicionar à Tela de Início**. Ative **Abrir como App da Web** se a opção aparecer. O pareamento fica salvo no iPhone. O app do Windows precisa permanecer aberto na bandeja para que o controle funcione. Se o roteador mudar o IP do PC, consulte o novo endereço nas configurações do Windows.

A interface usa a porta local `47821` por padrão. Você pode substituí-la com a variável de ambiente `GREE_PORT` antes de iniciar o servidor. O endereço local usa HTTP e deve ficar restrito à sua rede doméstica.

## Desenvolvimento

Requisitos: Windows 10/11, .NET SDK 8, Node.js 20+ e Microsoft Edge WebView2 Runtime.

```powershell
dotnet publish .\GreeControl.Native\GreeControl.Native.csproj -c Release -r win-x64 --self-contained false -o .\dist-dev
```

O arquivo `Abrir Gree Control.bat` é um lançador de desenvolvimento local e requer Node.js instalado. A versão distribuída usa runtime próprio e é produzida pelo workflow do GitHub Actions.

## Criar uma versão

Altere `VERSION` para a próxima versão estável (formato `MAJOR.MINOR.PATCH`) e envie a alteração à branch principal. O workflow `.github/workflows/release-windows.yml` publica o instalador `GreeControl-Setup-x64.exe` como release. Mantenha o repositório público para que o atualizador integrado acesse as releases sem token embutido no aplicativo.

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