# Investigación: Fuentes de datos de edificios a escala mundial

Investigación de las 4 fuentes solicitadas para alimentar un `GlobalBuildingDataProvider`. Todo lo marcado como **[CONFIRMADO]** viene de documentación oficial fetcheada directamente (URLs al final). Todo lo marcado como **[RECOMENDACIÓN]** es inferencia/propuesta propia, no un hecho documentado.

## A. Geometría de edificios, por fuente

| Fuente | Polígonos individuales | Coordenadas | Resolución/precisión | Cobertura mundial | Tipo de dato |
|---|---|---|---|---|---|
| Google Open Buildings 2.5D Temporal | **NO** — es raster, no polígonos | Sí (por píxel) | ~4m efectivo (archivos a 50cm) | Parcial: África, Sur de Asia, Sudeste Asiático, Latinoamérica y Caribe (~58M km²) | Raster: 3 canales (presencia, altura, conteo fraccional) |
| Microsoft GlobalMLBuildingFootprints | **Sí** | Sí | Polígono por edificio | 225 regiones, ~1.4 mil millones de edificios | GeoJSON (líneas), vectorial |
| OpenStreetMap | **Sí** | Sí | Polígono por edificio (mapeado a mano) | Irregular — depende de qué tan mapeada esté cada zona (Rosario ya nos dio 7454 edificios) | Vectorial, tags libres |
| Cesium OSM Buildings | Sí (repackaging de OSM) | Sí | Igual que OSM | Global (por ser 100% derivado de OSM) | 3D Tileset pre-generado |

**[CONFIRMADO]** Google 2.5D Temporal **no da polígonos individuales** — esto es clave: no sirve como fuente de footprint, solo de altura/presencia por píxel.

## B. Altura, comparación

- **Google 2.5D Temporal [CONFIRMADO]:** altura por píxel, error medio absoluto de 1.5m, capado a 100m. Es la fuente más precisa en altura donde tiene cobertura, pero no viene atada a un edificio individual — hay que agregarla dentro del footprint de otra fuente.
- **Microsoft [CONFIRMADO]:** solo ~174M de los ~1.4 mil millones de edificios (≈12%) tienen altura estimada; el resto queda marcado con `-1` (sin dato).
- **OSM [CONFIRMADO por conocimiento del formato de tags, no por la página fetcheada]:** altura solo cuando algún mapeador cargó `building:height` o `building:levels` a mano — cobertura muy irregular, pero cuando existe suele ser confiable (dato humano real, no estimado).
- **Ninguna fuente sola cubre "altura por edificio" a escala global.** Hay que combinarlas.

## C. Combinar las fuentes — ¿es viable el matching?

**[RECOMENDACIÓN, técnica estándar en GIS, no una fuente que lo confirme directamente]** Sí es viable, y es una técnica bien establecida (no algo experimental):

1. Tomar el polígono de footprint de Microsoft u OSM (el que esté disponible en esa zona).
2. Samplear los píxeles de altura de Google 2.5D que caen dentro de ese footprint (cuando la zona esté en su cobertura) y tomar el máximo o la media como altura estimada del edificio.
3. Si ninguna fuente de altura está disponible, usar heurística de pisos (`building:levels` de OSM si existe, o un default razonable como 3m/piso según tipo de edificio).

Dificultad: **moderada, no trivial pero manejable** con librerías GIS estándar (Shapely/GeoPandas para IoU y distancia de centroide), corriendo **offline**, no en tiempo real. El riesgo real no es la dificultad técnica sino los casos borde: un footprint grande en una fuente puede corresponder a varios edificios chicos en otra (años de captura distintos, metodologías distintas).

## D. Cobertura mundial — sistema de fallback

**[RECOMENDACIÓN]** Sí es viable un fallback en cadena, y de hecho es la única forma sensata de lograr cobertura global real:

```
OSM (más fresco, ya integrado, mejor en zonas bien mapeadas)
  ↓ si no hay datos suficientes
Microsoft (mejor cobertura uniforme global, 1.4B edificios)
  ↓ para enriquecer altura donde aplique
Google 2.5D (solo enriquece altura, solo en sus regiones cubiertas)
  ↓ si no hay nada
Estimación procedural (generar edificios plausibles a lo largo de calles conocidas)
```

