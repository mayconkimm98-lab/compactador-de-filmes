# Componentes de terceiros

O aplicativo chama FFmpeg e FFprobe como processos separados. Créditos: desenvolvedores do [FFmpeg](https://ffmpeg.org/). O pacote público gerado por `scripts/package.ps1` **não contém esses executáveis**. Obtenha-os de uma distribuição indicada em https://ffmpeg.org/download.html e mantenha suas licenças.

A licença efetiva depende da compilação do FFmpeg e das bibliotecas habilitadas. Consulte `ffmpeg -version`, `ffmpeg -L` e https://ffmpeg.org/legal.html. O FFmpeg presente na pasta local Programa é uma compilação Gyan com GPL e version3 habilitados; essa pasta está excluída do Git. Não a confunda com o pacote público gerado pelo script.

Quem decidir criar uma distribuição diferente que inclua FFmpeg deve incluir as licenças e fornecer o código-fonte correspondente à compilação distribuída, conforme os termos aplicáveis. O processo de empacotamento deste repositório evita redistribuir esses binários.

Os pacotes do .NET são incluídos pelo processo oficial de publicação self-contained. Seus avisos de terceiros gerados pelo SDK são preservados no pacote.
