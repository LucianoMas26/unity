# Proyecto: Mundo Real Procedural en Tercera Persona

## 1. Visión general
Quiero desarrollar un videojuego de supervivencia/exploración en tercera persona ambientado en un mundo basado en el mundo real.
La idea principal es utilizar datos geográficos reales para construir un mundo gigantesco y explorable, similar conceptualmente a cómo Microsoft Flight Simulator representa el mundo real, pero trasladado a un videojuego terrestre en tercera persona.
El jugador debe poder recorrer ciudades, calles, parques y zonas naturales basadas en ubicaciones reales.
El mundo debe ser escalable a enormes dimensiones mediante streaming y generación bajo demanda.
NO quiero intentar crear manualmente todo el mundo.
La filosofía del proyecto debe ser:
Datos reales + generación procedural + streaming + estética propia + gameplay.

## 2. Tecnología principal
Motor:
- Unity 6 o superior
- C#
- Cesium for Unity
- Unity 3D Tiles / streaming geoespacial
- OpenStreetMap u otras fuentes de datos geográficos cuando corresponda
- Unity MCP para permitir que agentes de IA trabajen directamente sobre el proyecto

El proyecto debe estar diseñado desde el principio para poder crecer progresivamente.

## 3. Mundo real
Quiero utilizar Cesium como una de las tecnologías principales para representar el mundo real.
El mundo debe tener: terreno real, ubicación geográfica real, calles reales, edificios basados en datos reales, parques, cuerpos de agua, carreteras, ciudades, zonas rurales.

No quiero depender exclusivamente de fotogrametría o texturas fotográficas.
Mi objetivo visual es: geometría y ubicación real + representación visual estilizada propia.
La información geográfica debe conservar la posición y escala aproximada del mundo real, pero los materiales, iluminación, vegetación, edificios y elementos visuales pueden ser modificados para crear una identidad artística propia.

## 4. Escala del mundo
El mundo debe ser potencialmente gigantesco. No quiero cargar el planeta entero en memoria. Debe utilizarse un sistema de streaming.

Conceptualmente:
Jugador → zona cercana cargada → zonas alejadas descargadas → nuevas zonas cargadas cuando el jugador se desplaza

El sistema debe ser diseñado para funcionar primero con una pequeña zona de prueba y posteriormente escalar a ciudades, países y eventualmente grandes regiones del mundo.

## 5. Primera prueba
NO empezar intentando crear el mundo entero. La primera versión debe ser un prototipo pequeño.

Objetivo inicial:
- aproximadamente 500 m × 500 m o una zona equivalente
- ubicación real, preferentemente una zona de Rosario, Argentina
- terreno real, calles reales, edificios reales o derivados de datos geográficos reales
- personaje en tercera persona, cámara en tercera persona
- movimiento básico, posibilidad de recorrer la zona

La finalidad de esta primera prueba es demostrar que la arquitectura funciona.

## 6. Personaje
El jugador será un personaje humano en tercera persona. Debe tener: movimiento WASD, caminar, correr, salto si resulta apropiado, gravedad, colisiones, interacción con el entorno, cámara de tercera persona, posibilidad de entrar en edificios, posibilidad de subir y bajar pisos.

La cámara debe sentirse similar a un videojuego moderno de tercera persona, no como una cámara de simulador geográfico.

## 7. Edificios
Los edificios exteriores deben derivarse de los datos geográficos disponibles. No quiero modelar manualmente miles o millones de edificios. Quiero utilizar generación procedural siempre que sea posible.

Un edificio debería tener información conceptual como: posición, orientación, ancho, profundidad, altura, cantidad de pisos cuando esté disponible, tipo de edificio cuando esté disponible, forma de la huella, entradas cuando puedan determinarse.

Ejemplo conceptual:
```
BuildingData:
  width
  depth
  height
  floors
  buildingType
  footprint
  position
  rotation
  seed
```

## 8. Estilo visual
No quiero que el resultado final sea simplemente Google Maps o una fotogrametría. Quiero una estética propia.

El mundo debe conservar la sensación de estar basado en lugares reales, pero los elementos visuales pueden ser: estilizados, simplificados, low-poly o mid-poly, materiales propios, iluminación cinematográfica, vegetación propia, ventanas generadas, desgaste procedural, suciedad procedural, destrucción procedural.

El resultado debería sentirse como: "El mundo real convertido en un videojuego."

## 9. Edificios explorables
Algunos edificios podrán ser explorables. NO es necesario que todos los edificios del mundo sean completamente explorables. Debe existir un sistema de niveles de detalle:

- Nivel 0: Edificio solamente exterior.
- Nivel 1: Edificio exterior + interior simplificado.
- Nivel 2: Edificio completamente explorable.
- Nivel 3: Edificio importante con interior altamente detallado.

El sistema debe decidir qué nivel utilizar según la importancia del edificio, distancia al jugador y otros factores.

## 10. Generación procedural de interiores
Los interiores NO necesitan representar el interior real del edificio. Deben ser generados proceduralmente a partir de las características exteriores del edificio.

Ejemplos:
- Residencial de 10 pisos: pisos, pasillos, escaleras, ascensor, departamentos, habitaciones, baños, cocinas, muebles, puertas, ventanas.
- Hotel: recepción, pasillos, habitaciones, escaleras, ascensores, áreas comunes.
- Oficina: recepción, oficinas, salas de reuniones, baños, pasillos, áreas técnicas.
- Hospital: otro tipo de generador.

La generación debe depender de: buildingType, floors, width, depth, footprint, seed.

## 11. Seed procedural
Cada edificio debe tener una seed determinista.
```
Building ID: 583921
Seed: 839271
```
El mismo edificio debe generar siempre el mismo interior si se utiliza la misma seed y versión del generador. Esto permite: regenerar edificios, ahorrar almacenamiento, mantener consistencia, modificar el mundo sin guardar cada objeto individualmente.

