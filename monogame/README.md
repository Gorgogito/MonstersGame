# Monsters Game — Duelo de Monstruos

Videojuego de duelo de cartas desarrollado en **C# + MonoGame**, adaptado a la especificación funcional de **`Reglas MonstersGame.docx`** (documento de referencia vigente; `Reglas.pdf` es el reglamento completo del TCG original y se conserva solo como referencia histórica, sin valor normativo cuando difiere del docx). Esta primera versión implementa únicamente el **Modo Duelo: Jugador Humano vs CPU**.

---

## Cómo abrir y ejecutar

### Desde Visual Studio 2022
1. Abrir `MonstersGame.sln`.
2. Pulsar **F5** (Depurar) o **Ctrl+F5** (Ejecutar sin depurar).

### Desde la línea de comandos
```powershell
dotnet build MonstersGame.sln -c Debug
dotnet run --project MonstersGame
```

### Requisitos
- **.NET 10 SDK** (el proyecto apunta a `net10.0`).
- **Visual Studio 2022 17.12 o superior** (versión que incluye soporte para .NET 10).
- No requiere instalar el *Content Pipeline* de MonoGame ni herramientas externas: la fuente y los placeholders se generan en tiempo de ejecución, y el arte real (`Data/Art/`) son PNG que se cargan directo, sin compilar nada de antemano — la solución compila y se ejecuta directamente.
- Paquete NuGet usado: `MonoGame.Framework.DesktopGL` (se restaura automáticamente).
- `<RollForward>Major</RollForward>` permite ejecutar también sobre runtimes aún más nuevos si fuera necesario.

---

## Controles

| Acción | Control |
|---|---|
| Navegar menús | Flechas / ratón |
| Confirmar | Enter / clic |
| Previsualizar las cartas de un mazo (Selección de Mazo) | Pasa el mouse sobre un mazo de la lista; el panel de la derecha lista sus cartas |
| Paginar la previsualización de un mazo | Botones **< ANTERIOR** / **SIGUIENTE >**, flechas **Izquierda/Derecha**, o rueda del mouse sobre la lista |
| Seleccionar carta de la mano | Clic sobre la carta |
| Invocar / Colocar / Fusionar | Botones contextuales |
| Elegir Sacrificios (Nivel 5+) | Clic en tus monstruos hasta completar la cantidad requerida, luego **CONFIRMAR** |
| Invocar por Volteo | Selecciona tu monstruo boca abajo (no Colocado este turno) y pulsa **VOLTEAR** |
| Colocar / Activar Magia o Trampa | Selecciona la carta en tu mano; **COLOCAR** siempre disponible, **ACTIVAR** para Magias (Rituales usan el flujo de abajo) |
| Activar una Magia/Trampa ya Colocada | Clic directo en su casilla de la Zona de Magia/Trampa en el tablero, luego **ACTIVAR** |
| Elegir el objetivo de un efecto | Clic en un monstruo del Campo, o **SIGUIENTE CEMENTERIO** + **CONFIRMAR OBJETIVO** si el objetivo está en tu Cementerio (todavía sin caja propia) |
| Invocar por Ritual | Selecciona la Magia de Ritual en tu mano, pulsa **INVOCAR RITUAL** y elige al Monstruo de Ritual correspondiente (Sacrificios automáticos) |
| Responder a una Cadena / resolverla | Encadena otra carta (mismos botones de siempre) o pulsa **RESOLVER CADENA** |
| Seleccionar atacante / objetivo | Clic en tu monstruo, luego en el objetivo |
| Siguiente fase | Botón "Siguiente" o **Espacio** (deshabilitado mientras haya una Cadena abierta) |
| Terminar turno | Botón o **Enter** — si tu mano supera el límite, primero debes elegir qué descartar |
| Deseleccionar | **Esc** |

**Selección de Mazo:** al pasar el mouse sobre un mazo de la lista, el panel de la derecha muestra sus cartas — una fila por carta distinta (con "xN" si hay copias), con miniatura, Nivel, ATK/DEF e insignia de Atributo para Monstruos, o el tipo de carta para Magias/Trampas (al estilo de la lista de mazo de *Yu-Gi-Oh! Forbidden Memories*). Si el mazo tiene más cartas distintas de las que entran en una página, se pagina con los botones, las flechas del teclado o la rueda del mouse.

**Fusión:** selecciona una carta de la mano, pulsa **FUSIONAR** y elige la segunda carta. Si existe una receta válida (tabla `Fusions` de `Data/monstersgame.db`), se invoca el monstruo resultante.

**Sacrificios:** al pulsar INVOCAR/COLOCAR sobre un monstruo de Nivel 5+, el juego pide elegir tus propios monstruos como Sacrificio (clic para marcar/desmarcar) en vez de elegirlos automáticamente.

**Límite de mano:** si terminas el turno con más de 6 cartas, aparece una pantalla de descarte: elige exactamente las cartas sobrantes y confirma con **DESCARTAR** antes de que el turno pase al rival.

**Cadena y Prioridad:** activar una Magia o Trampa ya no se resuelve al instante — abre una Cadena. El rival (la CPU) tiene la oportunidad de responder antes de que se resuelva; si no responde, te toca a ti decidir si encadenas otra carta o resuelves. Solo cartas con Velocidad de Hechizo 2 o mayor pueden responder (y deben igualar o superar la Velocidad del eslabón anterior). La Cadena se resuelve del último eslabón activado al primero. Mientras esté abierta, ninguna otra acción (Invocar, cambiar de fase, atacar, terminar el turno) es válida. El **panel de Cadena** (columna derecha de la pantalla de Duelo) muestra siempre el estado: "SIN CADENA ACTIVA" en reposo, o la lista de eslabones (del último activado al primero, el mismo orden en que se resuelven) con quién la activó y con qué carta, mientras está abierta.

**Magias y Trampas:** una Trampa siempre debe Colocarse antes de poder activarse, y no en el mismo turno en que se Coloca. Las Magias pueden activarse directamente desde la mano o Colocarse boca abajo y activarse después (incluso el mismo turno). Las de Campo van a una zona propia (reemplazan la anterior) y resuelven al instante, sin pasar por la Cadena; las Continuas y de Equipo permanecen boca arriba en el Campo; el resto ejecuta su efecto (si tiene uno registrado) y se resuelve al Cementerio. Desde el Bloque 7, la Zona de Magia/Trampa (5 casillas) y la Zona de Campo (1 casilla) tienen su propia caja en el tablero, debajo/encima de las Zonas de Monstruos de cada jugador: se activa una carta ya Colocada con un clic directo en su casilla. Tus propias cartas boca abajo muestran su nombre real (siempre sabes qué Colocaste); las de la CPU muestran un reverso oculto, igual que un monstruo boca abajo.

