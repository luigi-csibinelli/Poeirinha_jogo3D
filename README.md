# Poeirinha

Jogo 3D hiper-casual feito em Unity para o Projeto 3D (Insper, 2026.2).
Você é um **robô aspirador** e precisa limpar 90% da casa **antes do dono voltar** (3 minutos).
A bateria acaba, o gato corre atrás de você (esconda-se embaixo do sofá ou da cama!) e a escada é fatal.
Sensibilidade do giro, do mouse e volume ficam em **Opções**.

![Vista geral](Docs/Screenshots/00_vista_geral.png)

## Controles

| Tecla | Ação |
|---|---|
| W / S ou ↑ / ↓ | Andar para frente / ré |
| A / D ou ← / → | Girar |
| Shift esquerdo (segurar) | Turbo (+60% de velocidade, gasta 2× bateria) |
| E (várias vezes) | Soltar-se de uma meia enroscada |
| Mouse / roda | Girar a câmera / zoom |
| Esc | Pausar / despausar |

## Como abrir

1. Unity **6000.3.25f1** (Unity 6.3 LTS), template URP.
2. Abra a pasta `Poeirinha` no Unity Hub.
3. Abra `Assets/_Project/Scenes/MainMenu.unity` e aperte Play.

## Ferramentas do Editor (menu `Tools > Poeirinha`)

As cenas, prefabs e materiais são **gerados por código** (`Assets/_Project/Editor/PoeirinhaBuilder.cs`):

| Menu | O que faz |
|---|---|
| 1. Configurar projeto | Layers (Player, Wall, Occluder, Cat), tamanho do agente da NavMesh (gato), sombras, nome do produto |
| 2. Montar tudo | **Recria** texturas, materiais, prefabs, `MainMenu`, `Level01`, NavMesh e Build Settings |
| Espalhar sujeira na cena aberta | Sorteia de novo as ~60 manchas (15 embaixo de móveis) em `Level01` |
| Capturar screenshots | Salva prints em `Screenshots/` |
| Build Windows | Gera `Build/Windows/Poeirinha.exe` |

> Atenção: "Montar tudo" sobrescreve as cenas. Se alguém ajustar algo à mão na cena, passe o ajuste para o script
> (ou pare de usar o "Montar tudo" a partir daí).

## Estrutura

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

A arquitetura segue o GDD (seção 14): comunicação por eventos C#, a UI só escuta, e todos os parâmetros
ficam em `[SerializeField]` para ajustar no Inspector.

## Testes automáticos

`Window > General > Test Runner > PlayMode > Run All`. Os 22 testes cobrem boa parte do checklist de QA
(GDD seção 17): movimento, colisão no turbo, bateria e base, vitória, os 3 motivos de derrota, pausa,
reiniciar 10×, menu, gato (patrulha, perseguição e esconderijo), NavMesh, meia, sensibilidade, câmera pelo mouse, câmera fora das paredes e oclusão do sofá.
Pela linha de comando:

```
Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults results.xml
```

Os testes **não substituem** rodar o checklist na build.

## Créditos

- **Programação, modelos e texturas:** o grupo. Os modelos são feitos com primitivas do Unity e as texturas
  (madeira, azulejo, tapete, poeira, estrela) são geradas por código.
- **Sons e música:** sintetizados por código (`ProceduralAudio.cs`). Nenhum arquivo de áudio de terceiros.
- **Fonte:** Liberation Sans (SIL Open Font License), incluída no TextMesh Pro.
- **Engine:** Unity 6.3 LTS, URP, Input System, AI Navigation.

Se algum asset externo (ex.: Kenney Furniture Kit, CC0) for adicionado, registre aqui.