## 12. Pisos
Los edificios explorables deben permitir desplazarse verticalmente. Debe existir un sistema procedural para generar: pisos, escaleras, ascensores, huecos de escalera, conexiones entre pisos.

La cantidad de pisos debe derivarse de los datos reales cuando sea posible. Si no existe ese dato, se debe estimar a partir de la altura del edificio.

## 13. Objetos interiores
Los interiores deben contener objetos generados proceduralmente: camas, mesas, sillas, sofás, armarios, cocinas, televisores, computadoras, basura, cajas, herramientas, electrodomésticos, decoración, objetos interactivos.

No quiero que cada objeto tenga que ser colocado manualmente. Debe existir un sistema de distribución procedural.

## 14. Abandono y destrucción
El juego será de supervivencia y el mundo puede presentar distintos niveles de deterioro. Cada edificio o zona puede tener un nivel de abandono.

```
condition = 0.0  Edificio prácticamente intacto.
condition = 0.5  Edificio parcialmente abandonado.
condition = 1.0  Edificio extremadamente deteriorado.
```

Este valor puede afectar: ventanas rotas, muebles, basura, suciedad, paredes, puertas, vegetación, escombros, objetos abandonados, iluminación, daños estructurales visuales.

## 15. Streaming de interiores
Los interiores tampoco deben estar todos cargados simultáneamente.
- Jugador lejos: no generar o descargar interior.
- Se aproxima: preparar interior.
- Entra: cargar/generar interior.
- Se aleja: descargar o liberar recursos.

El objetivo es que el jugador perciba el mundo como continuo.

## 16. Arquitectura
Arquitectura modular. Evitar crear un único script gigantesco. Cada sistema debe tener una responsabilidad clara.

```
World
├── Geography
├── Terrain
├── Roads
├── Buildings
├── BuildingGenerator
├── InteriorGenerator
├── ObjectGenerator
├── DamageGenerator
├── Streaming
├── Player
├── Camera
├── Interaction
└── Gameplay
```

## 17. Performance
Prioridad desde el principio: streaming, LOD, occlusion culling cuando sea apropiado, GPU instancing, object pooling, generación bajo demanda, descarga de zonas lejanas, reducción de geometría, simplificación de materiales, evitar GameObjects innecesarios, evitar generar interiores que el jugador nunca visitará.

## 18. Filosofía de desarrollo
NO construir todo el proyecto de una sola vez. Trabajar por etapas. Cada etapa debe: 1) tener un objetivo concreto, 2) implementarse, 3) probarse, 4) verificar errores de Unity, 5) comprobar rendimiento, 6) documentarse, 7) recién después pasar a la siguiente.

Antes de realizar cambios grandes, analizar primero la arquitectura existente. No reemplazar sistemas existentes sin verificar qué dependencias tienen.

## 19. Primera fase a implementar
NO implementar todavía el sistema completo de interiores, destrucción ni supervivencia. Primero conseguir:
1. Unity 6 funcionando.
2. Cesium correctamente integrado.
3. Mundo geográfico funcionando.
4. Una ubicación de prueba en Rosario.
5. Terreno.
6. Edificios.
7. Personaje en tercera persona.
8. Cámara.
9. Movimiento.
10. Colisiones.
11. Streaming básico.
12. Una arquitectura preparada para posteriormente implementar edificios explorables.

Después:
- FASE 2: Sistema de edificios.
- FASE 3: Generación procedural de interiores.
- FASE 4: Pisos, escaleras y ascensores.
- FASE 5: Objetos y mobiliario procedural.
- FASE 6: Deterioro y destrucción.
- FASE 7: Interacción.
- FASE 8: Supervivencia y gameplay.
- FASE 9: Optimización y escalabilidad.

## 20. Regla fundamental
No inventar soluciones antes de comprobar las capacidades reales de las tecnologías utilizadas. Si una característica depende de Cesium, Unity, OpenStreetMap, un paquete externo o una API, primero verificar qué puede hacer realmente esa tecnología. Si existe una limitación técnica, explicarla antes de construir una solución alrededor de una suposición incorrecta. El objetivo es construir algo técnicamente viable, no solamente una demostración conceptual.

## 21. Qué se espera del agente
Actuar como desarrollador senior de Unity y arquitecto técnico. No solamente código. Inspeccionar el proyecto, entender la arquitectura existente, proponer soluciones, implementar los cambios, probarlos, revisar errores de consola, mantener el proyecto organizado, evitar duplicaciones, documentar decisiones importantes.

Cuando una tarea sea demasiado grande, dividirla automáticamente en tareas más pequeñas. No avanzar a la siguiente fase hasta que la anterior esté funcionando correctamente.

## 22. Objetivo final
Videojuego de supervivencia en tercera persona donde el jugador pueda explorar un mundo basado en la Tierra real. El mundo debe sentirse enorme. Las ciudades deben estar basadas en ciudades reales. Los edificios deben conservar su ubicación y características exteriores reales cuando los datos lo permitan. Algunos edificios deben poder explorarse. Sus interiores deben generarse proceduralmente. El jugador debe poder subir y bajar pisos. Los interiores deben contener objetos, mobiliario, basura, daños y elementos interactivos. El mundo debe poder crecer desde una pequeña zona de prueba hasta regiones enormes mediante streaming. La estética final NO debe ser Google Maps ni fotogrametría pura. Debe ser una interpretación artística y estilizada del mundo real.

Referencia conceptual: Microsoft Flight Simulator + videojuego de supervivencia en tercera persona + generación procedural + interiores explorables + estética propia.