**Efectos de carta:** algunas Magias, Trampas y Monstruos de Efecto ya tienen un efecto real (no solo el movimiento de la carta): robar cartas, destruir un monstruo objetivo, revivir un monstruo desde tu Cementerio, o negar la activación a la que responden. Si una carta necesita un objetivo, se elige al activarla (antes de que entre en la Cadena), nunca al resolverse.

---

## Arquitectura

La lógica del juego está **totalmente desacoplada** de la interfaz gráfica, y desde la adaptación a MonstersGame también está **separada en su propio proyecto** (`MonstersGame.Core`), sin ninguna dependencia de MonoGame, para poder compartirla entre el juego, las pruebas automatizadas y (más adelante) el editor de cartas.

```
MonstersGame.sln
 ├─ MonstersGame.Core/         (lógica pura, sin dependencias de MonoGame)
 │   ├─ Entities/              Card, MonsterCard, SpellCard, TrapCard, CardInstance,
 │   │                         SpellTrapInstance, Player, FusionRecipe, enums
 │   ├─ Rules/                 DuelConfig (constantes de reglas)
 │   ├─ Battle/                DuelEngine, DuelState, ChainLink, BattleResolver, GameLog, ActionResult
 │   ├─ Effects/                IEffectAction, ITargetedEffectAction, EffectContext,
 │   │                         EffectTarget, EffectRegistry, catalogo de acciones
 │   ├─ AI/                    IDuelAI, BasicCpuAI
 │   └─ Services/              CardDatabase, FusionService
 │
 ├─ MonstersGame.Data/          (carga de datos, referencia a Core)
 │   ├─ Loaders/                ICardLoader/IDeckLoader/IFusionLoader, DTOs, CardDtoMapper/Validator,
 │   │                           JsonCardLoader/Writer (backend alternativo, ver más abajo)
 │   ├─ Sqlite/                 SqliteSchema, SqliteCardLoader/Writer, SqliteDeckLoader/Writer,
 │   │                           SqliteFusionLoader, SqliteTypeWriter, JsonToSqliteMigrator
 │   └─ GameData.cs             Orquestador de datos
 │
 ├─ MonstersGame.Tests/         (xUnit, referencia solo a Core)
 │
 ├─ MonstersGame.Data.Tests/    (xUnit, referencia a Core + Data)
 │
 ├─ MonstersGame/                (juego MonoGame — referencia a Core y Data)
 │   ├─ Data/                    monstersgame.db (base SQLite unica; se copia al directorio
 │   │                           de salida y la lee MonstersGame.Data)
 │   │   └─ Art/                 Cards/, Attributes/, Level/ — arte real (Bloque 11),
 │   │                           PNG cargados directo, sin Content Pipeline
 │   ├─ Screens/                 (presentación)
 │   │   ├─ MainMenu/            MainMenuScreen
 │   │   ├─ DeckSelection/       DeckSelectionScreen
 │   │   ├─ Duel/                DuelScreen
 │   │   ├─ Result/              ResultScreen
 │   │   └─ ScreenManager, GameContext, Screen, Widgets
 │   ├─ Graphics/                Primitives, BitmapFont, IconAtlas, TextureCache, CardRenderer, Theme
 │   ├─ Input/                   InputManager
 │   ├─ Audio/                   AudioManager, SoundSynth (efectos sintetizados por codigo)
 │   ├─ MonstersGameApp.cs       Clase Game (solo inicialización + bucle)
 │   └─ Program.cs               Punto de entrada
 │
 ├─ MonstersGame.CardEditor/     (app de escritorio WinForms — crear/editar/duplicar/
 │                                eliminar/validar cartas y mazos; importar/exportar
 │                                cartas y paquetes; ver "Editor de cartas" más abajo)
 │   ├─ MainForm.cs               Ventana principal (cartas)
 │   ├─ DeckEditorForm.cs         Ventana secundaria (mazos, Bloque 12)
 │   ├─ TypeEditorForm.cs         Ventana secundaria (Tipos de Monstruo, Bloque 13)
 │   ├─ CardRepository.cs / DeckRepository.cs / TypeRepository.cs
 │   └─ CardPreviewControl.cs, PackService.cs
 │
 └─ MonstersGame.CardEditor.Tests/  (xUnit, referencia a Core + Data + CardEditor;
                                      prueba CardRepository/DeckRepository/TypeRepository y PackService
                                      sin WinForms)
```

### Principios aplicados
- **Responsabilidad única** y clases pequeñas.
- El **motor (`DuelEngine`)** no conoce nada de gráficos; la UI y la IA usan solo su API pública.
- La **IA** (`BasicCpuAI`) está aislada tras `IDuelAI` y puede reemplazarse sin tocar el motor.
- **Cero cartas codificadas en el código**: todo se carga desde datos externos (SQLite por defecto).
- `MonstersGame.Core` no depende de ningún framework de UI: por eso `MonstersGame.Tests` puede probar todas las reglas sin arrancar MonoGame.

### Ejecutar las pruebas
```powershell
dotnet test MonstersGame.Tests/MonstersGame.Tests.csproj
dotnet test MonstersGame.Data.Tests/MonstersGame.Data.Tests.csproj
dotnet test MonstersGame.CardEditor.Tests/MonstersGame.CardEditor.Tests.csproj
```
226 pruebas en total (125 del motor de reglas — incluye `DuelState.LastAttack` —, 67
de carga/validación/escritura de datos — incluye los backends JSON y SQLite, sus
escritores de mazos y Tipos, y el migrador entre ambos —, 34 del catálogo de
cartas, mazos y Tipos, y de exportar/importar del editor). La pantalla de Duelo
(`DuelScreen`), la Selección de Mazo
(`DeckSelectionScreen`) y los formularios del editor (`MainForm`, `DeckEditorForm`,
`TypeEditorForm`) no tienen pruebas automatizadas — son la capa de presentación de
MonoGame/WinForms respectivamente y se verifican con un smoke test manual (arranca
sin excepciones) en
cada entrega, no con xUnit. Tampoco hay pruebas
automatizadas de audio ni de las animaciones del Bloque 10 (destellos, degradados,
"lunge" de ataque): son igualmente presentación, verificadas a mano cuando es posible
y, para audio, sin forma de confirmar en este entorno que el sonido generado suena
como se espera (ver "Sonido y animaciones" más abajo).

---

## Carga de datos (SQLite)

Toda la información (cartas, mazos, fusiones) se carga mediante las interfaces
`ICardLoader`, `IDeckLoader` e `IFusionLoader`, sin que el resto del motor sepa nada del
formato concreto. Desde el **Bloque 9**, la fuente de datos por defecto es una **base
SQLite única** (`Data/monstersgame.db`) — el catálogo de cartas es lo bastante grande
como para que un archivo por carta (Bloque 6) dejara de ser lo más práctico. Los
loaders JSON (`JsonCardLoader`/`JsonCardWriter`, ver abajo) se conservan intactos como
backend alternativo, no se borraron: siguen probados y listos si alguna vez hace falta
volver a ellos, sin tocar el resto del motor — es exactamente la promesa de diseño
original ("basta con implementar las mismas interfaces") ya demostrada con dos backends
reales conviviendo en el código.

