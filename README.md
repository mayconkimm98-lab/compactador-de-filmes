# Compactador de Filmes para Windows

Aplicativo nativo para comprimir vídeos em fila, preservar os originais e conferir duração e integridade antes de concluir. Interface em português. Versão 0.2.0.

[Baixar versão para Windows x64](https://github.com/mayconkimm98-lab/compactador-de-filmes/releases/tag/v0.2.0) · [Ver compilações e testes](https://github.com/mayconkimm98-lab/compactador-de-filmes/actions)

## Baixar e usar

1. Baixe o ZIP **CompactadorFilmes-0.2.0-win-x64.zip** disponibilizado pelo mantenedor em Releases (ou nos artefatos da ação de compilação). Extraia a pasta inteira.
2. Obtenha FFmpeg para Windows pelo [site oficial](https://ffmpeg.org/download.html#build-windows). Extraia a distribuição e copie **ffmpeg.exe e ffprobe.exe** da pasta `bin` para a mesma pasta do Compactador.exe. Também é possível usar **Localizar FFmpeg** no programa para selecionar essa pasta, ou uma instalação disponível no PATH.
3. Abra **Compactador.exe**. Não é necessário instalar o .NET. O aplicativo não baixa ferramentas ou drivers automaticamente.

O repositório contém código-fonte; o ZIP para usuários é gerado pela ação de compilação. Os dois executáveis FFmpeg são obtidos separadamente para evitar redistribuição sem o respectivo código-fonte e licenças. Não envie a pasta local Programa ao GitHub.

## Compatibilidade

Windows 10/11 **x64** com drivers do fabricante instalados e FFmpeg com os codificadores desejados. ARM64 nativo, Windows 32 bits, Linux e macOS não são alvos deste pacote.

| Modo | Recurso necessário | Quando não funciona |
|---|---|---|
| Automático (padrão) | Nenhuma marca específica | Tenta as opções aprovadas e termina no processador |
| NVIDIA NVENC | GPU e driver capazes de codificar HEVC | Usa processador se indisponível; em falha reinicia o vídeo no processador |
| Intel Quick Sync | GPU Intel integrada ou dedicada com HEVC e driver ativo | Usa processador se indisponível; em falha reinicia o vídeo no processador |
| AMD AMF | GPU AMD com codificação HEVC e driver compatível | Usa processador se indisponível; em falha reinicia o vídeo no processador |
| Processador | FFmpeg com libx265 | Funciona sem aceleração gráfica; pode ser mais lento |

Uma placa de uma marca suportada pode ser antiga demais, estar desativada ou não ter o driver necessário. O programa testa frames reais ao abrir, com limite de tempo por teste, em vez de inferir suporte pelo nome da placa. O botão **Compatibilidade** mostra o resultado e o motivo de indisponibilidade.

No automático, a ordem é NVIDIA → Intel → AMD → processador, considerando apenas os testes aprovados. Essa ordem é uma preferência fixa, não um benchmark de velocidade ou qualidade. Você pode escolher manualmente. A escolha é salva, mas o hardware é testado novamente em cada inicialização.

O teste inicial usa HEVC de 8 bits em baixa resolução. Não certifica todo formato, resolução ou HDR. Se a aceleração falhar no arquivo real, a tentativa parcial é descartada e o item reinicia com a alternativa. Uma falha também no processador gera erro na fila e registro local; os próximos itens continuam. Perfis ajustam a qualidade com controles específicos de cada codificador; resultados entre marcas não são visualmente idênticos.

## Recursos e limites

- Adicionar arquivos ou pastas com subpastas; análise de codec, duração, tamanho e taxa de dados.
- Perfis máxima qualidade, equilibrado e menor tamanho.
- Amostra de até 30 segundos, estimativa aproximada de economia e abertura do original/resultado no reprodutor padrão.
- Saída MKV com vídeo HEVC, cópia de áudio, legendas, capítulos e metadados compatíveis. Anexos e dados não são copiados.
- Progresso, previsão por filme e parada após o atual.
- Fila persistente e retomada: um item interrompido reinicia do começo, sem recodificar os já concluídos.
- Verificação de duração e decodificação completa de vídeo/áudio. Isso detecta erros técnicos, não garante qualidade perceptiva ou fidelidade de metadados HDR.
- Resultados maiores ou iguais ao original são descartados. Não há promessa de porcentagem de economia.

Arquivos, fila e diagnósticos permanecem no computador. O aplicativo não contém telemetria ou upload. A fila e logs em `%LOCALAPPDATA%\CompactadorFilmes` podem incluir caminhos dos seus arquivos; revise-os antes de anexar a uma issue.

## Compilar e verificar

Requer SDK .NET 10 em Windows, além de FFmpeg/FFprobe no PATH para testes.

```powershell
dotnet build Compactador -c Release
./scripts/package.ps1 -RunTests
```

O script publica em pasta nova, inclui o runtime do .NET e gera o ZIP e SHA-256 em `artifacts`. Não inclui ferramentas FFmpeg, filmes, filas, logs ou caminhos pessoais. A ação GitHub em `.github/workflows/build.yml` compila, instala FFmpeg somente para os testes e entrega os artefatos sem publicar uma Release automaticamente.

O autoteste usa vídeo sintético com áudio: prova codificação nos recursos encontrados, integridade, amostra, retorno ao processador após falha simulada do driver, execução com GPU indisponível, proteção de arquivo existente, limpeza da tentativa e serialização/migração da fila.

Validação local: NVIDIA RTX 3050, Intel Quick Sync disponível neste notebook e CPU, com duração e decodificação completas aprovadas no vídeo sintético. AMD foi corretamente indicada como indisponível e ainda precisa de validação em uma máquina AMD antes de anunciar suporte homologado. Testes em runner do GitHub normalmente comprovam CPU, não hardware de todas as marcas.

Consulte [LEIA-ME.md](LEIA-ME.md) e [avisos de terceiros](THIRD-PARTY-NOTICES.md).
