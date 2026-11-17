# Poeirinha — GDD / Especificação Técnica

> Jogo 3D hiper-casual feito em Unity para o Projeto 3D (Insper, 2026.2).
> **Entrega:** 25/11/2026 — Build Windows + GitHub + itch.io (jam da disciplina).
> Este documento é o GDD vivo do projeto **e** a especificação que o Claude Code segue para implementar.

---

## 0. Pitch

Você é um **robô aspirador** e precisa deixar a casa limpa **antes do dono voltar**.
A bateria acaba, o gato atrapalha e a escada é fatal. Limpe embaixo do sofá, volte à base para recarregar e não caia da escada.

- **Gênero:** hiper-casual 3D, arcade de tempo
- **Plataforma:** Windows (teclado)
- **Duração de uma partida:** ~3 minutos
- **Escopo:** 1 fase bem acabada (apartamento), dificuldade fixa

---

## 1. Escopo e restrições

| Item | Decisão |
|---|---|
| Engine | **Unity 6.3 LTS (6000.3.25f1)**, template **3D (URP)** |
| Input | **Input System** novo (padrão no Unity 6). Se o projeto estiver no Input Manager antigo, adaptar |
| Escala | **1 unidade = 1 metro** (normalizar todos os assets importados) |
| Cenas | `MainMenu` e `Level01` |
| Fases | 1 |
| Assets | Somente gratuitos (CC0/CC-BY), com créditos no README |

### Fora do escopo (não implementar)
Multiplayer, várias fases, save de progresso, upgrades do robô, mobile/controle, cutscenes.

---

## 2. Ciclo principal

```
Aspirar sujeira → bateria cai → voltar à base → recarregar → aspirar o resto → VITÓRIA
                          ↘ bateria zera fora da base / cai da escada / tempo acaba → DERROTA
```

---

## 3. Controles

| Tecla | Ação |
|---|---|
| **W / S** (ou ↑ / ↓) | Andar para frente / ré |
| **A / D** (ou ← / →) | Girar no próprio eixo |
| **Shift esquerdo** (segurar) | **Turbo**: +60% de velocidade, gasta 2× bateria |
| **E** (apertar repetidas vezes) | Soltar-se de uma meia enroscada *(extra)* |
| **Esc** | Pausar / despausar |

- Controle em **"tanque"**: frente e ré relativas ao robô, não à câmera. Combina com o tema e evita confusão de câmera.
- Aceleração e desaceleração suaves (sem parar ou arrancar seco), para o movimento parecer de um robô de verdade.

---

## 4. Câmera

**Terceira pessoa, atrás e acima do robô.** O robô é pequeno e os móveis são enormes: essa escala é a principal fonte de "sensação 3D".

| Parâmetro | Valor inicial (ajustável no Inspector) |
|---|---|
| Offset relativo ao robô | `(0, 1.0, -1.3)` |
| Ponto de mira | Robô + `0.5 m` à frente dele |
| Suavização de posição | `SmoothDamp`, ~0.15 s |
| Suavização de rotação | `Slerp`, acompanha a rotação do robô com leve atraso |

### Requisitos
1. **A câmera nunca atravessa paredes:** usar `SphereCast` do robô até a posição desejada da câmera. Se bater em algo, aproximar a câmera.
2. **Oclusão:** móveis entre a câmera e o robô (ex.: o robô embaixo do sofá) ficam **semitransparentes** (alpha ~0.3) e voltam ao normal quando deixam de bloquear.
   - No URP, isso exige trocar para um material transparente (ou usar um material com alpha controlado por script).
   - Marcar os móveis que podem sumir com uma layer `Occluder`.
3. Sem tremedeira: a câmera atualiza em `LateUpdate` e o Rigidbody do robô usa **Interpolate**.

---

## 5. Robô (jogador)

### Dimensões e física
| Item | Valor |
|---|---|
| Diâmetro / altura | `0.35 m` / `0.09 m` |
| Collider | **SphereCollider** ou **CapsuleCollider** deitado (formato redondo **escorrega nas quinas em vez de prender**) |
| Rigidbody | Massa 2, **Interpolate**, Collision Detection **Continuous**, **congelar rotação X e Z** |
| Physics Material | Atrito **0** (dinâmico e estático), Friction Combine = **Minimum** |
| Movimento | Em `FixedUpdate`, via `Rigidbody.linearVelocity` (Unity 6) ou `MovePosition` + `MoveRotation` |
| Velocidade | `2.0 m/s` (turbo: `3.2 m/s`) |
| Giro | `180°/s` |