### Esquema (`Data/monstersgame.db`)
```sql
Cards (Id PK, Kind, Name, Attack, Defense, Level, Type, Attribute, Image, Description,
       EffectId, SubType, Category, RitualMonsterId, RequiredRitualLevel)
Decks (Id PK autoincrement, Name)
DeckCards (DeckId FK, Position, CardId)   -- admite cartas repetidas, orden preservado
Fusions (MaterialA, MaterialB, Result)
```
`kind` admite `Monster`, `Spell` y `Trap`. Para `Spell`, `subType` admite `Normal`,
`Ritual`, `Continuous`, `Equip`, `Field` y `QuickPlay`; para `Trap`, `Normal`,
`Continuous` y `Counter` (por defecto `Normal` si se omite). Para `Monster`, `category`
admite `Normal`, `Effect`, `Fusion` y `Ritual` (por defecto `Normal`); solo `Fusion` y
`Ritual` no pueden Invocarse de Modo Normal. `effectId` se resuelve contra
`EffectRegistry` (código, no declarativo — ver más abajo); `ritualMonsterId` y
`requiredRitualLevel` solo aplican a Magias de Ritual. `DeckCards.Position` existe para
preservar el orden de inserción de un mazo (no afecta al juego, que siempre baraja).

### Importante: el editor y el juego leen bases de datos distintas
El editor escribe siempre en `MonstersGame/Data/monstersgame.db` (la base "fuente",
junto al código fuente). El juego, en cambio, lee la copia que MSBuild deja en su
carpeta de salida (`MonstersGame/bin/Debug/net10.0/Data/monstersgame.db`), copiada desde
la fuente en cada build (`<Content Include="Data\monstersgame.db"><CopyToOutputDirectory>
PreserveNewest</CopyToOutputDirectory>`). Esa copia **solo se actualiza al compilar** el
proyecto `MonstersGame` — si edita cartas o arma un mazo nuevo en el editor y luego
ejecuta un `MonstersGame.exe` que ya estaba compilado (sin recompilar), el juego seguirá
viendo los datos viejos. Al ejecutar con `dotnet run` desde `MonstersGame/` o con F5 en
Visual Studio esto pasa automáticamente (siempre compilan antes de correr); solo hace
falta prestar atención si se lanza un `.exe` ya compilado directamente.

### Migrar un volcado JSON existente a SQLite
`MonstersGame.Data.Sqlite.JsonToSqliteMigrator.Migrate(jsonDataRoot, dbPath)` lee el
formato anterior (un archivo por carta en `Cards/`, un archivo por mazo en `Decks/`, y
`Fusions/fusions.json`) y escribe una base nueva en `dbPath` (la sobrescribe si ya
existe). Es la misma herramienta que se usó para migrar el catálogo original del
Bloque 6 a SQLite; queda disponible para volver a sembrar una base desde un volcado
JSON en cualquier momento.

### Backend JSON alternativo (`Data/Loaders/JsonCardLoader.cs`, etc.)
Sigue existiendo y probado (`JsonCardLoaderTests`/`JsonCardWriterTests`), leyendo un
archivo `<id>__<slug>.json` por carta desde un directorio:
```json
{ "id": 1, "kind": "Monster", "name": "...", "attack": 3000, "defense": 2500,
  "level": 8, "type": "Dragon", "attribute": "Light", "description": "..." }
```
No es la fuente de datos activa del juego ni del editor desde el Bloque 9 (ambos usan
`SqliteCardLoader`/`SqliteCardWriter`), pero sigue siendo el formato que usan
`PackService.ExportCard`/`ImportCard` para exportar/importar una carta suelta — eso es
intercambio de datos entre catálogos, no almacenamiento del catálogo en sí, y JSON
sigue siendo el formato correcto para eso (portable, legible, diffable en control de
versiones si hace falta).

---

## Editor de cartas (`MonstersGame.CardEditor`)

App de escritorio independiente (WinForms) para editar el catálogo sin tocar SQL a
mano. Al arrancar busca `MonstersGame.sln` subiendo desde su carpeta de salida y abre
siempre `MonstersGame/Data/monstersgame.db` — la misma base que lee el juego (desde el
Bloque 9; antes era un directorio de archivos JSON, ver "Carga de datos" más arriba).

```powershell
dotnet run --project MonstersGame.CardEditor
```

**Qué permite:**
- **Crear / duplicar / eliminar / editar** cartas Monstruo, Mágica y Trampa, con
  validación en vivo (Id y nombre únicos, rangos de ATK/DEF/Nivel, `EffectId`
  registrado en `EffectRegistry`, enlace de Ritual a un Monstruo de Ritual existente,
  etc.) — **nunca deja guardar una carta inválida**; el botón Guardar se deshabilita
  y la lista de errores en pantalla explica qué falta corregir.
- **Vista previa** de la carta mientras se edita, con la misma paleta de colores y
  (desde el Bloque 11) el mismo arte real que usa el juego (`Graphics/Theme.cs`,
  `Data/Art/`) — no es el mismo renderer de MonoGame (un control WinForms no puede
  reutilizar `SpriteBatch`), pero muestra la misma información e imágenes. Desde el
  **Bloque 14**, el layout se acerca al de una carta física real (`fix/Carta.bmp` fue
  la referencia): el Nombre va dentro de su propio cuadro de fondo claro (para que
  resalte sobre el degradado de color de la carta), el arte tiene su propio marco, y
  toda la información textual — Atributo/Tipo (o Magia-Trampa/SubType) como título,
  la Descripción como cuerpo, y en Monstruos ATK/DEF como pie — vive dentro de un
  único cuadro de descripción con fondo claro y texto negro, igual que en una carta
  impresa.
- **Campo Imagen con botón "Examinar..."** al costado: abre el diálogo estándar de
  Abrir Archivo filtrado a imágenes en vez de tener que escribir el nombre del archivo
  a mano; guarda solo el nombre de archivo (no la ruta completa de la máquina local)
  para que el dato siga siendo portable.
- **Exportar / importar una carta individual** (formato JSON portable — no el mismo
  que usa el catálogo desde el Bloque 9, que es SQLite; ver "Backend JSON alternativo"
  más arriba) y **exportar / importar paquetes** (`.zip` con un `pack.json` de
  manifiesto + un JSON por carta) para compartir varias cartas a la vez.
- Al importar, si el Id ya existe en el catálogo local, pregunta si reasignar uno
  nuevo o cancelar — **nunca sobrescribe una carta existente en silencio**; toda carta
  importada se carga en el formulario para revisión y solo se persiste al pulsar
  Guardar.

La lógica no visual (`CardRepository`, `PackService`) tiene su propia suite de
pruebas en `MonstersGame.CardEditor.Tests` (26 casos: alta/edición/baja/duplicar de
cartas y de mazos, validar-antes-de-guardar, exportar/importar carta y paquete) — no
depende de WinForms, así que se prueba igual que cualquier otra lógica de `Core`/`Data`.

