# The Fusion

Videojuego de plataformas 2D desarrollado en Unity como entrega de la evaluación EVA2.

| Dato | Detalle |
| --- | --- |
| Asignatura | Desarrollo de Video Juegos |
| Docente | Nilson Guerra |
| Alumno | Dantes Burgos |
| Motor | Unity 6000.4.5f1 (URP) |
| Plataforma | Windows (64 bits) |
| Escena principal | `Assets/Scenes/SampleScene.unity` |

---

## Descripción

**The Fusion** es un plataformas 2D en el que el jugador debe atravesar un nivel, derrotar enemigos,
recolectar monedas y llegar a la zona de victoria. El nivel incluye un jefe final con 5 puntos de vida
y una pared elemental que solo puede abrirse con el elemento **Rayo**.

El proyecto está construido sobre la plantilla *2D Platformer* de Unity y utiliza el **Input System**
moderno (`activeInputHandler: 1`), por lo que no se usan los ejes clásicos de `Input Manager`.

---

## Contenido del repositorio

```
prototipojuego_eva2/
├── Assets/              Código, escenas, prefabs, arte, audio y ajustes del juego
├── Packages/            Dependencias del proyecto (manifest.json)
├── ProjectSettings/     Configuración del proyecto y del Input System
├── Ejecutable/          Build de Windows listo para ejecutar (ver aviso más abajo)
├── .gitignore           Exclusiones de Git para proyectos Unity
└── .vsconfig            Componentes recomendados de Visual Studio
```

Las carpetas generadas (`Library/`, `Temp/`, `Logs/`, `UserSettings/`, `obj/`) y los archivos de
solución (`*.csproj`, `*.sln`) **no** se incluyen: Unity los regenera al abrir el proyecto.

---

## Requisitos

- **Unity 6000.4.5f1** con el módulo *Windows Build Support (IL2CPP/Mono)*.
- El proyecto usa **Universal Render Pipeline (URP) 17.4.0** e **Input System 1.19.0**.
- Para abrirlo: `Unity Hub → Add → Add project from disk → seleccionar la carpeta prototipojuego_eva2`.

Al abrir por primera vez Unity importará los assets y regenerará `Library/` (puede tardar varios minutos).

---

## Cómo jugar

### Opción 1: ejecutable incluido

Abrir `Ejecutable/plantilla_2.exe` y jugar con teclado y mouse.

> **Aviso:** el ejecutable incluido corresponde a un build anterior a las últimas correcciones de
> código (ataque cuerpo a cuerpo y vida del jefe). Para probar la versión final hay que recompilar
> (ver *Compilar el ejecutable*) o abrir el proyecto en Unity y presionar **Play**.

### Opción 2: desde el editor

1. Abrir el proyecto en Unity 6000.4.5f1.
2. Abrir `Assets/Scenes/SampleScene.unity`.
3. Presionar **Play**.

---

## Controles

| Acción | Teclado / Mouse | Mando |
| --- | --- | --- |
| Moverse | `A` / `D` o flechas izquierda/derecha | Stick izquierdo / D-Pad |
| Saltar | `Espacio` | Botón Sur (A / Cruz) |
| Atacar (cuerpo a cuerpo) | **Click izquierdo** o `X` | Botón Oeste (X / Cuadrado) |
| Gancho | `F` | Botón Norte |
| Menú | `Esc` | Botón Start |

> Las teclas `E` y `Q` **no** están implementadas. El ataque es exclusivamente cuerpo a cuerpo y se
> activa con click izquierdo, `X` o el botón Oeste del mando.

El ataque tiene un alcance de `1.2` unidades, hace `1` punto de daño y un tiempo de recarga de
`0.25` segundos. Los golpes encadenados reproducen las animaciones `Attack1`, `Attack2` y `Attack3`.

---

## Estado actual del nivel

| Elemento | Posición (x, y) | Notas |
| --- | --- | --- |
| Punto de aparición | (-28, 0.21) | Inicio del nivel |
| Enemigo 1 | (-6.5, -0.01) | Activo |
| Enemigo 2 | (28.14, 11.9) | Activo |
| Jefe | (-20, 14.11) | 5 puntos de vida |
| Pared elemental | (23, 29.8) | Requiere el elemento **Rayo** |
| Zona de victoria | (35, 30.8) | Fin del nivel |
| Monedas | distribuidas en el nivel | ~48 monedas activas |

El nivel contiene además decoración (props del pueblo), plataformas, escaleras y capas de parallax.

### Limitaciones conocidas

- En la escena actual **no hay cofres interactivos** que entreguen el elemento **Rayo**, por lo que
  la pared elemental bloquea el paso. El elemento puede regenerarse ejecutando el menú
  `Village → Migrar nivel definitivo` dentro del editor.
- `Pared_Elemental`, `ChestPickup` y `DamageZone` están implementados en el código, pero no todos
  están colocados en la escena actual.

---

## Scripts principales

| Script | Responsabilidad |
| --- | --- |
| `Mechanics/PlayerController.cs` | Movimiento, salto y física del jugador |
| `Mechanics/PlayerMeleeAttack.cs` | Ataque cuerpo a cuerpo, combo de animaciones y daño |
| `Mechanics/PlayerAbilities.cs` | Habilidades elementales y gancho |
| `Mechanics/Health.cs` | Vida genérica; dispara `HealthIsZero` al llegar a 0 |
| `Mechanics/EnemyController.cs` | Comportamiento y patrulla de enemigos |
| `Mechanics/ElementalWall.cs` | Pared que se abre con un elemento concreto |
| `Mechanics/ChestPickup.cs` | Cofre que entrega un elemento |
| `Mechanics/DamageZone.cs` / `DeathZone.cs` | Zonas de daño y de muerte |
| `Mechanics/VictoryZone.cs` | Condición de victoria |
| `Mechanics/TokenController.cs` | Monedas recolectables |
| `Mechanics/GameController.cs` | Estado global de la partida |
| `Gameplay/*` | Reglas de colisión, muerte, salto, aterrizaje y victoria |
| `UI/HudController.cs` | HUD (vida, monedas y elementos) |
| `View/CircleEffect.cs` | Efecto visual del impacto del ataque |

---

## Compilar el ejecutable

Dentro del editor: menú **`Village → Compilar ejecutable (Windows)`**.

El build se genera en la carpeta `Ejecutable/` (renombrando el ejecutable anterior si es necesario).
El script del menú es `Assets/Editor/GameBuilder.cs`.

---

## Recursos y créditos

- **Unity 2D Platformer** — plantilla base de Unity Technologies.
- **Hero Knight – 2D Pixel Art** — Sven Thole.
- **Pixel Art Platformer – Village Props** — Cainos.
- **Mod Assets** — Unity Technologies.
- **TextMesh Pro / LiberationSans** — SIL Open Font License 1.1 (ver `Assets/ThirdPartyNotice.txt`).

Todos los assets de terceros se usan bajo la licencia de Unity Asset Store EULA o sus licencias
correspondientes.

---

## Autor

**Dantes Burgos** — Desarrollo de Video Juegos, EVA2.
