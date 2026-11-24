# Poeirinha 🧹🤖

> Você é um **robô aspirador** e tem **3 minutos** para limpar a casa **antes do dono voltar**.
> A bateria acaba, o gato corre atrás de você e a escada não tem grade.

Jogo 3D hiper-casual feito em Unity para o **Projeto 3D – Hiper-Casual** (Insper, 2026.2).

![Vista geral do apartamento](Docs/Screenshots/00_vista_geral.png)

| | |
|---|---|
| **Jogar** | [JOGAR](https://luigi-csibinelli.itch.io/poeirinha) |
| **Plataforma** | Navegador (WebGL) e Windows, com teclado + mouse |
| **GDD** | [MILANOTE](https://app.milanote.com/1XvDTh1eiNZ69m?p=c6i8IfX5k9Z) |
| **Engine** | Unity 6.3 LTS (6000.3.25f1), URP |
| **Documentação** | [GDD.md](GDD.md) (Game Design Document) |
| **Equipe** | Kauã Figueira, Luigi Sibinelli, Mathias Zanotto, Matheus Constante |

---

## Como jogar

**Objetivo:** aspirar **90% da sujeira** do apartamento antes do timer chegar a zero.

| Tecla | Ação |
|---|---|
| **W / S** ou **↑ / ↓** | Andar para frente / ré |
| **A / D** ou **← / →** | Girar |
| **Shift** (segurar) | Turbo: mais rápido, mas gasta o dobro de bateria |
| **Mouse** / **roda** | Girar a câmera em volta do robô / zoom |
| **E** (várias vezes) | Soltar-se de uma meia enroscada |
| **Esc** ou **P** | Pausar (no navegador, o Esc também solta o mouse e pausa o jogo) |

**Dicas:**
- 🔋 **Bateria:** ela cai o tempo todo. Volte à **base azul** (na parede entre a sala e o quarto) para recarregar.
  Se zerar longe da base, você perde.
- 🐈 **Gato:** se ele te vir, aparece um **"!"** e ele corre atrás de você. Fuja no turbo ou **esconda-se embaixo
  do sofá ou da cama**: ali ele não cabe. Se ele te pegar, você é empurrado e perde bateria.
- ⚠️ **Escada:** fica no canto da sala, sem grade. Um bip avisa quando a borda está perto. Caiu, perdeu.
- 🧦 **Meias:** prendem o robô. Aperte **E** cinco vezes para se soltar.
- 🛋️ **Sujeira escondida:** 15 das 60 manchas estão **embaixo dos móveis**.
- ⭐ **Estrelas:** vença com mais de 60 s sobrando para ganhar 3 estrelas.
- ⚙️ **Opções:** sensibilidade do giro e do mouse, volume, inverter o eixo Y e recentralizar a câmera
  (no menu principal e na pausa).

| Início na base | Embaixo do sofá | Cozinha |
|---|---|---|
| ![](Docs/Screenshots/01_inicio_base.png) | ![](Docs/Screenshots/02_embaixo_sofa.png) | ![](Docs/Screenshots/03_cozinha.png) |

| Escada | Quarto | Menu |
|---|---|---|
| ![](Docs/Screenshots/04_escada.png) | ![](Docs/Screenshots/05_quarto.png) | ![](Docs/Screenshots/08_menu_ui.png) |

---

## Rodando o projeto

1. Instale o **Unity 6000.3.25f1** (Unity 6.3 LTS) pelo Unity Hub.
2. Clone o repositório e abra a pasta no Unity Hub (**Add project from disk**).
3. Abra `Assets/_Project/Scenes/MainMenu.unity` e aperte **Play**.

**Build WebGL (navegador / itch.io):** `Tools > Poeirinha > Build WebGL (itch.io)` (gera `Build/WebGL/Poeirinha`).
Para publicar, compacte o **conteúdo** dessa pasta (o `index.html` precisa ficar na raiz do `.zip`) e envie no itch.io
marcando "This file will be played in the browser". A compressão já está configurada como Gzip com
"decompression fallback", que funciona no itch.io sem configuração extra.

**Build Windows:** `Tools > Poeirinha > Build Windows` (gera `Build/Windows/Poeirinha.exe`) ou `File > Build Profiles`.
A pasta `Build/` não vai para o Git: a build é publicada no itch.io.

### Ferramentas do Editor (`Tools > Poeirinha`)

As cenas, os prefabs, os materiais e a NavMesh são **gerados por código** (`Assets/_Project/Editor/PoeirinhaBuilder.cs`),
o que deixa o projeto reproduzível.

| Menu | O que faz |
|---|---|
| 1. Configurar projeto | Layers (Player, Wall, Occluder, Cat), agente da NavMesh do gato, sombras, nome do produto |
| 2. Montar tudo | **Recria** texturas, materiais, prefabs, as cenas `MainMenu` e `Level01`, a NavMesh e as Build Settings |
| Espalhar sujeira na cena aberta | Sorteia de novo as 60 manchas em `Level01` |
| Capturar screenshots | Salva prints em `Screenshots/` |
| Build WebGL (itch.io) | Gera `Build/WebGL/Poeirinha` (versão para navegador) |
| Build Windows | Gera `Build/Windows/Poeirinha.exe` |

> ⚠️ **"Montar tudo" sobrescreve as cenas.** Ajustes feitos à mão na cena se perdem se o script rodar de novo:
> passe o ajuste para o script (ou pare de usar o "Montar tudo" a partir daí).

### Estrutura

```
Assets/_Project/
├── Scenes/      MainMenu, Level01 (+ NavMesh e perfil de pós-processamento)
├── Scripts/
│   ├── Core/      GameManager, GameState, SceneLoader, GameSettings
│   ├── Player/    RobotController, BatterySystem, CliffSensor, RobotVisuals
│   ├── Gameplay/  DirtSpot, CleaningManager, ChargingDock, FallZone, SockTrap, LevelTimer
│   ├── AI/        CatAI
│   ├── Camera/    CameraFollow, OcclusionFader, FadeableObject, MenuCameraOrbit, Billboard
│   ├── UI/        HUDController, PauseMenu, EndScreen, MainMenu, OptionsMenu
│   └── Audio/     AudioManager, ProceduralAudio
├── Editor/      PoeirinhaBuilder, BuilderUtils, TextureGen, ScreenshotTool
├── Tests/PlayMode/  Level01Tests (22 testes)
├── Prefabs/     Robot, Cat, ChargingDock, Sock, DirtSpot_Dust, DirtSpot_Crumbs
├── Materials/
└── Art/         Texturas e meshes gerados
```

Os sistemas conversam por **eventos C#**: a UI e o áudio só escutam, e a lógica não conhece a UI. Os números de
ajuste (velocidades, bateria, tempos, gato) ficam em `[SerializeField]`, editáveis no Inspector.
Detalhes no [GDD](GDD.md#11-arquitetura-técnica).

### Testes automáticos

`Window > General > Test Runner > PlayMode > Run All`. São **22 testes**: movimento, colisões no turbo, bateria e base,
vitória, as 3 derrotas, pausa, reiniciar 10×, menu, gato (patrulha, perseguição e esconderijo), NavMesh, meia,
opção de sensibilidade, câmera pelo mouse, câmera fora das paredes e transparência do sofá.

Pela linha de comando:

```
Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults results.xml
```

---

## Créditos

- **Programação, modelos, texturas e sons:** a equipe. Os modelos são feitos com primitivas do Unity, as texturas
  (madeira, azulejo, tapete, poeira, estrela) são geradas por código e todos os sons e a música são sintetizados
  por código (`ProceduralAudio.cs`). **Nenhum asset de terceiros** foi usado.
- **Fonte:** Liberation Sans (SIL Open Font License), incluída no TextMesh Pro.
- **Engine e pacotes:** Unity 6.3 LTS, Universal Render Pipeline, Input System, AI Navigation, TextMesh Pro.