**Editor de Mazos (Bloque 12):** botón "Editor de Mazos..." al pie de la lista de
cartas, abre `DeckEditorForm` como ventana modal. Permite crear/renombrar/eliminar
mazos y armar su contenido eligiendo cartas del catálogo (doble clic o **Agregar >>**)
o quitándolas (doble clic o **<< Quitar**), con el mismo patrón de
validar-antes-de-guardar que el resto del editor: nombre único y no vacío, al menos 1
carta, y como máximo 3 copias de la misma carta. Guarda contra las mismas tablas
`Decks`/`DeckCards` de `monstersgame.db` que ya leía el juego — un mazo armado acá
aparece de inmediato en la Selección de Mazo del juego. No impone un tamaño mínimo o
máximo de mazo (el catálogo actual ya tiene mazos de 52 cartas, fuera del 40-60 "real"
del TCG original).

**Mantenimiento de Tipos de Monstruo (Bloque 13):** el campo Tipo (Dragón, Guerrero,
etc.) dejó de ser una lista fija del código — no tiene ninguna sinergia de reglas
propia, así que ahora es texto libre administrado desde el editor (tabla `Types` de
`monstersgame.db`), con un botón **"..."** al costado del combo de Tipo que abre
`TypeEditorForm`: agregar un Tipo nuevo, renombrar uno existente (actualiza todas las
cartas que lo tengan asignado) o eliminarlo (bloqueado si alguna carta del catálogo lo
está usando — primero hay que cambiarles el Tipo o renombrarlo). La base se siembra
la primera vez con los Tipos clásicos de *Forbidden Memories*, más **"DivineBeast"**
(Bestia Divina), que la lista original no tenía. Agregar un Tipo nuevo ya no exige
tocar código ni recompilar.

**Fuera de alcance** (no forma parte de la especificación aprobada): edición de
recetas de fusión — el editor gestiona cartas, mazos y Tipos, no fusiones. El Id de
una carta ya guardada no puede cambiarse desde el formulario (evita dejar huérfanas
referencias como `ritualMonsterId`); para reasignar un Id hay que eliminar y volver a
crear. **Atributo, Categoría y SubType siguen siendo enums fijos del código** (a
diferencia de Tipo) porque sí tienen sinergia de reglas o de arte real (Atributo trae
su propio ícono en `Data/Art/Attributes/`, agregar uno nuevo exigiría también el
archivo de arte correspondiente) — no es una laguna, es la línea que separa "metadato
puramente descriptivo" (Tipo) de "algo que el motor o el arte ya entienden".

**Bug de layout corregido:** los bloques de propiedades especificas de Monstruo/Magia/
Trampa (`_monsterGroup`/`_spellGroup`/`_trapGroup`) podian aparecer vacios o con los
campos amontonados en una columna angosta. La causa: un `GroupBox` con `Dock = None`
(el valor por defecto) que contiene una tabla con una columna de ancho "Percent" cae en
un calculo circular de WinForms — sin un ancho ya conocido desde el padre, la columna
Percent no tiene como calcular su ancho preferido, y `AutoSize` termina resolviendo un
ancho casi nulo. La correccion fue acoplar esos tres `GroupBox` con `Dock = DockStyle.Top`
(igual que ya hacia la tabla de campos comunes, que nunca tuvo el problema) para que el
ancho lo dicte el contenedor, no un calculo circular consigo mismo. De paso se corrigio
la posicion de la etiqueta "Solo para Magias de Ritual", que flotaba superpuesta arriba
de todo el bloque de Magia en vez de aparecer solo encima de los dos campos que son
especificos de Ritual.

---

## Reglas implementadas (según `Reglas MonstersGame.docx`)

> `Reglas MonstersGame.docx` es la especificación funcional vigente del proyecto.
> `Reglas.pdf` (reglamento completo del TCG, con Sincronía/Xyz/Péndulo/Enlace) se
> conserva solo como referencia histórica; donde ambos difieren, manda el docx.

- 8000 LP iniciales; mano inicial de 5 cartas.
- Estructura de turno con fases Draw / Main 1 / Battle / Main 2 / End.
- El **primer jugador no roba ni puede atacar en su primer turno**.
- **Invocación Normal / Colocación** una vez por turno; Invocación por Sacrificio:
  Nivel 5-6 = 1 sacrificio, Nivel 7+ = 2.
- Cambio de posición de batalla con sus restricciones.
- **Invocación por Volteo** como acción explícita (`FlipSummon`), separada del cambio
  de posición normal y del volteo implícito al ser atacado.
- **Resolución de combate** completa (ATK vs ATK, ATK vs DEF, ataque directo,
  volteo de cartas boca abajo, regla de 0 ATK).
- Límite de 6 cartas en mano al final del turno, con **descarte elegido por el
  jugador** (el turno queda en espera hasta que se elige qué descartar).
- **Selección manual de Sacrificios** al Invocar/Colocar un monstruo de Nivel 5+.
- Condiciones de victoria: LP a 0, *deck-out* o empate.
- **Fusión desde la mano** (2 materiales, sin Carta Mágica de Fusión) — es la regla
  propia de MonstersGame, no una desviación del reglamento original.
- **Zona de Campo** (1 espacio, reemplaza a la Carta Mágica de Campo anterior
  mandándola al Cementerio) y **Zona de Magia/Trampa** (5 espacios) jugables:
  Colocar boca abajo, activar desde la mano o desde una Colocación previa.
- **Subtipos de Magia** (Normal, Continua, de Equipo, de Campo, de Juego Rápido,
  de Ritual) y **de Trampa** (Normal, Continua, de Contraefecto), cada uno con su
  Velocidad de Hechizo y ventana de activación.
- **Una Trampa no puede activarse el mismo turno en que se Coloca**; una Magia sí.
- **Cadena y Prioridad reales**: activar una Magia o Trampa (Velocidad ≥ 2, o el
  primer eslabón de cualquier Velocidad) abre una Cadena; el adversario puede
  responder con una carta de Velocidad de Hechizo igual o mayor a la del eslabón
  anterior. Dos "paso" seguidos (uno de cada jugador) la cierran y la resuelven
  del último eslabón activado al primero. Mientras esté abierta, Invocar,
  Sacrificar, cambiar de fase, atacar y terminar el turno no son acciones válidas
  (tal como especifica el glosario del docx).
- **Sistema de efectos real**: Magias, Trampas y Monstruos de Efecto (Volteo) pueden
  ejecutar un efecto de verdad al resolverse — robar cartas, destruir un monstruo
  objetivo, revivir un monstruo desde el Cementerio propio, o negar la activación a
  la que responden (Contraefecto). El objetivo, si el efecto lo requiere, se elige
  al activar la carta, no al resolverla.
