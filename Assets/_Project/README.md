# Survival Prototype

Prototipo jugable de supervivencia/exploración en tercera persona. Región de prueba de 5×5 km,
generada por seed determinista. Prioridad: **gameplay y arquitectura sobre gráficos**.

## Puesta en marcha (proyecto recién clonado o recién creado)

1. Abrir el proyecto en Unity 6000.3.23f1. Esperar a que el Package Manager resuelva URP.
2. Menú `Survival > Setup > Run Full Setup`. Crea el asset de URP, los assets de mundo,
   el material de terreno y la escena `Assets/_Project/Scenes/Prototype.unity`.
3. Abrir esa escena y darle a Play.

## Controles

Esquema de action RPG en tercera persona: el ratón manda sobre la cámara, y el personaje encara
hacia donde se desplaza.

| Entrada | Qué hace |
| --- | --- |
| Ratón | Orbita la cámara. El personaje no la arrastra nunca. |
| `WASD` | Mover, **relativo a la cámara**. |
| `Shift` | Sprint. Consume stamina. |
| `Espacio` | Saltar. Soltarlo pronto corta el salto. |
| Rueda | Zoom, de 2,5 m a 7 m. |
| `E` | Interactuar. `Esc` suelta el cursor, click lo recupera. |

En el editor hay un panel de ajuste en vivo (`F1`) sobre la cámara, que se compila fuera de una
build de release.

## Escenas

| Escena | Para qué |
| --- | --- |
| `Scenes/Prototype.unity` | Región ficticia: terreno por seed, streaming, 5×5 km. |
| `Scenes/MovementTest.unity` | Sala de pruebas: suelo a cuadros de 2 m, rampas, escalones, pilares, un pasillo estrecho y un techo. Para juzgar movimiento, cámara y colisiones sin que el terreno procedural meta ruido. |
| `Scenes/RealWorld.unity` | Rosario real: elevación SRTM y huellas de OpenStreetMap. Mismo streamer, mismo mesher, mismo LOD — solo cambia la fuente de altura. |

## Datos geográficos reales

El módulo `Survival.GeoData` sustituye **solo la entrada** del pipeline. `WorldStreamer` tiene un
campo `Height Source Override` de tipo `TerrainHeightSourceAsset`; si está vacío usa ruido, y si
tiene un `GeoDataset` usa elevación real. Nada aguas abajo se entera.

Los datos se descargan **una vez** con las herramientas de `scratchpad/` y se cachean como JSON en
`GeoData/Source/`, y de ahí a un asset con `Survival > Geo > Import Rosario Dataset`. El juego
nunca toca la red: funciona sin internet, sigue siendo determinista, y no machaca dos APIs
públicas gratuitas cada vez que alguien pulsa Play.

