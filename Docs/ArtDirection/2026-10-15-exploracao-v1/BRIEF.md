# Rail — exploração de direção artística, rodada 1

Data: 15/10/2026. Briefing reiniciado com base exclusivamente nesta conversa. Propostas em avaliação; nenhuma direção aprovada.

## Intenção do usuário

Infinite runner de velocidade e adrenalina, hipnótico e prazeroso. Personagem abstrato. Visual flat, atual e abstrato; futurismo e fantasia abstrata são territórios possíveis. Bloom e efeitos visuais têm forte interesse. Preservar gameplay e câmera existentes.

## Estrutura que a arte deve preservar

Lanes retas em linhas e colunas, com segmentos consecutivos e lacunas. No final de cada segmento, apenas uma direção de conexão selecionada: esquerda, centro ou direita, em relação à próxima linha. A disposição pode conter células vazias. O briefing indica início com três colunas e expansão até sete. Coins, power-ups e hazards precisam continuar distintos e legíveis.

## Inspeção do projeto

Análise estática, sem abrir uma sessão de jogo. Não foram alterados código, prefabs, cenas, materiais nem configurações.

- A câmera usa perspectiva e o preset lido registra tilt 50°, FOV 50° e look-ahead 0,3. Não são propostas mudanças nesses parâmetros.
- SwitchController possui os estados Left, Middle e Right; SwitchVisual apresenta uma separação entre estado e animação visual. A existência desses componentes não comprova que todas as instâncias da cena os utilizem.
- RailGenConfig_Default.asset registra laneSpacing 3, trackLength 12 e rowGap 2. Esses valores orientam a proporção das peças, sem edição.
- O preset também registra globalMaxLanes 9. Para este briefing artístico, prevalece o pedido atual de até sete; a divergência foi registrada, sem alterar geração ou dificuldade.
- O projeto utiliza URP. A câmera encontrada em Assets/Scenes/RailSwitchMVP.unity registra m_RenderPostProcessing 0. Há perfis de volume com bloom, mas sua existência não comprova bloom ativo no jogo. Implementação e desempenho precisam de validação no aparelho-alvo.
- Há alterações locais anteriores à análise, preservadas. Documentos artísticos anteriores não foram usados como fonte de direção.

## Três estudos

### 01 — Aurora

![Aurora](01-aurora.png)

Fundo azul profundo, pistas azul-petróleo, conexão menta, coins âmbar. Personagem em forma de pérola com crescente e rastro curto. A fantasia vem de campos de luz e grandes curvas distantes. Potencial: ritmo hipnótico e identidade luminosa. Próxima investigação: reduzir pilares e luminosidade do fundo para destacar o conector.

### 02 — Relíquia

![Relíquia](02-reliquia.png)

Pistas marfim, vazio ameixa e conexões douradas. Personagem como pequeno fuso facetado. Anéis monumentais abstratos nas laterais. Potencial: fantasia abstrata com contraste quente/frio. Próxima investigação: simplificar o cenário e evitar que a atmosfera pareça lenta ou que a coleta se confunda com a energia das conexões.

### 03 — Prisma

![Prisma](03-prisma.png)

Pistas ultramarinas, conexão lima, personagem coral e grandes planos angulares. Potencial: impacto gráfico e adrenalina. Próxima investigação: reduzir a presença dos elementos laterais e distinguir claramente personagem e hazards por forma, além de cor.

## Limites das imagens

Imagens geradas pela ferramenta integrada imagegen, usando a primeira captura enviada como referência de câmera e composição. São estudos de atmosfera e materiais, não capturas do Unity nem validação geométrica. As conexões e a perspectiva podem apresentar aproximações. Os três estudos herdaram bastante massa vertical da referência; essa massa não é um requisito aprovado e deve ser explorada na próxima rodada. Interface omitida para permitir comparação das pistas e do mundo.

## Regras propostas para desenvolvimento posterior

- Formas simples e materiais com grandes áreas de cor; textura apenas se tiver uma função clara.
- Núcleo luminoso nítido com halo contido; preservar separação entre peças mesmo com sete colunas.
- Direção selecionada deve ser reconhecida também pela geometria, sem depender só de cor.
- Não iluminar antecipadamente um caminho completo que revele escolhas futuras além da informação já disponível no gameplay.
- Efeitos cosméticos respondem aos eventos existentes; não adicionam mudanças de câmera, tempo, velocidade ou regras.
- Rastro curto no personagem; partículas de coleta rápidas; animação do conector deve refletir o estado sem esconder a direção.
- Decoração longe das áreas de decisão. Percepção de velocidade pode vir da passagem de elementos e do rastro, preservando a câmera.
- O bloom deve valorizar eventos e pontos de atenção, sem nivelar toda a tela em brilho.

## Próxima decisão

Escolher a atmosfera mais atraente e apontar o que deve ser descartado. Depois, refinar uma única direção com formas mais flat, estados visuais do conector e teste de legibilidade com três e sete colunas. Só então propor aplicação em uma cena de validação, preservando lógica e câmera.

Os prompts exatos estão em prompts.json.