### Visual
- Pode ser feito com **primitivas do Unity**: cilindro achatado + "para-choque" + LED.
- **LED colorido** no topo indica a bateria (verde → amarelo → vermelho piscando).
- Escovas laterais girando (rotação simples via script).

### Sensor de queda (cliff sensor)
- `Raycast` para baixo a partir de um ponto **0.25 m à frente** do robô.
- Se não houver chão a menos de `0.2 m` → **bip de alerta + ícone no HUD**.
- O sensor **não freia** o robô sozinho: evitar a queda é responsabilidade do jogador.

---

## 6. Bateria

| Evento | Efeito |
|---|---|
| Andando | −1.5 / s |
| Parado | −0.5 / s |
| Turbo | consumo ×2 |
| Encostar no gato | −10 instantâneo |
| Dentro da base de recarga | +25 / s (até 100) |

- Começa com **100**.
- **Abaixo de 25%:** barra vermelha, LED piscando e bip a cada 2 s.
- **Chegou a 0 fora da base → DERROTA** ("A bateria acabou!").
- Na prática, uma carga cheia dura ~66 s andando. Em 3 minutos, o jogador precisa voltar à base 2 ou 3 vezes: essa é a tensão do jogo.

### Base de recarga (`ChargingDock`)
- Objeto no chão com um trigger. Enquanto o robô estiver dentro, recarrega.
- Feedback: som de "plug", LED azul e partícula leve.
- Fica num ponto **central** da casa, para que todo cômodo seja alcançável com uma carga.

---

## 7. Sujeira e limpeza

- Prefab `DirtSpot`: decal ou quad no chão com textura de poeira + **trigger**.
- Quando o robô encosta: fade-out rápido (0.2 s), partícula de poeira, som de "sucção" e o objeto é desativado.
- **~60 manchas** na fase, sendo **~15 embaixo de móveis** (sofá, cama, mesa). Embaixo dos móveis é onde o 3D vira mecânica.
- **`CleaningManager`** conta o total no início da fase (manchas registradas) e as coletadas.
- **Vitória: 90% limpo** (ex.: 54 de 60).
- Ao passar de 50% e de 75%: pequeno feedback no HUD ("Metade!").

---

## 8. Fase: `Level01` (apartamento)

### Layout (vista de cima, aproximado)
```
┌─────────────┬──────────────┐
│   QUARTO    │   COZINHA    │
│  (cama)     │ (mesa+cadeiras)
│             │              │
├──── porta ──┼──── vão ─────┤
│        SALA                │
│  (sofá, rack, tapete)      │
│     [BASE de recarga]      │
│                    ┌───────┤
│                    │ESCADA ↓│  ← sem grade, descendo para o andar de baixo
└────────────────────┴───────┘
```

- Área total ~ **10 × 8 m**, chão plano, exceto a escada.
- **Móveis com vão** por onde o robô passa: sofá (vão de 0.12 m), cama (vão de 0.15 m), mesa e cadeiras (passa entre os pés).
- **Escada:** degraus descendo. Um trigger `FallZone` abaixo da borda superior aciona a derrota ("Caiu da escada!"). A câmera pode acompanhar a queda por 0.5 s antes da tela de derrota.
- Paredes com colliders simples (BoxCollider), **sem frestas**. Rodapés não devem criar degraus invisíveis.

---

## 9. Obstáculos

### Gato (obrigatório)
- **NavMeshAgent** (pacote AI Navigation) patrulhando entre 4–6 waypoints, com velocidade `1.2 m/s` e pausa de 1–3 s em cada ponto (às vezes "deita e dorme" por 5 s).
- Se o robô chegar a **menos de 1.5 m**, o gato se vira para ele por um instante (reação, sem perseguir).
- **Encostar no gato:** o robô leva um empurrão (`AddForce` com impulso para longe do gato), −10 de bateria, miado e 1 s de invulnerabilidade, para não perder bateria em sequência.
- Colliders do gato: trigger para detectar contato + collider sólido simples (o robô não atravessa o gato).

### Meia no chão (extra)
- Trigger. Ao entrar, o robô fica **preso** (velocidade 0) até o jogador apertar **E 5 vezes**. Ícone de "aperte E" no HUD.
- 2 ou 3 meias espalhadas, longe da base.

---