- **Invocación Ritual**: activa una Carta Mágica de Ritual junto con su Monstruo de
  Ritual correspondiente (ambos en la mano) y Sacrifica monstruos, de la mano y/o
  del Campo, cuya suma de Niveles alcance el mínimo exigido por la Carta Mágica.
  Es una Invocación Especial independiente de la Invocación Normal del turno.

Cobertura verificada por la suite de pruebas en `MonstersGame.Tests` (`dotnet test`).

---

## Decisiones de diseño confirmadas para MonstersGame

Ambigüedades de `Reglas MonstersGame.docx` resueltas explícitamente (no asumidas en
silencio) antes de tocar el motor:

1. **Fusión — resolución del resultado.** Modelo híbrido: recetas explícitas en
   `fusions.json` primero; si ninguna aplica, la fusión falla sin efectos secundarios
   ("no hay fusión posible"). Sin resultado genérico calculado por tipo en esta etapa.
2. **Fusión — número de materiales.** Se mantienen **2 materiales** (no se generaliza
   a N por ahora).
3. **Sin Deck Extra.** Los Monstruos de Fusión y de Ritual se materializan
   directamente desde el catálogo de cartas; no necesitan estar en el Mazo Principal
   ni en un Deck Extra separado.
4. **Xyz / Sincronía / Péndulo / Enlace: fuera de alcance.** La mención a "Rango" en
   el docx es residual de la fuente original y no implica soportar esas mecánicas.
5. **Lista Prohibida/Limitada.** Campo opcional por carta
   (`Restriction: None|SemiLimited|Limited|Banned`), fijable carta por carta desde el
   editor; sin una lista inicial poblada.
6. **Match (mejor de 3 Duelos).** Fuera de alcance de v1: se juega un Duelo único,
   como hasta ahora. El Match queda para una iteración posterior.
7. **Efectos de Volteo.** Entran en el primer incremento del sistema de efectos
   (junto con Cadenas, Magias y Trampas jugables), no en una fase posterior.

**Simplificaciones deliberadas** (documentadas, no silenciosas):
- **Catálogo de efectos acotado, vía código en vez de JSON declarativo.** El
  `EffectId` de una carta se resuelve contra `EffectRegistry` (la "vía de escape"
  del diseño original), no contra un formato declarativo en JSON. El editor de
  cartas (Bloque 6) deja **elegir** un `EffectId` ya registrado, con validación,
  pero no permite **autorizar/programar** efectos nuevos desde la interfaz — eso
  sigue exigiendo tocar `EffectRegistry` en código. El catálogo actual cubre 4
  acciones: robar cartas, destruir 1 monstruo objetivo, revivir 1 monstruo del
  Cementerio propio, y negar una activación — suficientes para demostrar el
  sistema de punta a punta, no para cubrir cualquier texto de carta real.
- **Solo Normal/Trampa/Contraefecto y Volteo ejecutan efecto real.** Las Magias
  Continuas, de Equipo y de Campo se modelan (permanecen boca arriba en el Campo)
  pero no aplican ningún bono ni efecto continuo real — eso exige poder recalcular
  ATK/DEF dinámicamente y detectar cuándo una carta "deja el Campo", una pieza de
  arquitectura mayor que queda para una iteración posterior.
- **Cartas Mágicas de Campo y la Invocación Ritual no entran en la Cadena.** Igual
  que en el Bloque 4: se activan/resuelven al instante. Tampoco se admite Colocar
  una Magia de Campo boca abajo, ni Sacrificar la propia Magia/Monstruo de Ritual.
- **`Renacer del Monstruo` solo revive desde tu propio Cementerio**, y siempre en
  Posición de Ataque (el real permite elegir Cementerio rival y Ataque/Defensa).
- **La CPU nunca inicia una Cadena ni una Invocación Ritual.** Sigue sin jugar
  Magias/Trampas por su cuenta; solo *responde* cuando el humano abre una Cadena
  (siempre pasa, porque nunca tiene nada Colocado con qué responder). El motor y
  la IA ya están preparados para el día en que la IA sepa jugar estas cartas.
- **Sin ventana de respuesta específica al declarar un ataque.** Ver Bloque 4.
- **El Cementerio propio sigue sin caja dedicada en el tablero.** El pase visual
  del Bloque 7 le dio caja a la Zona de Magia/Trampa y a la Zona de Campo (las
  zonas que ese bloque tenía a cargo); elegir un objetivo en el Cementerio propio
  (p. ej. "Renacer del Monstruo") sigue usando el explorador de texto **SIGUIENTE
  CEMENTERIO** / **CONFIRMAR OBJETIVO**, sin una caja de Cementerio en pantalla.
- **Un monstruo propio boca abajo no revela su identidad en el tablero, ni siquiera
  al dueño** (simplificación previa al Bloque 7, sin tocar). Es asimétrico respecto
  a las Zonas de Magia/Trampa, que desde el Bloque 7 sí revelan al dueño su propia
  carta Colocada — la asimetría es deliberada (alcance del bloque: solo las zonas
  nuevas), no un descuido.

Pendiente de implementar (no son desviaciones de regla, son trabajo no iniciado):
efectos Continuos/de Equipo/de Campo reales, heurística ofensiva de la IA para
Magias/Trampas y Ritual, caja de Cementerio en el tablero. El detalle completo
(matriz de adaptación, arquitectura, modelo de cartas, roadmap) vive en el documento
de análisis de la adaptación.

---

## Recursos gráficos

Desde el **Bloque 11**, las cartas usan **arte real** (imagen por carta, insignia por
Atributo, estrellas de Nivel), con degradación elegante a los placeholders del
Bloque 10 cuando falta un archivo — nunca una carta se rompe por no tener imagen.

### Arte real (Bloque 11)
- **Imagen de carta**: `MonsterCard.Image`/`Card.Image` (el campo que se completa desde
  el editor, ver "Editor de cartas") apunta a un archivo en `Data/Art/Cards/`; se
  dibuja como miniatura en el hueco entre el nombre y ATK/DEF.
- **Insignia de Atributo**: un PNG por Atributo de Monstruo en `Data/Art/Attributes/`
  (`Oscuridad.png`, `Luz.png`, `Tierra.png`, `Fuego.png`, `Agua.png`, `Viento.png`,
  `Divinidad.png`), en la esquina donde antes iba el icono procedural.
- **Estrellas de Nivel**: un único `Data/Art/Level/level.png`, dibujado tantas veces
  como indique el Nivel de la carta (tamaño de cada estrella ajustado para que el
  Nivel más alto siga entrando en el ancho de la carta).
- Se cargan con `Texture2D.FromStream` directo sobre el PNG (`Graphics/TextureCache.cs`,
  con cache en memoria por ruta) — **sin Content Pipeline** (mgcb): ningún paso de
  compilación previo, coherente con cómo ya se generaban la fuente y los placeholders.
- **Si un archivo falta o no se puede leer**, se degrada al placeholder correspondiente
  del Bloque 10 (icono procedural de Atributo, o simplemente sin miniatura) — nunca
  interrumpe el juego ni el editor.