## E. Licencias — la parte más importante

**[CONFIRMADO — leído directamente de las licencias oficiales]**

- **Google Open Buildings 2.5D Temporal:** doble licencia **CC BY 4.0 / ODbL 1.0**. Uso comercial permitido, requiere atribución. ✅
- **Microsoft GlobalMLBuildingFootprints:** **CDLA Permissive 2.0**. Es una licencia permisiva (sin cláusula "share-alike"): uso comercial, modificación y redistribución de datos derivados permitidos, solo con atribución. ✅ — la más permisiva de las tres.
- **OpenStreetMap:** **ODbL 1.0**. Uso comercial permitido. Requiere atribución (créditar OSM + mencionar ODbL). **Punto crítico que verifiqué en el texto legal directo:** la cláusula de "share-alike" (Sección 4.4) aplica solo a una **"Derivative Database"** (redistribuir una copia modificada de la base de datos de OSM). Tu juego, al generar geometría/mundo a partir de consultar esos datos, es un **"Produced Work"** (Sección 1.0/4.5b), y la licencia dice explícitamente: *"Using this Database... to create a Produced Work does not create a Derivative Database"* — o sea, **tu mundo generado NO tiene que liberarse bajo ODbL ni ser open source**. Solo necesitás atribución visible (ej. pantalla de créditos). ✅ Esto resuelve tu preocupación principal.
- **Cesium OSM Buildings / World Terrain (vía Cesium ion):** esto es un tema aparte de la licencia de los datos — es el **servicio de hosting/streaming de Cesium** el que cobra. El tier gratuito de Cesium ion es "personal y no comercial" únicamente; para un juego comercial shippeado necesitás un plan pago (~$149-524+/mes base, más consumo de streaming/storage). Esto no es una restricción de datos, es un costo de infraestructura si decidís usar el tileset ya armado de Cesium en vez de pullear los datos crudos vos mismo.
- **Combinación entre fuentes:** ninguna licencia prohíbe combinar las tres — las tres son independientemente comercial-friendly, y ninguna "contamina" a las otras.

## F. Actualización de los datos

**[CONFIRMADO]**
- Google 2.5D Temporal: snapshots anuales 2016-2023, no es de actualización continua.
- Microsoft: actualizaciones regulares y activas (la documentación mostraba una actualización de agosto 2026), millones de ediciones agregadas periódicamente.
- OSM: actualización continua por la comunidad — podés re-consultar Overpass en cualquier momento y tener el estado más fresco (es la fuente más "viva" de las tres).
- Cesium OSM Buildings: reconstruye su tileset trimestralmente a partir de OSM.

**[RECOMENDACIÓN]** Usar OSM como fuente primaria justamente por esto — ya está integrado y es la más fácil de refrescar; Microsoft/Google como capas complementarias que no hace falta re-consultar tan seguido.

## G. Integración con Cesium — ¿tiene sentido la arquitectura propuesta?

**[RECOMENDACIÓN]** Sí, la división que planteaste tiene sentido, con un ajuste:

- **Cesium for Unity:** dejarlo exclusivamente para lo que resuelve bien y es genuinamente difícil de construir desde cero — georreferenciación (lat/lon ↔ coordenadas de Unity), terreno mundial (Cesium World Terrain, gratis), y el framework de streaming/LOD de tiles 3D.
- **NO usar el tileset "Cesium OSM Buildings" ya armado** como fuente de datos de edificios — requiere plan pago comercial y de todas formas es solo OSM repaquetizado, con menos control que pullear los datos crudos vos mismo (que ya estás haciendo con Overpass API, gratis).
- **BuildingDataProvider (tu código):** implementa la cadena de fallback (OSM → Microsoft → Google → estimación), corre el matching geoespacial offline, y produce un `BuildingData` unificado — completamente independiente de Cesium.
- **ProceduralBuildingGenerator (tu código, ya en desarrollo):** consume `BuildingData` sin saber ni importarle de qué fuente salió cada dato.