## 10. Estados do jogo (critério 4)

```
MainMenu → Playing ⇄ Paused
              ↓
     Victory / Defeat → (Tentar de novo → Playing) | (Menu → MainMenu)
```

### Regras de robustez
- `GameManager` com `enum GameState { Playing, Paused, Victory, Defeat }` e evento `OnStateChanged`.
- **Toda condição de fim só vale se o estado atual for `Playing`.** Isso evita vitória e derrota simultâneas ou disparos duplos.
- **Pausa** usa `Time.timeScale = 0`. Ao carregar qualquer cena, **sempre** voltar `Time.timeScale = 1`.
- **Reiniciar** = recarregar a cena `Level01` com `SceneManager.LoadScene`. Nada de "resetar na mão".
- Derrota sempre mostra **o motivo**: `BatteryEmpty`, `FellDownStairs` ou `TimeUp`.
- Ao vencer ou perder: robô e gato param, o input de movimento é ignorado e o cursor fica visível.
- **Timer:** 3:00. Nos últimos 30 s, ele fica vermelho e toca um som de porta/chave ("o dono está chegando").
- Botão **Sair** chama `Application.Quit()` (funciona na build).

---

## 11. Interface

### HUD (durante o jogo)
- **Topo-esquerda:** % de limpeza (barra + número).
- **Topo-direita:** timer `mm:ss`.
- **Base-esquerda:** barra de bateria com ícone.
- **Centro (contextual):** alerta de borda (cliff), "aperte E" e "Bateria fraca!".

### Telas
| Tela | Conteúdo |
|---|---|
| **Menu principal** | Título, **Jogar**, **Como jogar** (painel com os controles), **Sair** |
| **Pausa** | **Continuar**, **Reiniciar**, **Menu** |
| **Vitória** | "Casa limpa!", tempo restante, **estrelas** (3★ se sobrou >60 s, 2★ >30 s, 1★ caso contrário), **Jogar de novo**, **Menu** |
| **Derrota** | Motivo da derrota, % que chegou a limpar, **Tentar de novo**, **Menu** |

- Navegação também por teclado (botão selecionado por padrão), não só pelo mouse.
- Fonte legível e consistente (TextMeshPro).

---

## 12. Áudio

| Som | Quando |
|---|---|
| Motor (loop) | Sempre que o robô anda; o **pitch acompanha a velocidade** |
| Sucção / "pop" | Ao coletar sujeira |
| Bip | Bateria fraca e alerta de borda |
| Plug / recarga | Ao entrar na base |
| Miado | Ao encostar no gato |
| Jingle | Vitória / derrota |
| Música | Trilha leve em loop, volume baixo |

Fontes sugeridas: Kenney (packs de áudio CC0), freesound.org (checar a licença de cada arquivo).

---

## 13. Assets

| Necessidade | Fonte sugerida |
|---|---|
| Móveis e cômodos | **Kenney – Furniture Kit** (CC0) |
| Gato | Modelo low-poly CC0/CC-BY (Quaternius, Poly Pizza) |
| Robô | Primitivas do Unity + materiais próprios |
| Texturas de chão e sujeira | Kenney / ambientCG (CC0) |
| UI | Kenney – UI Pack (CC0) |

**Critério 3 (visual):** paleta coerente (low-poly e cores suaves), escala real de 1 m, materiais URP configurados, iluminação com uma luz direcional + luzes de ambiente (abajur), sombras ligadas. A **sombra do robô** é importante para a noção de profundidade.

Todos os créditos vão no `README.md`.

---

## 14. Arquitetura de código

### Estrutura de pastas
```
Assets/_Project/
├── Scenes/        (MainMenu, Level01)
├── Scripts/
│   ├── Core/      (GameManager, GameState, SceneLoader)
│   ├── Player/    (RobotController, BatterySystem, CliffSensor, RobotVisuals)
│   ├── Gameplay/  (DirtSpot, CleaningManager, ChargingDock, FallZone, SockTrap, LevelTimer)
│   ├── AI/        (CatAI)
│   ├── Camera/    (CameraFollow, OcclusionFader)
│   ├── UI/        (HUDController, PauseMenu, EndScreen, MainMenu)
│   └── Audio/     (AudioManager)
├── Prefabs/
├── Materials/
├── Audio/
└── Art/ThirdParty/   (assets importados, cada pack em sua pasta)
```