- La vista previa del editor (`CardPreviewControl`, GDI+) dibuja el mismo arte real con
  `Image.FromFile` (más grande, con más espacio disponible que en el tablero), para que
  "cómo se ve acá" y "cómo se ve jugando" sigan sin contradecirse.

### Origen de los archivos y saneamiento necesario
Los PNG que llegaron para este bloque incluían casos que ni MonoGame ni GDI+ pueden
leer tal cual: 35 de las 36 imágenes de carta y las 7 insignias de Atributo eran en
realidad **WebP con la extensión cambiada a `.png`** (típico de guardar una imagen
directo desde el navegador, que a veces sirve WebP aunque la URL diga `.png`), y una
imagen de carta era un **JPEG** con la misma extensión falsa. Se convirtieron todas a
PNG real con una herramienta de un solo uso (`SixLabors.ImageSharp`, igual patrón que
el migrador JSON→SQLite del Bloque 9) antes de integrarlas. De paso, las imágenes de
carta llegaron hasta 1792x1792 para mostrarse en una miniatura de ~90x60 píxeles — se
redujeron a un máximo de 256px por lado (conservando la relación de aspecto), sin
pérdida perceptible a los tamaños en que realmente se muestran, y bajando el peso
total de `Data/Art/` a ~5 MB.

### Placeholders procedurales (Bloque 10, siguen siendo el respaldo)
- Cartas como paneles con **degradado vertical** (más claro arriba, más oscuro abajo),
  coloreados por Atributo (Monstruos) o por Magia/Trampa.
- Una **insignia de icono de 7x7 píxeles** por Atributo de Monstruo y por Magia/Trampa,
  generada igual que la fuente (`Graphics/IconAtlas.cs`: patrón de texto → atlas de
  textura en tiempo de ejecución) — Magia/Trampa no tienen arte real todavía, así que
  siguen usando siempre este icono.
- Nombre y ATK/DEF con la **fuente de mapa de bits 5x7 generada por código**.

## Sonido y animaciones (Bloque 10)

**Audio:** los efectos de sonido se **sintetizan por código en tiempo de ejecución**
(`Audio/SoundSynth.cs`: genera PCM de 16 bits — tonos con barrido de frecuencia,
arpegios de varias notas — y los envuelve en un `SoundEffect` de MonoGame), sin
archivos de audio externos. `AudioManager.PlaySfx(key)` ya no es un no-op: reproduce
uno de 8 efectos (`summon`, `set`, `attack`, `damage`, `chain`, `error`, `win`, `lose`)
enganchados a las acciones correspondientes en `DuelScreen`. **No hay música de
fondo**: sintetizar música con calidad razonable no es viable por código (a diferencia
de un efecto corto de una nota), así que `PlayMusic`/`StopMusic` siguen siendo no-op a
propósito — no es una laguna, es la decisión de alcance que se tomó para este bloque.

**Animaciones:** puramente cosméticas, detectadas comparando el estado del motor
frame a frame contra el frame anterior (`DuelScreen.UpdateAnimations`) — así cubren
tanto las acciones del jugador humano como las que decide la CPU, sin necesitar un
gancho en cada punto de llamada del motor:
- La **barra de LP** ya no salta de golpe al nuevo valor: se anima suavemente
  (~6000 LP/segundo) y destella en rojo un instante cuando el jugador pierde vida.
- Una **Zona de Monstruos** destella en blanco brevemente al pasar de vacía a
  ocupada, o al voltearse boca arriba (Invocación, Colocación o Volteo, propio o de
  la CPU).
- Declarar un ataque dispara un **"lunge"** breve (el atacante se desplaza hacia el
  objetivo y vuelve, curva de seno) — con dirección exacta hacia el objetivo cuando lo
  decide el humano (se sabe en el momento del clic), y un empujón genérico hacia el
  rival cuando lo decide la CPU (se detecta por diferencia, sin esa información).

**Límite de esta verificación:** no hay forma de confirmar en este entorno que el
audio generado *suena* bien (solo que `AudioManager` se construye sin excepciones al
arrancar, y que el juego sigue funcionando con las llamadas a `PlaySfx` activas), ni
de ver renderizadas las animaciones — son la capa de presentación de MonoGame, sin
automatización de UI disponible aquí. Si algo no se ve o no suena como se espera,
avisen y se ajusta.

---

## Próximas iteraciones (roadmap de adaptación a MonstersGame)

1. ~~Separar `Core`/`Data` en proyectos propios y levantar `MonstersGame.Tests`.~~ — hecho.
2. ~~Descarte elegido por el jugador, selección manual de sacrificios en la UI,
   Invocación por Volteo explícita.~~ — hecho. La Zona de Campo se movió al Bloque 3
   (ver más arriba).
3. ~~Zona de Campo, Cartas Mágicas y de Trampa jugables (Colocar/activar, SubTypes).~~ —
   hecho, con las simplificaciones documentadas más arriba (sin efecto real, sin Campo
   boca abajo, sin respuesta en el turno rival, Ritual rechazado).
4. ~~Cadena y Prioridad.~~ — hecho: `DuelState.Chain`, Velocidad de Hechizo real con
   gating por eslabón, resolución LIFO, y la CPU ya puede recibir Prioridad aunque no
   sea su turno (con las simplificaciones documentadas más arriba).
5. ~~Sistema de efectos e Invocación Ritual.~~ — hecho: `MonstersGame.Core.Effects`
   (`IEffectAction`, `EffectRegistry`, 4 acciones), efecto de Volteo, y
   `DuelEngine.RitualSummon`, con las simplificaciones documentadas más arriba
   (catálogo acotado, sin efecto Continuo/Equipo/Campo real, sin Ritual por la CPU).
6. ~~Editor de cartas.~~ — hecho: `MonstersGame.CardEditor` (WinForms), catálogo
   migrado a un archivo por carta (`Data/Cards/<id>__<slug>.json`), validación
   compartida con el juego (`CardDtoValidator`/`CardDtoMapper`), exportar/importar
   cartas y paquetes `.zip`. Fuera de alcance: edición de mazos y de recetas de
   fusión (ver más arriba).
7. ~~Pase visual: HUD, tablero con las nuevas zonas, prompts de Cadena.~~ — hecho:
   la Zona de Magia/Trampa (5 casillas) y la Zona de Campo (1 casilla) de cada
   jugador tienen su propia caja en el tablero (`CardRenderer.DrawSpellTrapZoneCard`/
   `DrawFieldZone`), activables con un clic directo; un panel de Cadena persistente
   en la pantalla de Duelo muestra los eslabones en curso en orden de resolución
   (LIFO); el HUD se reordenó para que todo (HUD de ambos jugadores, tablero,
   panel de Cadena, registro, mano y controles) quepa en los 1280x720 sin
   solapamientos. Fuera de alcance: caja de Cementerio (ver simplificaciones) y
   arte final (sigue siendo placeholders, ver más abajo).