## H. Viabilidad real — conclusión

1. **¿Es técnicamente viable?** Sí.
2. **¿Qué precisión podemos esperar?** Footprint: buena (nivel de metro) casi en todos lados combinando OSM+Microsoft. Altura: confiable solo en ~12-20% de los edificios sin enriquecimiento; fuera de las regiones de Google (Norteamérica, Europa, gran parte de Asia no están cubiertas por Google 2.5D) la altura real solo viene de tags manuales de OSM cuando existen, si no, heurística.
3. **¿Qué cobertura mundial podemos conseguir?** Footprint: casi total combinando OSM+Microsoft. Altura: bastante más parcial e irregular por región.
4-6. **Combinación recomendada:** OSM (primaria, footprint+altura cuando esté tageada) + Microsoft (respaldo de footprint global uniforme) + Google 2.5D (enriquecimiento de altura, solo en sus regiones) + estimación procedural como último recurso.
7. **Problemas técnicos:** casos borde de matching entre polígonos de años/métodos distintos; ausencia total de Google en Norteamérica/Europa/gran parte de Asia (justo las regiones "importantes" si algún día quieren ciudades de EEUU/Europa quedan sin esa capa de altura real).
8. **Problemas legales:** ninguno bloqueante — la exención de "Produced Work" de ODbL + la licencia permisiva de Microsoft + la doble licencia permisiva de Google cubren el caso de uso. Solo hace falta atribución consistente (pantalla de créditos estándar).
9. **Procesamiento/storage:** esto tiene que ser **offline/preprocesado por región** — los archivos de Microsoft y los rasters de Google son pesados a escala global (aunque se puede traer solo el tile/quadkey de la región que interesa). No es algo para hacer en runtime durante el juego shippeado.
10. **Offline vs. tiempo real:** TODO el fetch+matching+inferencia de altura debe correr offline, por región, en tu "modo administrador" (el mismo patrón que ya habíamos definido para la generación de contenido) → produce un asset liviano de `BuildingData` por región (mismo patrón que `GeoDataset_Rosario.asset` que ya existe) → en runtime el juego solo carga/streamea ese asset ya procesado, nunca llama a Overpass/Microsoft/Google durante el gameplay real.

## Arquitectura propuesta: `GlobalBuildingDataProvider`

```
IBuildingDataProvider (interfaz)
  GetBuildings(BoundingBox) -> List<BuildingData>

Implementaciones, en orden de fallback:
  OsmBuildingProvider            // Overpass API, primario, ya funciona (Rosario)
  MicrosoftBuildingProvider      // archivos de footprint por región/quadkey, preprocesado offline
  GoogleHeightEnrichmentProvider // NO es fuente de footprint — enriquece BuildingData ya existente
                                  // con altura sampleada del raster 2.5D, solo en sus regiones
  ProceduralEstimateProvider     // último recurso: sintetiza edificios plausibles a lo largo
                                  // de la red de calles conocida, cuando no hay nada más

BuildingDataMatcher (utilidad offline)
  MatchAndMerge(List<BuildingData> fuenteA, List<BuildingData> fuenteB) -> BuildingData fusionado
  (distancia de centroide + umbral de IoU)

GlobalBuildingDataProvider (orquestador)
  - corre offline, por región (modo administrador)
  - ejecuta la cadena de fallback + el matching
  - cachea el resultado como asset importable de Unity (mismo patrón que GeoDataset_Rosario.asset)

ProceduralBuildingGenerator (ya planeado)
  - consume solo BuildingData ya fusionado, sin saber de qué fuente vino cada dato
```

## Fuentes consultadas
- https://sites.research.google/gr/open-buildings/temporal/
- https://github.com/microsoft/GlobalMLBuildingFootprints
- https://www.openstreetmap.org/copyright
- https://opendatacommons.org/licenses/odbl/1-0/ (texto legal completo)
- https://cesium.com/platform/cesium-ion/content/cesium-osm-buildings/
- https://cesium.com/platform/cesium-ion/pricing/