### Responsabilidades
| Script | Faz |
|---|---|
| `GameManager` | Dono do estado do jogo; recebe eventos de fim e decide vitória/derrota; pausa |
| `RobotController` | Lê o input e move o Rigidbody; turbo; trava quando preso ou fora do estado `Playing` |
| `BatterySystem` | Consome e recarrega; evento `OnBatteryChanged(float)` e `OnBatteryEmpty` |
| `CliffSensor` | Raycast à frente; evento `OnEdgeWarning(bool)` |
| `DirtSpot` | Trigger; avisa o `CleaningManager` e se desativa |
| `CleaningManager` | Conta as manchas; evento `OnProgressChanged(float)`; avisa 90% |
| `ChargingDock` | Trigger que liga e desliga a recarga |
| `FallZone` | Trigger que aciona a derrota por queda |
| `LevelTimer` | Contagem regressiva; evento `OnTimeUp` |
| `CatAI` | Patrulha com NavMesh; empurrão e dano ao encostar |
| `CameraFollow` | Seguir, suavizar e evitar paredes (SphereCast) |
| `OcclusionFader` | Deixar transparentes os objetos entre a câmera e o robô |
| `HUDController` / telas | Só **escutam** eventos e mostram dados; não contêm regra de jogo |
| `AudioManager` | Toca SFX e música; volume central |

### Princípios
- Comunicação por **eventos C#** (`event Action<...>`): a UI não chama a lógica e a lógica não conhece a UI.
- Parâmetros de ajuste (velocidades, consumo, tempos) como `[SerializeField]` com os valores deste documento, para ajustar no Inspector sem mexer no código.
- Nada de `FindObjectOfType` em `Update`: as referências são resolvidas no `Awake`/`Start` ou via Inspector.
- Física em `FixedUpdate`, câmera em `LateUpdate` e input em `Update`.

---

## 15. Como cada critério de avaliação é atendido

| Critério | Como o jogo atende |
|---|---|
| **1. Controles, câmera e sensação 3D** | Controle em tanque responsivo com aceleração suave; câmera em 3ª pessoa suavizada que não atravessa paredes; oclusão de móveis; escala gigante dos móveis; passagem embaixo deles; sensor de borda e sombra do robô dando noção de profundidade |
| **2. Desempenho e colisões** | Collider redondo + atrito zero (não prende em quinas); Rigidbody Interpolate + Continuous (sem tremer nem atravessar); colliders de parede sem frestas; checklist de QA (seção 17) |
| **3. Modelos, materiais e texturas** | Packs coerentes (Kenney low-poly), escala normalizada, materiais URP, iluminação e sombras, robô com LED de status |
| **4. Estados e erros** | Máquina de estados única, guardas contra fim duplo, motivo de derrota claro, reinício por recarga de cena, pausa robusta, sem dead ends (a base sempre é alcançável) |

---

## 16. Plano de implementação (passos pequenos)

> Hoje: **ter 17/11** · Entrega: **qua 25/11**. Cada passo termina com algo **jogável e testável**.

| # | Passo | Critério de aceite | Prazo sugerido |
|---|---|---|---|
| 1 | Projeto Unity (URP) + Git + `.gitignore` de Unity + estrutura de pastas | Projeto abre em todas as máquinas do grupo | 17/11 |
| 2 | **Greybox** da casa com cubos (paredes, móveis com vão, escada) | Dá para andar pela casa com a câmera de cena | 17/11 |
| 3 | `RobotController` (tanque + turbo) | Robô anda, gira, não prende em quinas nem treme | 18/11 |
| 4 | `CameraFollow` + desvio de parede | Câmera nunca entra em parede; sem tremedeira | 18/11 |
| 5 | `DirtSpot` + `CleaningManager` + HUD de % | Coletar manchas sobe a % | 19/11 |
| 6 | `BatterySystem` + `ChargingDock` + HUD de bateria | Bateria cai, recarrega na base, derrota ao zerar | 19/11 |
| 7 | `GameManager` + `LevelTimer` + telas de vitória, derrota e pausa | Dá para ganhar, perder (3 motivos), pausar e reiniciar 10× seguidas sem bug | 20/11 |
| 8 | `FallZone` + `CliffSensor` | Alerta perto da borda; cair = derrota | 20/11 |
| 9 | `CatAI` (NavMesh + empurrão) | Gato patrulha; encostar empurra e tira bateria | 21/11 |
| 10 | `OcclusionFader` | Sofá e cama ficam transparentes com o robô embaixo | 21/11 |
| 11 | Trocar o greybox por **assets reais**, materiais, iluminação e LED do robô | Visual coerente, escala certa | 22–23/11 |
| 12 | Menu principal, "Como jogar", **áudio** | Fluxo completo Menu → Jogo → Fim → Menu | 23/11 |
| 13 | *(Extra)* Meias, estrelas na vitória, escovas girando | — | se sobrar tempo |
| 14 | **Build Windows** + QA (seção 17) + correções | Checklist 100% na build, não só no editor | 24/11 |
| 15 | README (créditos), GDD atualizado, página no itch.io com GIFs, instruções e controles | Tudo publicado e submetido na jam | 24–25/11 |

