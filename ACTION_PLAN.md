# PLAN DE ACCIÓN DEFINITIVO: MUNDO ABIERTO PROCEDURAL REAL (CESIUM + MOTOR PROPIO)

> [!IMPORTANT]
> **DIRECTIVA PARA TODAS LAS IA Y DESARROLLADORES**:
> 1. **NO intentar programar un motor de terreno planetario propio ni reemplazar Cesium para el terreno**.
> 2. **Cesium for Unity** es la base **definitiva, obligatoria y exclusiva** para:
>    - Terreno mundial a escala planetaria (*Cesium World Terrain*).
>    - Curvatura terrestre y coordenadas globales (*CesiumGeoreference*).
>    - Precisión numérica flotante de 64 bits a gran distancia (*CesiumOriginShift*).
>    - Anclaje georreferenciado de objetos (*CesiumGlobeAnchor*).
> 3. **Nuestro código propio** es responsable **exclusivamente** de la capa de gameplay:
>    - Extrusión de edificios interactivos y explorables a partir de huellas vectoriales reales (OpenStreetMap / Overture Maps).
>    - Puertas, losas de pisos transitables, huecos y escaleras interiores.
>    - Aplanado de calles y zonas urbanas mediante herramientas de Cesium (*CesiumCartographicPolygon* / *TileExcluder* / Shaders).
>    - Vegetación interactiva (GPU Instancing) y supervivencia.

---

## 1. Arquitectura del Sistema

```
┌────────────────────────────────────────────────────────────────────────┐
│               CAPA 1: GEOGRAFÍA Y PLANETA (Cesium for Unity)           │
│  • Cesium World Terrain (Streaming de terreno global por LOD)          │
│  • CesiumGeoreference (Conversión Lat/Lon <-> Coordenadas Unity)       │
│  • CesiumOriginShift (Mantiene la cámara cerca del origen flotante)    │
│  • CesiumGlobeAnchor (Clava GameObjects a coordenadas terrestres)      │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│            CAPA 2: GENERACIÓN PROCEDURAL Y GAMEPLAY (Código Propio)    │
│  • Ingesta de Huellas: OpenStreetMap / Overture Maps / Microsoft ML   │
│  • ProceduralBuildingGenerator:                                        │
│     - LOD 0: Mallas exteriores optimizadas (batch sólido)              │
│     - LOD 1: Edificios explorables con puertas, pisos y escaleras      │
│  • Aplanado Urbano: CesiumCartographicPolygon para nivelar calzadas    │
│  • Red Vial: Mallas de calzada y veredas transitables                  │
│  • Biomas y Vegetación: Árboles y follaje con GPU Instancing           │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Fases de Implementación y Roadmap

### FASE 1: Base de Cesium y Aplanado Urbano (Completada / En ajuste)
- [x] Configuración de `CesiumGeoreference` y `CesiumOriginShift` en el rig del Player.
- [x] Soporte de colisiones en tiempo de ejecución con `CesiumGroundHold`.
- [ ] Aplicar `CesiumCartographicPolygon` / `CesiumTileExcluder` en la zona urbana de prueba para aplanar el microrrelieve de las calles de la ciudad sin alterar el relieve natural periférico.

### FASE 2: Generador de Edificios Procedurales Georreferenciados (En curso)
- [x] Algoritmo de extrusión 2.5D con mallas sólidas de doble cara (`BuildingMeshBuilder.cs`).
- [x] Generación de puertas en planta baja, losas de pisos cada 3.2m y escaleras (`StaircaseBuilder.cs`).
- [ ] Conectar el `ProceduralBuildingGenerator` para anclar los edificios directamente al terreno de Cesium usando `CesiumGlobeAnchor`.
- [ ] Sistema de selección determinista de LOD (LOD 0 sólido para ~90% de la ciudad, LOD 1 explorable para ~10% de edificios clave).

### FASE 3: Red Vial y Calles Transitables
- [ ] Generador de cinta de pavimento (*Road Ribbon Generator*) sobre los vectores de calles de OpenStreetMap.
- [ ] Proyección de calzadas a ras de la superficie de Cesium con cordones de vereda en zonas urbanas.

### FASE 4: Vegetación y Biomas con GPU Instancing
- [ ] Spawner de árboles 3D y áreas verdes que lee las coberturas de suelo y siembra vegetación masiva con alto rendimiento.
- [ ] Árboles con físicas y colisiones para tala y recolección de recursos.

### FASE 5: Mecánicas de Supervivencia y Exploración
- [ ] Sistema de inventario, herramientas y crafteo.
- [ ] Mobiliario y loot procedural dentro de los edificios explorables (LOD 2/3).
- [ ] Ciclo día/noche e iluminación cinematográfica.

---

## 3. Guía de Integración Técnica: Cómo Agregar Nuevos Edificios

Cualquier sistema o IA que genere un edificio nuevo debe seguir esta plantilla:

```csharp
// 1. Crear el GameObject del edificio con su malla procedural
GameObject buildingGO = new GameObject($"Building_{id}");
BuildingMeshBuilder.AddExplorableBuilding(vertices, colors, triangles, footprint, groundHeight, ...);

// 2. Anclar el edificio geográficamente en Cesium
var anchor = buildingGO.AddComponent<CesiumGlobeAnchor>();
anchor.longitudeLatitudeHeight = new Unity.Mathematics.double3(longitude, latitude, groundHeight);
```

---

## 4. Reglas Estrictas de Desarrollo
1. **No duplicar sistemas**: Si Cesium ya resuelve el terreno y las coordenadas, no crear generadores de terreno alternativos.
2. **Preservar el rendimiento**: Todo edificio no explorable debe permanecer como LOD 0 en batch; solo los edificios dentro del radio de interacción se expanden a LOD 1 (interiores).
3. **Determinismo**: Mismo seed + misma coordenada geográfica = exactamente el mismo edificio e interior.
