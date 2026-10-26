# Compactador de Filmes

Abra `Compactador.exe` na pasta extraída (neste computador, em `Programa`). Não requer instalação do .NET. A cópia local já contém FFmpeg e FFprobe; no ZIP público eles são obtidos separadamente conforme o README. Mantenha os arquivos juntos ou use **Localizar FFmpeg**.

1. Adicione filmes ou pastas (inclui subpastas).
2. Escolha máxima qualidade, equilibrado ou menor tamanho.
3. Escolha o destino. Os originais são preservados.
4. Selecione um filme e teste um trecho. Use **Abrir original / resultado** para comparar; a tela informa em qual ponto do original começa a amostra.
5. Clique em **Comprimir fila**.

Saída em MKV com vídeo HEVC e cópia das faixas de áudio e legendas, metadados e capítulos compatíveis. Não altera resolução ou taxa de quadros intencionalmente. Alguns aparelhos exigem um reprodutor compatível com HEVC, como VLC. Anexos e faixas de dados não são copiados.

Deixe **Automático (recomendado)** para testar e usar NVIDIA, Intel ou AMD disponíveis, com alternativa pelo processador. Você também pode escolher manualmente. **Compatibilidade** mostra quais testes de codificação passaram e quais falharam. Uma opção indisponível usa o processador; no automático, uma falha durante a compressão reinicia o filme com a próxima opção, até o processador. O andamento e a conclusão mostram qual codificador foi usado. Placas antigas podem não oferecer HEVC. Os perfis não produzem qualidade idêntica entre fabricantes.

O programa verifica a duração e decodifica o vídeo e áudio completos antes de concluir. Essa etapa pode demorar e não mede a qualidade visual. Resultados sem economia são descartados automaticamente. Não há garantia de redução ou de ausência de perdas perceptíveis: compare as amostras, sobretudo em filmes escuros, com granulação ou HDR.

**Parar após o atual** conclui a compressão e a verificação em andamento, depois pausa a fila. Fechar normalmente durante o processamento solicita essa parada. Após interrupção forçada, o filme incompleto reinicia do começo; os já concluídos são mantidos. Arquivos `.partial` ou `.attempt` de interrupções forçadas podem permanecer no destino e não são resultados finais.

Fila e registros ficam em `%LOCALAPPDATA%\CompactadorFilmes`. Retomada recupera a lista, destino e preferências. O programa impede duas instâncias simultâneas para proteger a fila. Ele não transmite seus filmes nem logs.

Para gerar o pacote público: `./scripts/package.ps1 -RunTests`. Leia o README para requisitos, distribuição e limites dos testes.