---

## 17. Checklist de QA (rodar **na build**, não só no editor)

**Colisão e movimento**
- [ ] Robô não prende em nenhuma quina, pé de móvel ou batente de porta
- [ ] Robô não atravessa paredes nem móveis, mesmo no turbo
- [ ] Robô não "sobe" em móveis nem tomba
- [ ] Todas as manchas são alcançáveis (inclusive as de baixo dos móveis)

**Câmera**
- [ ] Câmera nunca fica dentro de parede ou de móvel
- [ ] Robô sempre visível (oclusão funcionando)

**Estados**
- [ ] Vencer, perder pelos 3 motivos e reiniciar: tudo funciona 10× seguidas
- [ ] Pausar e despausar em vários momentos, inclusive perto do fim
- [ ] Não acontece vitória e derrota no mesmo frame
- [ ] Voltar ao menu e jogar de novo: tudo resetado (bateria, timer, %)
- [ ] Botão Sair fecha o jogo

**Desempenho**
- [ ] ≥ 60 FPS estáveis na build, em notebook comum
- [ ] Sem erros no console durante uma partida inteira

---

## 18. Instruções para o Claude Code

1. **Implemente um passo da seção 16 por vez.** Não avance sem o grupo confirmar que o passo anterior funciona.
2. Você **não tem acesso ao Editor do Unity**. Portanto:
   - Escreva os scripts C# em `Assets/_Project/Scripts/...`.
   - **Não edite à mão** arquivos `.unity`, `.prefab`, `.asset` ou `.meta`.
   - Ao final de cada passo, entregue um **roteiro de montagem no Editor**: quais objetos criar, que componentes adicionar, quais valores colocar no Inspector, quais layers e tags criar.
   - Quando valer a pena, crie **Editor scripts** (menu `Tools/Poeirinha/...`) que automatizem tarefas repetitivas, como espalhar as manchas de sujeira.
3. Ao final de cada passo, diga **como testar** e qual é o **critério de aceite**.
4. Use comentários inline nos trechos não óbvios (física, câmera, eventos). **O grupo precisa entender e explicar todo o código** nas sprints semanais.
5. Siga a arquitetura da seção 14: eventos C#, UI sem regra de jogo, parâmetros em `[SerializeField]`.
6. O projeto usa **Unity 6.3 LTS (6000.3.25f1)**: use as APIs atuais (ex.: `Rigidbody.linearVelocity`, `FindFirstObjectByType`) e evite as obsoletas.
7. Commits pequenos e descritivos, um por passo ou subpasso.
8. Nada de assets pagos. Todo asset novo entra na seção de créditos do `README.md`.
9. Em caso de dúvida de design, **siga este documento**. Se algo aqui estiver inviável, proponha a alternativa mais simples e registre a mudança neste GDD.

---

## 19. Histórico de mudanças do GDD

| Data | Mudança |
|---|---|
| 17/11/2026 | Versão inicial |
| 17/11/2026 | **Implementação completa (passos 1–14).** Mudanças em relação ao plano: (a) collider do robô = **cilindro convexo** (MeshCollider), porque esfera/cápsula com 0.35 m de diâmetro não cabem nos 0.09 m de altura; (b) espelho dos degraus = **0.25 m**, senão o sensor de 0.2 m não detecta a borda; (c) tapetes e base de recarga sem collider (um degrau de milímetros travaria o robô); (d) visual com **primitivas + texturas geradas por código** e **áudio sintetizado por código** (sem assets de terceiros; dá para trocar por Kenney depois); (e) as cenas são montadas por Editor script (`Tools/Poeirinha/Montar tudo`) em vez de roteiro manual; (f) 17 testes PlayMode automáticos cobrindo boa parte da seção 17. |