8. ~~Cierre: cobertura de pruebas completa, validación final contra la matriz de
   trazabilidad.~~ — hecho: se revisó la matriz de trazabilidad del documento de
   análisis (varias filas "Pendiente" databan de antes de que el Bloque correspondiente
   se implementara y ya tenían una fila "Probado" duplicada más abajo apuntando a la
   prueba real — se limpiaron; solo el Efecto Continuo sigue genuinamente fuera de
   alcance, reetiquetado como tal en vez de "Pendiente"). Se agregó
   `MonstersGame.CardEditor.Tests` (17 pruebas) para `CardRepository` y `PackService`
   — la única lógica no trivial del editor que todavía no tenía prueba automatizada
   (no depende de WinForms, así que se prueba como cualquier otra clase de Core/Data).
   Total final: **170 pruebas** en 3 proyectos, todas en verde.
9. ~~Persistencia con SQLite.~~ — hecho: el catálogo (cartas, mazos, fusiones) vive en
   una base SQLite única (`Data/monstersgame.db`) en vez de un directorio de archivos
   JSON; `SqliteCardLoader`/`SqliteCardWriter`/`SqliteDeckLoader`/`SqliteFusionLoader`
   implementan las mismas interfaces que ya usaba el backend JSON (que se conserva,
   probado, como alternativa — ver "Carga de datos" más arriba), y
   `JsonToSqliteMigrator` migró el catálogo existente sin pérdida de datos. El editor
   de cartas también pasó a leer/escribir contra la base SQLite. Un bug real se
   detectó y corrigió gracias a la nueva suite de pruebas: el migrador intentaba
   borrar la base anterior antes de volver a escribirla, pero el *connection pooling*
   de `Microsoft.Data.Sqlite` mantenía el archivo bloqueado incluso después de cerrar
   la conexión — hacía falta vaciar el pool explícitamente
   (`SqliteConnection.ClearAllPools()`) antes del borrado. 20 pruebas nuevas en
   `MonstersGame.Data.Tests` (loaders, escritor, migrador); `CardRepositoryTests` del
   editor se adaptó a la nueva base sin perder cobertura (una prueba dejó de aplicar:
   "renombra el archivo viejo" ya no tiene sentido cuando editar el nombre es
   simplemente un `UPDATE` por Id, no un archivo que renombrar).
10. ~~Arte, animaciones y audio.~~ — hecho, con el alcance que decidiste: arte y audio
    siguen siendo **generados por código** (ni arte ilustrado ni archivos de audio
    reales), con más trabajo de diseño encima — degradados e insignias de icono por
    Atributo en las cartas, y efectos de sonido sintetizados en tiempo de ejecución
    para las acciones clave del Duelo. Ver "Recursos gráficos" y "Sonido y
    animaciones" más arriba para el detalle completo, incluyendo qué quedó
    deliberadamente fuera (música de fondo, iconos en la vista previa del editor).
11. ~~Arte real de cartas.~~ — hecho: al conseguir imágenes reales (arte por carta,
    insignia por Atributo, estrella de Nivel repetible), se reemplazó lo procedural del
    Bloque 10 por PNG reales cargados directo (`Graphics/TextureCache.cs` en el juego,
    `Image.FromFile` en la vista previa del editor), con degradación automática al
    placeholder si un archivo falta. Las imágenes recibidas necesitaron saneamiento
    antes de poder usarse: la mayoría eran WebP/JPEG con la extensión cambiada a
    `.png` (ni MonoGame ni GDI+ pueden leer eso), y algunas medían hasta 1792x1792
    para mostrarse en una miniatura de ~90x60 — ver "Recursos gráficos" más arriba
    para el detalle de la conversión y el redimensionado. De paso se corrigió un bug de
    layout real en el editor (bloques de propiedades que se veían vacíos o amontonados)
    y se agregó un botón "Examinar..." para elegir la imagen de una carta con el
    diálogo estándar de Windows en vez de escribir la ruta a mano.
12. ~~Previsualización de mazo y Editor de Mazos.~~ — hecho: la Selección de Mazo del
    juego ahora muestra, al pasar el mouse sobre un mazo, la lista de sus cartas
    (miniatura, Nivel/Tipo, ATK/DEF, insignia de Atributo o de Magia/Trampa), al estilo
    de la lista de mazo de *Yu-Gi-Oh! Forbidden Memories* — con paginación por
    teclado, botones o rueda del mouse (`InputManager.ScrollDelta`, nuevo). El editor
    de cartas ganó un Editor de Mazos (`DeckEditorForm`, ventana modal): crear/
    renombrar/eliminar mazos y armar su contenido desde el catálogo, con la misma
    validación de nunca-guardar-algo-inválido del resto del editor (nombre único,
    al menos 1 carta, máximo 3 copias por carta) — guarda contra las mismas tablas
    `Decks`/`DeckCards` que ya leía el juego, así que un mazo armado en el editor
    aparece de inmediato en el juego. Nuevo `SqliteDeckWriter` (antes solo había
    lector de mazos) más 17 pruebas nuevas (7 en `MonstersGame.Data.Tests`, 10 en
    `MonstersGame.CardEditor.Tests`). De paso se activó `PRAGMA foreign_keys = ON`
    en toda conexión SQLite del proyecto, para que el `ON DELETE CASCADE` de
    `DeckCards` (declarado desde el Bloque 9 pero nunca antes verificado) realmente
    se cumpla al borrar un mazo.
13. ~~Mantenimiento de Tipos de Monstruo.~~ — hecho: al notar que faltaba "Bestia
    Divina" en el Tipo de un Monstruo, y que agregarlo exigía tocar código y
    recompilar, se convirtió `MonsterCard.Type` de un enum fijo (`MonsterType`,
    eliminado de `MonstersGame.Core`) a texto libre validado solo contra "no vacío"
    — es un campo puramente descriptivo, sin ninguna sinergia de reglas, a diferencia
    de Atributo/Categoría/SubType (que siguen siendo enums fijos a propósito). Nueva
    tabla `Types`, sembrada la primera vez con los Tipos clásicos más "DivineBeast"
    (que la lista original no tenía); `TypeEditorForm` en el editor (botón "..." junto
    al combo de Tipo) para agregar/renombrar/eliminar Tipos sin tocar código —
    renombrar actualiza en cascada toda carta que use ese Tipo, y no se puede eliminar
    uno que este en uso. Un bug real se detectó y corrigió gracias a la nueva suite de
    pruebas: en una base nueva, leer los Tipos antes de que existiera el archivo
    devolvía una lista vacía en vez de sembrar los valores por defecto — a diferencia
    de Cartas/Mazos (donde "no existe la base" es legítimamente "catálogo vacío"),
    Tipos necesita sembrarse siempre, o ni la primera carta se podría crear. 17
    pruebas nuevas (9 en `MonstersGame.Data.Tests`, incluida la que atrapó el bug
    de arriba; 8 en `MonstersGame.CardEditor.Tests`).
