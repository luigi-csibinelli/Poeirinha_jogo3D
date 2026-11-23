# Poeirinha — Game Design Document

> Jogo 3D hiper-casual feito em Unity para o **Projeto 3D – Hiper-Casual** (Insper, 2026.2).
> **Entrega:** 25/11/2026 — Build Windows + repositório GitHub + página na [jam do itch.io](https://itch.io/jam/projeto-jogo-3d-insper-20262).
> Este é um **documento vivo**: tudo que mudou ao longo do desenvolvimento está registrado na seção 14.

| | |
|---|---|
| **Gênero** | Arcade de tempo, hiper-casual 3D |
| **Plataforma** | Windows (teclado + mouse) |
| **Engine** | Unity 6.3 LTS (6000.3.25f1), URP, Input System, AI Navigation |
| **Duração de uma partida** | até 3 minutos |
| **Escopo** | 1 fase (apartamento) bem acabada, dificuldade fixa |
| **Equipe** | _(preencher com os nomes dos integrantes)_ |

![Vista geral do apartamento](Docs/Screenshots/00_vista_geral.png)

---

## 1. Conceito

Você é a **Poeirinha**, um **robô aspirador**, e precisa deixar o apartamento limpo **antes do dono voltar**.
A bateria acaba, o **gato** corre atrás de você e a **escada não tem grade**. Aspire a sujeira (inclusive a que está
escondida **embaixo dos móveis**), volte à base para recarregar e não caia da escada.

**A graça do jogo é a escala:** o robô tem 9 cm de altura e os móveis são gigantes. Passar por baixo do sofá, da cama
e das mesas é ao mesmo tempo a forma de achar sujeira escondida e de fugir do gato. É aí que o 3D vira mecânica.

### Pilares
1. **Gerenciar a bateria.** Ela é o relógio de verdade: é preciso planejar as voltas até a base.
2. **Usar o espaço 3D.** Passar por baixo dos móveis para limpar e para se esconder.
3. **Risco × recompensa.** Há sujeira perto da borda da escada, e o turbo é rápido mas gasta o dobro.

---

## 2. Ciclo principal

```
Aspirar sujeira → bateria cai → voltar à base → recarregar → aspirar o resto → VITÓRIA (90% limpo)
       ↑                ↘ o gato vê você → fugir no turbo ou se esconder embaixo de um móvel
       └──────────────── ↘ bateria zera fora da base / cai da escada / tempo acaba → DERROTA
```

Uma carga cheia dura ~66 s andando. Em 3 minutos o jogador volta à base **2 ou 3 vezes**: essa é a tensão do jogo.

---

## 3. Controles

| Entrada | Ação |
|---|---|
| **W / S** ou **↑ / ↓** | Andar para frente / ré |
| **A / D** ou **← / →** | Girar no próprio eixo |
| **Shift esquerdo** (segurar) | **Turbo**: +60% de velocidade, gasta 2× bateria |
| **Mouse** | Girar a câmera em volta do robô |
| **Roda do mouse** | Zoom da câmera |
| **E** (várias vezes) | Soltar-se de uma meia enroscada |
| **Esc** | Pausar / despausar |

- **Controle "tanque":** frente e ré são relativas ao robô, não à câmera. Combina com o tema (é assim que um robô
  aspirador anda) e evita confusão quando o jogador gira a câmera com o mouse.
- **Aceleração suave** (4 m/s²) e frenagem um pouco mais forte (6 m/s²): o robô não arranca nem para seco.
- Menus navegáveis por **teclado** (setas + Enter) e por **mouse**.
- Sensibilidade do giro e do mouse ajustáveis na tela de **Opções** (seção 9).

---

## 4. Câmera

Câmera em **terceira pessoa, atrás e acima do robô**, baixa o bastante para os móveis parecerem enormes.

| Parâmetro | Valor |
|---|---|
| Posição padrão | 1,64 m de distância, inclinada 37,5° para baixo (≈ 1,0 m acima e 1,3 m atrás) |
| Ponto de mira | robô + 0,5 m à frente |
| Suavização | `SmoothDamp` (0,15 s) na posição; o giro acompanha o robô com leve atraso |
| Mouse | gira em volta do robô (horizontal e vertical, 8°–70°); roda = zoom (0,8–2,6 m) |
| Recentralizar | se ligado nas Opções, volta para trás do robô 1,5 s depois de o mouse parar, quando o robô está andando |

**Garantias:**
1. **Nunca atravessa paredes:** um `SphereCast` do robô até a câmera aproxima a câmera quando bate numa parede ou
   num móvel alto (layer `Wall`).
2. **Oclusão:** sofá, cama, mesas e cadeiras (layer `Occluder`) ficam **semitransparentes (30%)** quando ficam entre
   a câmera e o robô, por exemplo com o robô embaixo do sofá.
3. **Sem tremedeira:** a câmera atualiza em `LateUpdate` e o Rigidbody do robô usa `Interpolate`.

---

## 5. O robô (jogador)

| Item | Valor |
|---|---|
| Tamanho | 0,35 m de diâmetro × 0,09 m de altura (escala real: 1 unidade = 1 m) |
| Velocidade | 1,3 m/s (turbo: 2,1 m/s) |
| Giro | 120°/s × sensibilidade (0,4–1,6) |
| Collider | cilindro convexo (`MeshCollider`) com atrito zero: desliza nas quinas em vez de prender |
| Rigidbody | massa 2, `Interpolate`, colisão `Continuous`, rotação X/Z congelada (não tomba) |

- **LED de bateria** no topo: verde → amarelo → **vermelho piscando** (fraca) → **azul** (carregando).
- **Escovas laterais** girando, mais rápido quando o robô anda.
- **Sensor de borda (cliff sensor):** um raio para baixo, 0,25 m à frente do robô. Sem chão a menos de 0,2 m →
  **bip + aviso "Cuidado: escada!"**. O sensor só avisa e **não freia**: evitar a queda é responsabilidade do jogador.

### Bateria
| Situação | Efeito |
|---|---|
| Andando | −1,5 por segundo |
| Parado | −0,5 por segundo |
| Turbo | consumo ×2 |
| Ser pego pelo gato | −10 instantâneo (com 1 s de invulnerabilidade) |
| Na base de recarga | +25 por segundo (até 100) |

Começa em 100. **Abaixo de 25%** a barra fica vermelha, o LED pisca, toca um bip a cada 2 s e aparece
"Bateria fraca! Volte à base". **Chegou a 0 fora da base = derrota.**

A **base de recarga** fica num ponto central (na parede entre a sala e o quarto), para que todo cômodo seja alcançável
com uma carga. Ao entrar nela: som de "plug", LED azul, luz e partículas.

---

## 6. Sujeira e limpeza

- **60 manchas** (poeira e migalhas), **15 delas embaixo de móveis**: sofá (5), cama (5), mesa de centro (2),
  mesa de jantar (2) e escrivaninha (1).
- Ao encostar, a mancha some em 0,2 s, com partículas de poeira e som de sucção.
- **Vitória: 90% limpo** (54 de 60). Mensagens no HUD em 50% ("Metade!") e 75% ("Quase lá!").
- Duas manchas ficam **perto da borda da escada** (risco × recompensa).
- O posicionamento é sorteado por uma ferramenta de Editor que **garante que o robô cabe** em cada ponto
  (há um teste automático que confere isso).

---

## 7. Obstáculos

### Gato
- **Patrulha** 7 pontos pela casa usando **NavMesh** (1,0 m/s), com pausas de 1–3 s; às vezes **deita e dorme** por 5 s ("zZ").
- **Perseguição:** se **vê** o robô (a menos de 3,2 m e sem parede no meio), para por 0,6 s com um **"!"** sobre a
  cabeça, mia e **corre atrás** dele a **1,6 m/s**: mais rápido que o robô andando e mais lento que o turbo.
- **Como escapar:**
  - fugir no **turbo**;
  - **se esconder embaixo do sofá ou da cama**: o gato não cabe ali (a NavMesh dele não passa por baixo) e **desiste**;
  - se afastar mais de 5 m. Depois de 8 s de perseguição ele também cansa.
- **Se pegar o robô:** empurra o robô para longe, tira **10 de bateria** e se dá por satisfeito (5 s sem perseguir).
- O gato dormindo não persegue ninguém.

### Meias (extra)
Três meias espalhadas pela casa, longe da base. Passar por cima **prende o robô** até o jogador apertar **E cinco
vezes**; o HUD mostra quantos apertos faltam. Depois de solto, a mesma meia não prende de novo até o robô sair dela.

### Escada
Escada **sem grade** no canto da sala, com uma faixa amarela no chão marcando a borda. Cair um degrau já é derrota:
a câmera acompanha a queda por ~1 s antes da tela de fim.

---

## 8. Fase: o apartamento (`Level01`)

```
┌──────────────┬───────────────┐
│   QUARTO     │    COZINHA    │
│ cama, guarda-│ bancada, gela-│
│ roupa, escri-│ deira, mesa + │
│ vaninha      │ 4 cadeiras    │
├─── porta ────┴──── vão ──────┤
│            SALA     [BASE]   │
│ estante, sofá, mesa de       │
│ centro, rack + TV, abajur    │
│                      ┌───────┤
│                      │ESCADA↓│  ← sem grade
└──────────────────────┴───────┘
              10 m × 8 m
```

| Móvel | Vão embaixo | O robô passa? | O gato passa? |
|---|---|---|---|
| Sofá | 0,12 m | ✅ | ❌ (esconderijo) |
| Cama | 0,15 m | ✅ | ❌ (esconderijo) |
| Mesa de centro | 0,36 m | ✅ | ✅ |
| Mesa de jantar / cadeiras | 0,73 m / entre os pés | ✅ | ✅ |
| Escrivaninha | 0,71 m | ✅ | ✅ |
| Rack, estante, guarda-roupa, bancada, geladeira | — | ❌ (sólidos) | ❌ |

- Paredes com colliders simples e **sem frestas**; chão com um único collider (sem emendas que prendam o robô).
- Tapetes e a placa da base **não têm collider**: um degrau de milímetros já travaria o robô.

---

## 9. Estados do jogo e telas

```
MainMenu ──Jogar──▶ Playing ⇄ Paused (Esc)
   ▲                  │
   │                  ├─▶ Victory (90% limpo)
   │                  └─▶ Defeat (bateria / escada / tempo)
   └──── Menu ◀───────┘        └─ Jogar de novo / Tentar de novo → recarrega Level01
```

### Robustez (critério 4)
- Um único `GameManager` com `enum GameState { Playing, Paused, Victory, Defeat }` e o evento `OnStateChanged`.
- **Toda condição de fim só vale se o estado for `Playing`:** é impossível ganhar e perder no mesmo frame ou
  disparar o fim duas vezes.
- **Pausa** = `Time.timeScale = 0`. Qualquer troca de cena volta `timeScale` para 1, então nunca se começa "congelado".
- **Reiniciar** = recarregar a cena (nada de "resetar na mão").
- A derrota sempre mostra **o motivo**: "A bateria acabou!", "Caiu da escada!" ou "O dono chegou e a casa ainda está suja!".
- No fim, o robô e o gato param, o input é ignorado e o cursor volta.
- **Sem dead ends:** a base é alcançável de qualquer lugar; a meia sempre solta com E; o gato desiste sozinho;
  o tempo sempre termina a partida.

### Telas
| Tela | Conteúdo |
|---|---|
| **Menu principal** | Título, **Jogar**, **Como jogar**, **Opções**, **Sair** |
| **Como jogar** | Objetivo, controles, base, escada, gato, esconderijos |
| **Opções** (menu e pausa) | Sensibilidade do giro (A/D), sensibilidade do mouse, volume, inverter eixo Y do mouse, recentralizar câmera, botão **Padrão**. Salvo entre partidas (`PlayerPrefs`) |
| **HUD** | Topo-esquerda: % limpo (com marca da meta de 90%). Topo-direita: "O dono volta em" (timer). Base-esquerda: bateria. Centro: avisos (meia, escada, gato, bateria fraca) e mensagens rápidas |
| **Pausa** | **Continuar**, **Opções**, **Reiniciar**, **Menu** |
| **Vitória** | "Casa limpa!", tempo restante, % limpo, **estrelas** (3★ se sobrou mais de 60 s, 2★ mais de 30 s, 1★ caso contrário), **Jogar de novo**, **Menu** |
| **Derrota** | Motivo, % que chegou a limpar, **Tentar de novo**, **Menu** |

O timer fica **vermelho nos últimos 30 s**, quando toca um som de chaves e porta ("O dono está chegando!").

![Menu principal](Docs/Screenshots/08_menu_ui.png)

---

## 10. Arte e áudio

### Visual (critério 3)
- **Low-poly com cores suaves**, todo em **escala real** (1 unidade = 1 m).
- Modelos feitos com **primitivas do Unity** combinadas (robô, gato, móveis).
- **Texturas geradas por código** (madeira em tábuas, azulejo, tapete, poeira, migalhas) e **materiais URP Lit**
  configurados (madeira fosca, metal brilhante, LEDs e janelas com emissão).
- **Iluminação:** uma luz direcional (sol) com **sombras suaves**, mais abajur, luminária da cozinha, abajur do quarto
  e a luz da base. A **sombra do robô** ajuda a perceber profundidade.
- **Pós-processamento:** tonemapping, bloom leve nos LEDs e vinheta.

### Áudio
Todos os sons são **sintetizados por código** (`ProceduralAudio.cs`): nenhum arquivo de terceiros.

| Som | Quando |
|---|---|
| Motor (loop) | Sempre; o **pitch acompanha a velocidade** |
| Sucção | Ao aspirar sujeira |
| Bip | Bateria fraca (a cada 2 s) e borda da escada (rápido) |
| Plug | Ao entrar na base |
| Miado | Quando o gato vê o robô e quando pega |
| Chaves + porta | Últimos 30 s |
| Jingle | Vitória / derrota |
| Música | Trilha leve em loop (continua tocando na pausa) |

| Início na base | Embaixo do sofá | Cozinha |
|---|---|---|
| ![](Docs/Screenshots/01_inicio_base.png) | ![](Docs/Screenshots/02_embaixo_sofa.png) | ![](Docs/Screenshots/03_cozinha.png) |

---

## 11. Arquitetura técnica

```
Assets/_Project/
├── Scenes/      MainMenu, Level01 (+ NavMesh e pós-processamento)
├── Scripts/
│   ├── Core/      GameManager, GameState, SceneLoader, GameSettings
│   ├── Player/    RobotController, BatterySystem, CliffSensor, RobotVisuals
│   ├── Gameplay/  DirtSpot, CleaningManager, ChargingDock, FallZone, SockTrap, LevelTimer
│   ├── AI/        CatAI
│   ├── Camera/    CameraFollow, OcclusionFader, FadeableObject, MenuCameraOrbit, Billboard
│   ├── UI/        HUDController, PauseMenu, EndScreen, MainMenu, OptionsMenu
│   └── Audio/     AudioManager, ProceduralAudio
├── Editor/      PoeirinhaBuilder (Tools > Poeirinha), BuilderUtils, TextureGen, ScreenshotTool
├── Tests/PlayMode/  Level01Tests (22 testes automáticos)
└── Prefabs/, Materials/, Art/
```

**Princípios:**
- **Eventos C#** (`event Action<...>`) ligam os sistemas: a UI e o áudio **só escutam**; a lógica não conhece a UI.
- Todos os números de ajuste (velocidades, consumo, tempos, distâncias do gato) ficam em `[SerializeField]`, para
  ajustar no Inspector.
- Física em `FixedUpdate`, câmera em `LateUpdate`, input em `Update`.
- **Cenas geradas por código:** `Tools > Poeirinha > Montar tudo` recria cenas, prefabs, materiais e a NavMesh.
  Isso deixa o projeto reproduzível. Em troca, ajustes feitos à mão na cena se perdem se o script rodar de novo.

| Layer | Uso |
|---|---|
| `Player` | Robô |
| `Wall` | Paredes e móveis altos: a câmera colide e eles bloqueiam a visão do gato |
| `Occluder` | Móveis com vão: ficam transparentes quando tapam o robô |
| `Cat` | Gato |

---

## 12. Como cada critério de avaliação é atendido

| Critério | Como o jogo atende |
|---|---|
| **1. Controles, câmera e sensação 3D** | Controle tanque com aceleração suave e sensibilidade ajustável; câmera em 3ª pessoa suavizada, com mouse e zoom, que não atravessa paredes; oclusão dos móveis; escala gigante dos móveis; passar por baixo deles é mecânica (sujeira e esconderijo); sensor de borda e sombra do robô dão noção de profundidade |
| **2. Desempenho e colisões** | Collider redondo com atrito zero (não prende em quinas); `Interpolate` + `Continuous` (não treme nem atravessa); chão num collider só, paredes sem frestas, tapetes sem collider; cena leve (primitivas + iluminação em tempo real) |
| **3. Modelos, materiais e texturas** | Paleta coerente, escala real, materiais URP configurados, texturas próprias, iluminação com sombras, LEDs com emissão e pós-processamento |
| **4. Estados e erros** | Máquina de estados única com guardas contra fim duplo; motivo de derrota sempre visível; reinício por recarga de cena; pausa robusta; sem dead ends; 22 testes automáticos cobrindo vitória, as 3 derrotas, pausa, reiniciar 10× e voltar ao menu |

---

## 13. Qualidade (QA)

### Testes automáticos (`Window > General > Test Runner > PlayMode`)
22 testes, todos passando: início da partida, movimento e giro, turbo contra parede, deslizar na quina, bateria e
base, derrota por bateria (e bloqueio de vitória depois), vitória por limpeza, espaço para o robô em todas as
manchas, queda na escada com aviso do sensor, tempo esgotado, pausa, gato empurrando e tirando bateria uma vez só,
câmera fora das paredes em 6 situações, sofá transparente com o robô embaixo, meia, reiniciar 10×, menu e volta,
patrulha do gato, perseguição, esconderijo embaixo do sofá, opção de sensibilidade, câmera pelo mouse e cobertura da NavMesh.

### Checklist manual (rodar **na build**)
- [ ] Robô não prende em nenhuma quina, pé de móvel ou batente
- [ ] Robô não atravessa paredes nem móveis no turbo
- [ ] Câmera nunca fica dentro de parede; robô sempre visível
- [ ] Vencer, perder pelos 3 motivos e reiniciar: 10× seguidas sem bug
- [ ] Pausar e despausar em vários momentos, inclusive com as Opções abertas
- [ ] Voltar ao menu e jogar de novo: bateria, timer e % zerados
- [ ] Opções salvas depois de fechar e abrir o jogo
- [ ] Botão Sair fecha o jogo
- [ ] ≥ 60 FPS estáveis num notebook comum; console sem erros

---

## 14. Histórico de mudanças (documento vivo)

| Data | Mudança | Motivo |
|---|---|---|
| 17/11/2026 | Versão inicial do conceito: robô aspirador, bateria, gato, escada, 1 fase | Proposta do projeto |
| 17/11/2026 | Collider do robô passou de esfera/cápsula para **cilindro convexo** | Esfera/cápsula de 0,35 m não cabe nos 0,09 m de altura (o robô não passaria embaixo do sofá) |
| 17/11/2026 | Degraus com espelho de **0,25 m** (em vez de ~0,18 m) | Com 0,18 m o sensor de borda (0,2 m) não detectava a escada |
| 17/11/2026 | Tapetes e placa da base **sem collider** | Degraus de milímetros travavam o robô |
| 17/11/2026 | Visual com **primitivas + texturas geradas por código** e **áudio sintetizado**, em vez de packs Kenney | Zero dependência de licenças; pode ser trocado depois |
| 17/11/2026 | Cenas montadas por **script de Editor** (`Tools > Poeirinha`) + **testes automáticos** | Reprodutibilidade e QA rápido |
| 17/11/2026 | 1º playtest: robô **mais lento** (2,0 → 1,3 m/s; giro 180 → 120°/s) | Estava rápido e sensível demais |
| 17/11/2026 | Tela de **Opções** (sensibilidade do giro e do mouse, volume, inverter Y, recentralizar) | Sensibilidade alta no playtest; cada jogador ajusta a sua |
| 17/11/2026 | **Câmera controlada pelo mouse** (órbita + zoom) | Pedido do playtest: olhar em volta sem girar o robô |
| 17/11/2026 | **Gato passou a perseguir** o robô; sofá e cama viraram **esconderijos** | Antes o gato só reagia; perseguir dá tensão e usa melhor o 3D |
| 17/11/2026 | Correção: Rigidbody do gato sem `Interpolate`; NavMesh com célula de 5 cm | O gato andava devagar/travava; a NavMesh era grossa para um agente pequeno |

---

## 15. Créditos

- **Programação, modelos, texturas e sons:** a equipe (modelos com primitivas do Unity; texturas e sons gerados por código).
- **Fonte:** Liberation Sans (SIL Open Font License), incluída no TextMesh Pro.
- **Engine e pacotes:** Unity 6.3 LTS, Universal Render Pipeline, Input System, AI Navigation, TextMesh Pro.
- Desenvolvido com apoio do Claude Code (Anthropic) como assistente de programação.