Fuentes: elevación de [opentopodata.org](https://www.opentopodata.org) (dataset `srtm30m`),
vectores de [OpenStreetMap](https://www.openstreetmap.org/copyright) vía Overpass, ambos bajo ODbL.

### Alturas de edificios: cuatro fuentes evaluadas

Medidas contra los 281 edificios de Rosario que declaran altura en OSM:

| Fuente | Cobertura | Veredicto |
| --- | --- | --- |
| OpenStreetMap (tags) | 3,8 % | Dato humano real. Se usa siempre que existe |
| GHSL / OpenBuildingMap | 100 % | ~27 m casi uniformes: sobreestima 1,8× en el centro y 3,6× en la periferia. **Descartada** |
| Microsoft GlobalMLBuildingFootprints | 0 % en Argentina | Ni una altura en 330.923 edificios del tile, y 2.548 huellas contra 7.454 de OSM. **Descartada** |
| **Google Open Buildings 2.5D** | **99 %** | Correlación log 0,74; 19 de los 30 más altos reales están en su top-30. Sobreestima ~2,5× de forma **uniforme**, así que se recalibra |

Google es la única que sabe *cuáles* edificios son altos. En error mediano empata con adivinar,
porque la mediana la dominan las casas bajas — pero en la banda de 30 m o más el error cae de
35,2 m a 14,2 m, y ahí es donde vive la silueta.

Reparto actual en Rosario: **Google 7258 · OSM 174 · estimación 22**.

#### Cobertura mundial: las dos fuentes son complementarias

Google 2.5D cubre solo el Sur Global. Comprobado contra sus propios manifiestos de tiles:

| Ciudad | Google 2.5D | Altura en tags de OSM |
| --- | --- | --- |
| Rosario, Buenos Aires, México DF | Cubierta | 7,3 % |
| Lagos, Delhi, Yakarta | Cubierta | — |
| Madrid | **Sin cobertura** | 23,1 % |
| Berlín | **Sin cobertura** | 71,1 % |
| Nueva York | **Sin cobertura** | 87,9 % |
| Tokio, Sídney | Sin manifiesto | — |

Donde Google no llega, OSM está bien etiquetado: son comunidades de mapeo maduras. Y donde OSM
es pobre, Google cubre. Por eso la cadena es **OSM → Google → estimación propia**, y no hace
falta una fuente única global.

El hueco real es el **sur de Europa**: Madrid queda con 23 % de OSM y sin Google. Ahí habría
que mirar datos catastrales nacionales, que en España son públicos.

### Límites conocidos

- SRTM tiene ~30 m de resolución y la rejilla guardada ~39 m, contra 2,7 m entre vértices del
  mesh. `GeoHeightSource` interpola y añade ruido **por debajo** de la resolución del dato para
  que no se vean facetas. Inventa detalle, pero solo detalle que el dato nunca tuvo.
- El 90% de los edificios de Rosario están etiquetados solo `building=yes` y caen en el arquetipo
  `Unknown`. Los arquetipos concretos son unos pocos cientos de hitos, no el grueso de la ciudad.
- Overpass devuelve las vías **enteras** que cruzan el bounding box, así que hay geometría hasta
  1,2 km fuera de la región. Cae sobre terreno extrapolado plano.
- `GeoFeatureSpawner` dibuja volúmenes placeholder, **no** es el sistema modular de edificios.
  Cuando ese exista, consume el mismo `GeoDataset`: las huellas reales ya están guardadas.

## Arquitectura

Cuatro assembly definitions, con dependencias en una sola dirección:

```
Survival.Core  <-- Survival.World
      ^        <-- Survival.Player
      |
Survival.Editor (solo editor, referencia a todos)
```

`Player` **no** referencia a `World`. Se comunican por interfaces declaradas en `Core`
(`ITerrainSampler`) resueltas vía `ServiceRegistry`. Cada sistema nuevo (cuevas, edificios,
criaturas, inventario, combate, descubrimientos) entra como su propio assembly con la misma regla.

### Core

| Pieza | Para qué |
| --- | --- |
| `DeterministicHash`, `SeedStream` | Aleatoriedad reproducible. No se usa `UnityEngine.Random` ni `Mathf.PerlinNoise`: ninguno garantiza el mismo resultado entre versiones de Unity o plataformas, y "misma seed = mismo mundo" tiene que sobrevivir a ambas cosas. |
| `GradientNoise`, `FractalNoiseSettings` | Ruido de gradiente propio, construido sobre el hash anterior. |
| `ChunkCoord` | Dirección entera de una celda del mundo. |
| `GeoCoordinate`, `IWorldProjection`, `FlatWorldProjection` | Puente hacia el plan largo: cualquier posición del mundo ya se puede expresar como lat/lon reales. |
| `WorldContext` | Seed + proyección. Se pasa explícitamente, no por singleton, para que la generación pueda correr en hilos. |
| `ServiceRegistry` | Registro tipado mínimo para desacoplar assemblies. |

### World

| Pieza | Para qué |
| --- | --- |
| `WorldSettings` | Todos los parámetros de streaming y tamaño de región. |
| `RegionDefinition` / `RegionSnapshot` | Una región. El snapshot es C# puro: es lo que hace legal generar fuera del hilo principal. |
| `IRegionProvider` | "¿En qué región está esta posición?". Hoy responde siempre lo mismo. |
| `ITerrainHeightSource` / `ProceduralHeightProvider` | Altura del suelo. Función pura de la seed. |
| `ChunkMeshBuilder` | Construye la malla de un chunk. Estático, sin estado, sin tocar objetos nativos de Unity. |
| `WorldStreamer` | Carga, descarga y LOD alrededor del jugador. |

### Decisiones que conviene no deshacer sin motivo

- **Mallas propias por chunks, no Unity Terrain.** Unity Terrain no hace streaming real, ata la
  generación a un heightmap sobre un plano, y habría que tirarlo para el mundo real metro a metro.
- **Construcción en hilos de trabajo.** Todo lo que construye chunks es función pura de su
  `ChunkBuildRequest`. Solo la subida de la malla a la GPU ocurre en el hilo principal, y está
  racionada por frame. Por eso ningún generador puede leer un `ScriptableObject` directamente.
- **Normales analíticas con epsilon fijo.** No dependen del LOD, así que un vértice en el borde de
  un chunk recibe la misma normal desde ambos lados y en cualquier LOD. Eso es lo que elimina las
  costuras de iluminación.
- **Faldones (skirts) en vez de coser LODs.** Dos chunks vecinos a distinto LOD no muestrean el
  borde con el mismo espaciado; el faldón tapa la grieta sin lógica de stitching.
- **Colores por vértice, no texturas.** El terreno se lee bien sin arte, y se tira sin coste cuando
  llegue el arte de verdad.
- **Input Manager clásico detrás de `IInputProvider`.** Funciona en un proyecto recién creado, sin
  paquete extra ni reinicio del editor. Cambiar al Input System es escribir otra implementación.

## El plan largo (preparado, no implementado)

La intención es que la región ficticia se pueda reemplazar por la Tierra real sin reescribir los
generadores. Los ganchos ya están puestos:

- `GeoCoordinate.ToSeed()` deriva la seed de coordenadas geográficas en vez de un índice arbitrario.
- `WorldContext.GeoSeedFor()` es la variante geográfica de `SeedFor()`, con el mismo contrato.
- `IWorldProjection` aísla el hecho de que hoy el mundo es un plano tangente local. Una versión
  ECEF con origen flotante entra por ahí.
- `ITerrainHeightSource` aísla de dónde viene la altura. Una implementación respaldada por tiles
  SRTM, mezclada con ruido por debajo de la resolución del dato, entra por ahí.
- `IRegionProvider` aísla el reparto de regiones. Un proveedor que resuelva por latitud, clima y
  cobertura del suelo entra por ahí.

## Qué falta

Sistemas todavía no escritos, en el orden en que tiene sentido atacarlos:
vegetación → edificios modulares → cuevas → recursos y loot → criaturas → inventario → combate →
descubrimientos → HUD.