14. ~~Rediseño de la vista previa de carta.~~ — hecho: tomando `fix/Carta.bmp` (una
    carta real) como referencia, se rehizo `CardPreviewControl` del editor para que el
    Nombre viva dentro de su propio cuadro con fondo claro (antes era texto suelto
    sobre el degradado de color de la carta, poco legible), el arte tenga su propio
    marco, y toda la información — título Atributo/Tipo (o Magia-Trampa/SubType),
    cuerpo con la Descripción, y pie ATK/DEF en Monstruos — viva en un único cuadro de
    descripción con fondo claro y texto negro, en vez de líneas de texto sueltas de
    distintos colores desperdigadas por la carta. Es un cambio puramente visual (WinForms
    `GDI+`, `CardPreviewControl.OnPaint`); no toca `CardRepository` ni ninguna lógica de
    negocio, así que no requirió pruebas nuevas — las 223 existentes siguen cubriendo
    todo lo que cubrían antes.
15. ~~Ventana redimensionable/maximizable.~~ — hecho: `Window.AllowUserResizing = true`
    en `MonstersGameApp`, más un lienzo virtual fijo de 1280x720 (`RenderTarget2D`) que
    se escala y centra (letterbox, con barras negras si la proporción de la ventana no
    es 16:9) al volcarse al back buffer real, que ahora se ajusta al tamaño de la
    ventana en cada `Window.ClientSizeChanged`. Ninguna pantalla (`MainMenuScreen`,
    `DeckSelectionScreen`, `DuelScreen`, `ResultScreen`) tuvo que tocarse: todas siguen
    dibujando en el mismo espacio de 1280x720 de siempre. Lo único que sí necesitaba
    saber del nuevo escalado era el mouse — `InputManager` ahora transforma la posición
    cruda del cursor (píxeles reales de ventana) al espacio virtual antes de exponerla
    (`MousePosition`, `IsHovering`, `ClickedIn`, `RightClickedIn`), así que clics y hover
    siguen funcionando igual sea cual sea el tamaño o la posición de la ventana. Cambio
    de motor/presentación puro; no toca reglas de juego ni datos, así que no requirió
    pruebas nuevas — las 223 existentes siguen en verde.
16. ~~Combo para el `EffectId` en el editor.~~ — hecho: antes, al crear una carta de
    Magia/Trampa/Monstruo, `EffectId` era una caja de texto libre donde había que
    escribir de memoria un código exacto (p. ej. `destroy_target_monster`) — sin
    saber cuáles existían, y con `CardDtoValidator` rechazando el guardado recién al
    escribir algo mal. Los tres campos (`_monsterEffectCombo`, `_spellEffectCombo`,
    `_trapEffectCombo` en `MainForm`) pasaron de `TextBox` a `ComboBox`
    (`DropDownList`), poblados desde `EffectCatalog.Options`: un nombre legible más el
    código real entre paréntesis (p. ej. "Destruir 1 monstruo objetivo
    (destroy_target_monster)"), con "(Sin efecto)" siempre como primera opción. Se
    agregó `EffectRegistry.RegisteredIds` en `MonstersGame.Core` para que la lista de
    ids válidos tenga una única fuente de verdad (la misma que ya usaba
    `CardDtoValidator`): el catálogo de nombres del editor no puede desincronizarse
    de los efectos realmente implementados, y un id nuevo sin nombre asignado todavía
    aparece en la lista (mostrando el código crudo) en vez de quedar invisible.
    Cambio de presentación puro — no toca `CardDtoValidator` ni `EffectRegistry.Get`,
    así que no sumó pruebas nuevas.
17. ~~Posiciones de carta y choque de batalla estilo Forbidden Memories.~~ — hecho, en
    dos partes, ambas guiadas por `Yu-Gi-Oh! - Forbidden Memories` como referencia
    explícita:
    - **Orientación por Posición.** Un Monstruo en Defensa (boca arriba o Colocado
      boca abajo) ahora se dibuja rotado 90° y un poco más chico, en vez de la misma
      carta vertical de siempre con solo una etiqueta "DEF" en la esquina — la
      orientación misma comunica Ataque/Defensa, como en el juego real. La rotación
      se hace con una matriz de transformación de `SpriteBatch` (`CardRenderer.DrawTransformed`)
      alrededor del centro de la carta, escalada para que quepa sin invadir las
      zonas vecinas — no hizo falta rotar cada trazo (panel, texto, arte, insignias)
      por separado. De paso, el reverso de una carta Colocada dejó de ser el
      placeholder gris con un "?": ahora usa la imagen real `Data/Art/Cards/CardBack.jpg`
      que compartiste (`TextureCache.CardBack`), degradando al placeholder solo si el
      archivo faltara. El `.csproj` del juego copiaba antes solo `*.png` a la salida;
      se sumó un patrón para `*.jpg` porque este es el primer arte que no es PNG.
    - **Choque de batalla.** Al declararse un ataque, el atacante y el objetivo
      convergen hacia el punto medio entre sus dos Zonas, se sostienen un instante
      (reutilizando el destello blanco de Zona del Bloque 10 como "impacto") y
      vuelven; el o los monstruos destruidos se encogen en su lugar en vez de
      desaparecer de golpe. Esto reemplaza el "lunge" genérico del Bloque 10 (un
      simple empujón hacia el rival, sin saber si había un monstruo objetivo ni si
      algo se destruía). El obstáculo real: `DuelScreen` no tiene forma de saber,
      solo mirando el estado del motor, *qué* Zona fue el objetivo de un ataque de
      la CPU cuando nada se destruye (nada cambia para poder detectarlo por
      diferencia) — a diferencia de un clic del humano, donde la pantalla ya conoce
      las dos Zonas de antemano. Se resolvió con un agregado mínimo y aditivo al
      motor: `DuelState.LastAttack` (tipo `AttackInfo`, nuevo), que `DuelEngine.DeclareAttack`
      completa con exactamente qué Zonas participaron, las cartas involucradas y qué
      se destruyó — el mismo principio que ya tenía `DuelState.Log` (contarle a la
      presentación qué pasó, sin que ninguna regla dependa de ello). Con eso,
      `DuelScreen.DetectAttacks` detecta *cualquier* ataque (humano o CPU) con un
      único camino de código, comparándolo contra el último que ya vio — mismo
      patrón de diferencia frame a frame que el resto de estas animaciones, pero
      leyendo un dato que el motor ya calculó en vez de inferirlo mirando las Zonas.
      3 pruebas nuevas en `MonstersGame.Tests` verifican que `LastAttack` quede bien
      poblado (ataque a objetivo con destrucción, ataque directo sin objetivo, y
      ataque contra una carta Colocada que se voltea a mitad de la resolución).

**Total final: 226 pruebas** en 4 proyectos de prueba, sobre una solución de 7
proyectos que compila sin advertencias.
