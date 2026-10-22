using System;
using System.Collections.Generic;
using System.IO;
using Platformer.Core;
using Platformer.Mechanics;
using Platformer.Model;
using Platformer.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>
/// Genera el nivel definitivo del prototipo: terreno continuo con escalones
/// alcanzables, cofres que otorgan elementos, pared elemental, zonas de
/// peligro, enemigos, jefe, monedas y HUD.
///
/// Puede ejecutarse desde el menú "Village/..." o de forma automática creando
/// el archivo centinela "_migrate_village.flag" en la raíz del proyecto.
/// </summary>
[InitializeOnLoad]
public static class VillagePropsMigrator
{
    const string PrefabBase = "Assets/Cainos/Pixel Art Platformer - Village Props/Prefab/";
    const string TilePath = "Assets/Cainos/Pixel Art Platformer - Village Props/Tileset Palette/TP Ground/";
    const string AudioBase = "Assets/Audio/";
    const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    const string FlagName = "_migrate_village.flag";
    const string PropsRootName = "Village_Props";
    const string HudName = "HUD";

    const float JumpModifier = 1f;
    const float JumpDeceleration = 0.85f;
    const float PlayerMaxSpeed = 4f;
    const float PlayerJumpTakeOffSpeed = 8f;
    const float PlayerStartX = -28f;
    const float WallX = 23f;
    const float VictoryX = 35f;

    // Alturas desde las que se busca la superficie del terreno.
    const float FloorLevel = 2f;     // suelo del nivel bajo
    const float ShelfLevel = 15f;    // estante superior
    const float UpperLevel = 34f;    // suelo del nivel alto

    // Capas de orden de dibujado: props (3) < monedas (4) < jugador y enemigos (5).
    const int PropSortingOrder = 3;
    const int TokenSortingOrder = 4;

    struct Block
    {
        public float xMin, xMax, top, bottom;
        public Block(float xMin, float xMax, float top, float bottom)
        {
            this.xMin = xMin; this.xMax = xMax; this.top = top; this.bottom = bottom;
        }
    }

    // Terreno del nivel, inspirado en PROTO_MAPA.drawio.
    // La superficie real de un bloque es su borde superior ("top"): los tiles
    // se pintan con el centro dentro del bloque, asi que el ultimo tile deja
    // el borde justo en "top". Por eso los props se buscan con
    // SurfaceY(..., techo).
    static readonly Block[] Blocks =
    {
        // --- Nivel bajo ---
        new Block(-36f, -34f, 14f, -8f),   // muro izquierdo de la camara
        new Block(-34f, 24f, 14f, 12f),    // estante superior (techo de camara y pasillo)
        new Block(-20f, -18f, 12f, 6f),    // pared colgante cuelga del estante (se pasa por debajo)
        new Block(-32f, 6f, 0f, -8f),      // suelo inicial
        new Block(8f, 12f, 2f, 0f),         // plataforma que salva el foso
        new Block(14f, 32f, 0f, -8f),      // suelo B

        // Escalera ascendente: dos columnas de 4 u con 2 u de hueco entre
        // ellas, igual que la escalera de la sala del jefe. Alternando
        // columnas el hueco siempre queda libre de techos: un bloque 2
        // niveles por encima que sobresaliese sobre el salto cortaria la
        // subida (la velocidad vertical se anula al chocar con la cabeza).
        new Block(34f, 38f, 2f, 0f),
        new Block(40f, 44f, 4f, 2f),
        new Block(34f, 38f, 6f, 4f),
        new Block(40f, 44f, 8f, 6f),
        new Block(34f, 38f, 10f, 8f),
        new Block(26f, 32f, 12f, 10f),     // plataforma ancha con enemigo

        new Block(-32f, -28f, 16f, 14f),   // escalera que sube a la sala del jefe
        new Block(-26f, -22f, 18f, 16f),
        new Block(-32f, -28f, 20f, 18f),
        new Block(-26f, -22f, 22f, 20f),
        new Block(-32f, -28f, 24f, 22f),
        new Block(-26f, -22f, 26f, 24f),
        new Block(-20f, -16f, 28f, 26f),
        new Block(-14f, -10f, 30f, 28f),
        new Block(-8f, -2f, 30f, 28f),
        new Block(-2f, 0f, 28f, 16f),      // pared divisoria (cuelga del suelo superior y deja paso por debajo)
        new Block(0f, 44f, 30f, 28f),      // suelo superior
        new Block(-34f, 44f, 38f, 36f),    // techo superior
    };

    struct PropDef
    {
        public string prefab, objName;
        public float x, yOffset, ceiling;
        public PropDef(string prefab, float x, float yOffset, string objName, float ceiling)
        {
            this.prefab = prefab; this.x = x; this.yOffset = yOffset; this.objName = objName; this.ceiling = ceiling;
        }
    }

    static readonly PropDef[] Decor =
    {
        // Camara inicial
        new PropDef("PF Village Props - Tree 01", -30.5f, 0f, "Arbol_Inicio", FloorLevel),
        new PropDef("PF Village Props - Barrel", -26.5f, 0f, "Barril_Inicio", FloorLevel),
        new PropDef("PF Village Props - Sign 01", -25f, 0f, "Senal_Inicio", FloorLevel),
        new PropDef("PF Village Props - Grass 01", -23.5f, 0f, "Hierba_01", FloorLevel),
        new PropDef("PF Village Props - Well", -17f, 0f, "Pozo_Inicio", FloorLevel),
        new PropDef("PF Village Props - Torch", -13.5f, 1.4f, "Antorcha_01", FloorLevel),
        // Pasillo bajo la pared colgante
        new PropDef("PF Village Props - Bush 01", -11.5f, 0f, "Arbusto_01", FloorLevel),
        new PropDef("PF Village Props - Flower 01", -9.5f, 0f, "Flor_01", FloorLevel),
        new PropDef("PF Village Props - Campfire 01", -3f, 0f, "Fuego_Ambiental", FloorLevel),
        new PropDef("PF Village Props - Bush 02", 3.5f, 0f, "Arbusto_02", FloorLevel),
        // Suelo B
        new PropDef("PF Village Props - Road Sign 01", 16f, 0f, "Senal_Controles", FloorLevel),
        new PropDef("PF Village Props - Fence A", 18f, 0f, "Valla_01", FloorLevel),
        new PropDef("PF Village Props - Hay Bale", 20f, 0f, "Fardo", FloorLevel),
        new PropDef("PF Village Props - Bush 02", 22.5f, 0f, "Arbusto_03", FloorLevel),
        new PropDef("PF Village Props - Grass 03", 24.5f, 0f, "Hierba_02", FloorLevel),
        new PropDef("PF Village Props - Torch", 28f, 1.4f, "Antorcha_02", FloorLevel),
        // Escalera ascendente
        new PropDef("PF Village Props - Statue", 35.5f, 0f, "Estatua", 3f),
        new PropDef("PF Village Props - Tree 02", 40.5f, 0f, "Arbol_02", 5f),
        new PropDef("PF Village Props - Grass 05", 34f, 0f, "Hierba_03", 7f),
        new PropDef("PF Village Props - Bush 01", 40f, 0f, "Arbusto_04", 9f),
        new PropDef("PF Village Props - Torch", 36f, 1.4f, "Antorcha_03", 11f),
        // Plataforma ancha
        new PropDef("PF Village Props - Tree 01", 27f, 0f, "Arbol_03", 13f),
        new PropDef("PF Village Props - Road Lamp", 28.5f, 2f, "Lampara_01", 13f),
        new PropDef("PF Village Props - Banner", 29.5f, 2f, "Estandarte_01", 13f),
        // Estante superior
        new PropDef("PF Village Props - Barrel", 21f, 0f, "Barril_02", ShelfLevel),
        new PropDef("PF Village Props - Bush 01", 19f, 0f, "Arbusto_05", ShelfLevel),
        new PropDef("PF Village Props - Tree 02", 16f, 0f, "Arbol_04", ShelfLevel),
        new PropDef("PF Village Props - Grass 02", 7f, 0f, "Hierba_04", ShelfLevel),
        new PropDef("PF Village Props - Torch", 4f, 1.4f, "Antorcha_04", ShelfLevel),
        new PropDef("PF Village Props - Road Sign 01", 4f, 0f, "Senal_Elementos", ShelfLevel),
        new PropDef("PF Village Props - Bush 02", -6f, 0f, "Arbusto_06", ShelfLevel),
        new PropDef("PF Village Props - Banner", -10f, 2f, "Estandarte_02", ShelfLevel),
        // Escalera de la sala del jefe
        new PropDef("PF Village Props - Grass 04", -30f, 0f, "Hierba_05", 17f),
        new PropDef("PF Village Props - Torch", -23f, 1.4f, "Antorcha_05", 19f),
        new PropDef("PF Village Props - Flower 02", -30f, 0f, "Flor_02", 25f),
        new PropDef("PF Village Props - Bush 01", -24f, 0f, "Arbusto_07", 27f),
        new PropDef("PF Village Props - Sign 01", -16f, 0f, "Senal_Jefe", ShelfLevel),
        // Nivel superior
        new PropDef("PF Village Props - Tree 01", 11f, 0f, "Arbol_05", UpperLevel),
        new PropDef("PF Village Props - Grass 01", 15f, 0f, "Hierba_06", UpperLevel),
        new PropDef("PF Village Props - Bush 02", 19f, 0f, "Arbusto_08", UpperLevel),
        new PropDef("PF Village Props - Banner", 26f, 2f, "Estandarte_03", UpperLevel),
        new PropDef("PF Village Props - Billboard", 30f, 2f, "Cartel_Victoria", UpperLevel),
        new PropDef("PF Village Props - Torch", 42f, 1.4f, "Antorcha_06", UpperLevel),
    };

    struct TokenSpot
    {
        public float x, ceiling, height;
        public TokenSpot(float x, float ceiling, float height)
        {
            this.x = x; this.ceiling = ceiling; this.height = height;
        }
    }

    // Monedas: x, techo de busqueda y altura sobre la superficie.
    static readonly TokenSpot[] TokenSpots =
    {
        new TokenSpot(-30f, FloorLevel, 0.7f), new TokenSpot(-26f, FloorLevel, 0.7f),
        new TokenSpot(-19f, FloorLevel, 0.7f), new TokenSpot(-17f, FloorLevel, 2f),
        new TokenSpot(-7f, FloorLevel, 0.7f), new TokenSpot(-4.5f, FloorLevel, 0.7f),
        new TokenSpot(2f, FloorLevel, 0.7f), new TokenSpot(4.5f, FloorLevel, 2f),
        new TokenSpot(9.5f, 3f, 0.7f), new TokenSpot(11f, 3f, 2f),
        new TokenSpot(15f, FloorLevel, 0.7f), new TokenSpot(17f, FloorLevel, 2f),
        new TokenSpot(21f, FloorLevel, 0.7f), new TokenSpot(24f, FloorLevel, 0.7f),
        new TokenSpot(28f, FloorLevel, 0.7f),
        new TokenSpot(35f, 3f, 0.7f), new TokenSpot(41f, 5f, 0.7f),
        new TokenSpot(34f, 7f, 0.7f), new TokenSpot(40f, 9f, 0.7f),
        new TokenSpot(36f, 11f, 0.7f),
        new TokenSpot(26.5f, 13f, 0.7f), new TokenSpot(28f, 13f, 2f), new TokenSpot(29.5f, 13f, 0.7f),
        new TokenSpot(22f, ShelfLevel, 0.7f), new TokenSpot(18f, ShelfLevel, 0.7f),
        new TokenSpot(9f, ShelfLevel, 0.7f), new TokenSpot(6f, ShelfLevel, 2f),
        new TokenSpot(0f, ShelfLevel, 0.7f), new TokenSpot(-8f, ShelfLevel, 0.7f),
        new TokenSpot(-30f, 17f, 0.7f), new TokenSpot(-24f, 19f, 0.7f),
        new TokenSpot(-30f, 21f, 0.7f), new TokenSpot(-24f, 23f, 0.7f),
        new TokenSpot(-30f, 25f, 0.7f), new TokenSpot(-24f, 27f, 0.7f),
        new TokenSpot(-18f, 29f, 0.7f), new TokenSpot(-8f, 31f, 0.7f),
        new TokenSpot(-4f, 31f, 0.7f),
        new TokenSpot(3f, UpperLevel, 0.7f), new TokenSpot(11f, UpperLevel, 2f),
        new TokenSpot(15f, UpperLevel, 0.7f), new TokenSpot(19f, UpperLevel, 0.7f),
        new TokenSpot(26f, UpperLevel, 0.7f), new TokenSpot(30f, UpperLevel, 2f),
        new TokenSpot(33f, UpperLevel, 0.7f), new TokenSpot(37f, UpperLevel, 0.7f),
        new TokenSpot(41f, UpperLevel, 0.7f), new TokenSpot(43f, UpperLevel, 0.7f),
    };

    static double nextFlagCheck;

    static VillagePropsMigrator()
    {
        EditorApplication.update += WatchFlag;
    }

    static void WatchFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.timeSinceStartup < nextFlagCheck) return;
        nextFlagCheck = EditorApplication.timeSinceStartup + 0.5;

        string flag = FlagPath();
        if (!File.Exists(flag)) return;

        if (Migrate())
        {
            File.Delete(flag);
            Debug.Log("[VillagePropsMigrator] Migracion automatica completada.");
        }
        else
        {
            Debug.LogWarning("[VillagePropsMigrator] Migracion automatica pospuesta: revisa que SampleScene este abierta.");
        }
    }

    static string FlagPath()
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, FlagName);
    }

    [MenuItem("Village/Migrar nivel definitivo")]
    public static void MigrateFromMenu()
    {
        Migrate();
    }

    public static bool Migrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[VillagePropsMigrator] Sal del modo play antes de migrar.");
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("[VillagePropsMigrator] No hay escena activa.");
            return false;
        }

        Tilemap level = FindCollisionTilemap();
        if (level == null)
        {
            Debug.LogError("[VillagePropsMigrator] No se encontro tilemap con TilemapCollider2D.");
            return false;
        }

        if (EditorSettings.enterPlayModeOptionsEnabled)
            EditorSettings.enterPlayModeOptionsEnabled = false;

        int tiles = BuildTerrain(level);
        HideBackgrounds();

        Transform propsRoot = CreatePropsRoot();
        Tilemap map = level;
        PlaceDecor(map, propsRoot);
        PlaceChests(map, propsRoot);
        PlaceBraziers(map, propsRoot);
        PlaceElementalWall(map, propsRoot);

        SetupEntities(map);
        DisableLeftoverProps(propsRoot);
        SetupHud();
        ConfigureModel();
        UpdateConfiner();
        RemoveSceneObjectGuids();
        RecordAllInstances();

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        if (saved)
            Debug.Log($"[VillagePropsMigrator] Nivel definitivo guardado ({tiles} tiles) en {scene.path}");
        else
            Debug.LogError("[VillagePropsMigrator] No se pudo guardar la escena.");

        return saved;
    }

    static Tilemap FindCollisionTilemap()
    {
        foreach (Tilemap tm in UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude))
        {
            if (tm.GetComponent<TilemapCollider2D>() != null)
                return tm;
        }
        return null;
    }

    static int BuildTerrain(Tilemap level)
    {
        TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(TilePath + "TX Tileset Ground_0.asset");
        if (tile == null)
        {
            Debug.LogError("[VillagePropsMigrator] No se encontro el tile del tileset.");
            return 0;
        }

        level.ClearAllTiles();

        TilemapRenderer rend = level.GetComponent<TilemapRenderer>();
        if (rend != null)
        {
            rend.enabled = true;
            rend.sortingOrder = 0;
        }

        int count = 0;
        HashSet<Vector3Int> painted = new HashSet<Vector3Int>();

        foreach (Block b in Blocks)
        {
            Vector3Int min = level.WorldToCell(new Vector3(b.xMin, b.bottom, 0f));
            Vector3Int max = level.WorldToCell(new Vector3(b.xMax, b.top, 0f));

            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    Vector3Int cell = new Vector3Int(x, y, 0);
                    Vector3 center = level.GetCellCenterWorld(cell);

                    if (center.x < b.xMin || center.x >= b.xMax) continue;
                    if (center.y < b.bottom || center.y >= b.top) continue;
                    if (painted.Contains(cell)) continue;

                    level.SetTile(cell, tile);
                    painted.Add(cell);
                    count++;
                }
            }
        }

        level.RefreshAllTiles();
        return count;
    }

    static void HideBackgrounds()
    {
        string[] names = { "Background", "FarBackground", "FarBackground " };
        foreach (string name in names)
        {
            GameObject go = GameObject.Find(name);
            if (go == null) continue;

            foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>(true))
                sr.enabled = false;
            foreach (TilemapRenderer tr in go.GetComponentsInChildren<TilemapRenderer>(true))
                tr.enabled = false;
        }
    }

    static Transform CreatePropsRoot()
    {
        GameObject old = GameObject.Find(PropsRootName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        GameObject root = new GameObject(PropsRootName);
        return root.transform;
    }

    /// <summary>
    /// Oculta los props de la demo original de la plantilla que quedaban
    /// flotando en el vacio: el nivel de ejemplo del asset esta muy por encima
    /// de este mapa y sus tiles fueron borrados al rehacer el terreno.
    /// </summary>
    static void DisableLeftoverProps(Transform propsRoot)
    {
        List<Transform> keep = new List<Transform>();

        void Add(GameObject go)
        {
            if (go != null) keep.Add(go.transform);
        }

        foreach (PlayerController c in UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include)) Add(c.gameObject);
        foreach (EnemyController c in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include)) Add(c.gameObject);
        foreach (TokenInstance c in UnityEngine.Object.FindObjectsByType<TokenInstance>(FindObjectsInactive.Include)) Add(c.gameObject);
        foreach (SpawnPoint c in UnityEngine.Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include)) Add(c.gameObject);
        foreach (VictoryZone c in UnityEngine.Object.FindObjectsByType<VictoryZone>(FindObjectsInactive.Include)) Add(c.gameObject);
        foreach (DeathZone c in UnityEngine.Object.FindObjectsByType<DeathZone>(FindObjectsInactive.Include)) Add(c.gameObject);
        foreach (Tilemap c in UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include)) Add(c.gameObject);
        foreach (Camera c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)) Add(c.gameObject);
        Add(GameObject.Find("CinemachineConfiner"));
        Add(GameObject.Find(HudName));

        List<string> disabled = new List<string>();

        foreach (GameObject go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            if (go == null || !go.activeSelf) continue;
            if (go.transform.IsChildOf(propsRoot)) continue;

            bool kept = false;
            foreach (Transform k in keep)
            {
                if (go.transform == k || go.transform.IsChildOf(k) || k.IsChildOf(go.transform))
                {
                    kept = true;
                    break;
                }
            }
            if (kept) continue;

            if (go.GetComponentInChildren<SpriteRenderer>() == null) continue;

            go.SetActive(false);
            disabled.Add(go.name);
        }

        if (disabled.Count > 0)
            Debug.Log("[VillagePropsMigrator] Props de la demo ocultados: " + disabled.Count + " -> " + string.Join(", ", disabled.ToArray()));
    }

    /// <summary>
    /// Superficie del terreno en la columna x. Se busca el primer suelo por
    /// debajo de ceilingY, de modo que las columnas con varios niveles
    /// (estante, suelo superior) devuelven la altura correcta.
    /// </summary>
    static float SurfaceY(Tilemap level, float worldX, float ceilingY)
    {
        Vector3Int column = level.WorldToCell(new Vector3(worldX, ceilingY, 0f));

        for (int y = column.y; y > column.y - 200; y--)
        {
            Vector3Int cell = new Vector3Int(column.x, y, 0);
            if (level.GetTile(cell) == null) continue;
            return level.GetCellCenterWorld(cell).y + level.cellSize.y * 0.5f;
        }

        return 0f;
    }

    static void PlaceDecor(Tilemap level, Transform root)
    {
        foreach (PropDef def in Decor)
        {
            float y = SurfaceY(level, def.x, def.ceiling) + def.yOffset;
            Spawn(def.prefab, new Vector3(def.x, y, 0f), root, def.objName);
        }
    }

    static void PlaceChests(Tilemap level, Transform root)
    {
        AudioClip open = LoadAudio("Collectable");

        GameObject fire = Spawn("PF Village Props - Chest Golden", new Vector3(-22f, SurfaceY(level, -22f, FloorLevel), 0f), root, "Cofre_Elemento_Fuego");
        SetupChest(fire, GameElement.Fuego, true, false, new Color(1f, 0.55f, 0.15f), open);

        GameObject grapple = Spawn("PF Village Props - Chest Wooden", new Vector3(2f, SurfaceY(level, 2f, ShelfLevel), 0f), root, "Cofre_Gancho");
        SetupChest(grapple, GameElement.Ninguno, false, true, new Color(0.75f, 0.6f, 0.35f), open);

        GameObject rayo = Spawn("PF Village Props - Chest Iron", new Vector3(7f, SurfaceY(level, 7f, UpperLevel), 0f), root, "Cofre_Elemento_Rayo");
        SetupChest(rayo, GameElement.Rayo, true, false, new Color(1f, 0.95f, 0.25f), open);
    }

    static void SetupChest(GameObject chest, GameElement element, bool grantsAttack, bool grantsGrapple, Color glow, AudioClip open)
    {
        if (chest == null) return;

        BoxCollider2D collider = chest.GetComponent<BoxCollider2D>();
        if (collider == null) collider = chest.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(1.4f, 1.6f);
        collider.offset = new Vector2(0f, 0.5f);

        ChestPickup pickup = chest.GetComponent<ChestPickup>();
        if (pickup == null) pickup = chest.AddComponent<ChestPickup>();

        SetProperty(pickup, "element", p => p.enumValueIndex = (int)element);
        SetProperty(pickup, "grantsAttack", p => p.boolValue = grantsAttack);
        SetProperty(pickup, "grantsGrapple", p => p.boolValue = grantsGrapple);
        SetProperty(pickup, "glowColor", p => p.colorValue = glow);
        SetProperty(pickup, "openAudio", p => p.objectReferenceValue = open);
    }

    static void PlaceBraziers(Tilemap level, Transform root)
    {
        AudioClip hurt = LoadAudio("Hurt");

        GameObject first = Spawn("PF Village Props - Fire Bowl", new Vector3(-15f, SurfaceY(level, -15f, FloorLevel), 0f), root, "Brasero_Peligro");
        SetupDamageZone(first, GameElement.Fuego, hurt);

        GameObject second = Spawn("PF Village Props - Fire Bowl", new Vector3(16f, SurfaceY(level, 16f, UpperLevel), 0f), root, "Brasero_Elemento");
        SetupDamageZone(second, GameElement.Rayo, hurt);
    }

    static void SetupDamageZone(GameObject brazier, GameElement safeElement, AudioClip hurt)
    {
        if (brazier == null) return;

        BoxCollider2D collider = brazier.GetComponent<BoxCollider2D>();
        if (collider == null) collider = brazier.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(1.6f, 2.4f);
        collider.offset = new Vector2(0f, 1f);

        DamageZone zone = brazier.GetComponent<DamageZone>();
        if (zone == null) zone = brazier.AddComponent<DamageZone>();

        SetProperty(zone, "safeElement", p => p.enumValueIndex = (int)safeElement);
        SetProperty(zone, "hurtAudio", p => p.objectReferenceValue = hurt);
    }

    static void PlaceElementalWall(Tilemap level, Transform root)
    {
        float baseY = SurfaceY(level, WallX, UpperLevel);

        GameObject wall = new GameObject("Pared_Elemental");
        wall.transform.SetParent(root, false);
        wall.transform.position = new Vector3(WallX, baseY, 0f);

        for (int i = 0; i < 4; i++)
        {
            GameObject brick = Spawn("PF Village Props - Brick Wall 01", Vector3.zero, wall.transform, "Ladrillo_" + (i + 1));
            if (brick == null) continue;
            brick.transform.localPosition = new Vector3(0f, 0.67f + i * 1.34f, 0f);
        }

        GameObject blocker = new GameObject("Bloqueo");
        blocker.transform.SetParent(wall.transform, false);
        blocker.transform.localPosition = Vector3.zero;
        BoxCollider2D blockerCollider = blocker.AddComponent<BoxCollider2D>();
        blockerCollider.size = new Vector2(4.4f, 6f);
        blockerCollider.offset = new Vector2(0f, 3f);

        // El aviso es un trigger en el mismo objeto que ElementalWall: si esta
        // en un hijo, Unity no le envia los eventos de trigger al script.
        BoxCollider2D warning = wall.AddComponent<BoxCollider2D>();
        warning.isTrigger = true;
        warning.size = new Vector2(2.4f, 3.2f);
        warning.offset = new Vector2(-2.8f, 3f);

        ElementalWall element = wall.AddComponent<ElementalWall>();
        SetProperty(element, "requiredElement", p => p.enumValueIndex = (int)GameElement.Rayo);
        SetProperty(element, "openAudio", p => p.objectReferenceValue = LoadAudio("Collectable"));
        SetProperty(element, "blockedAudio", p => p.objectReferenceValue = LoadAudio("Hurt"));
    }

    static void SetupEntities(Tilemap level)
    {
        // Jefe anterior
        GameObject oldBoss = GameObject.Find("Jefe");
        if (oldBoss != null) UnityEngine.Object.DestroyImmediate(oldBoss);

        // Jugador
        PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null)
        {
            SetFeet(player.gameObject, new Vector2(PlayerStartX, SurfaceY(level, PlayerStartX, FloorLevel)));
            SpriteRenderer sr = player.GetComponent<SpriteRenderer>();
            if (sr != null) sr.flipX = false;

            Health health = player.GetComponent<Health>();
            if (health != null) SetProperty(health, "maxHP", p => p.intValue = 3);

            if (player.GetComponent<PlayerAbilities>() == null)
                player.gameObject.AddComponent<PlayerAbilities>();

            PlayerMeleeAttack attack = player.GetComponent<PlayerMeleeAttack>();
            if (attack == null) attack = player.gameObject.AddComponent<PlayerMeleeAttack>();
            SetProperty(attack, "attackAudio", p => p.objectReferenceValue = LoadAudio("LandOnEnemy"));

            Debug.Log("[VillagePropsMigrator] Jugador preparado en " + player.transform.position);
        }
        else Debug.LogWarning("[VillagePropsMigrator] No se encontro PlayerController.");

        // Enemigos
        List<EnemyController> enemyList = new List<EnemyController>();
        foreach (EnemyController ec in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude))
        {
            if (ec.gameObject.name != "Jefe") enemyList.Add(ec);
        }

        // Orden estable para que la colocación sea siempre la misma.
        enemyList.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

        float[] enemyX = { -6.5f, 28f };
        float[] enemyCeiling = { FloorLevel, 13f };
        float[] patrol = { 2f, 1f };

        for (int i = 0; i < enemyList.Count; i++)
        {
            EnemyController e = enemyList[i];
            if (i < enemyX.Length)
            {
                float y = SurfaceY(level, enemyX[i], enemyCeiling[i]);
                SetFeet(e.gameObject, new Vector2(enemyX[i], y));
                e.gameObject.SetActive(true);

                if (e.Path != null)
                {
                    e.Path.startPosition = new Vector2(enemyX[i] - patrol[i], y);
                    e.Path.endPosition = new Vector2(enemyX[i] + patrol[i], y);
                }
            }
            else
            {
                e.gameObject.SetActive(false);
            }
        }

        // Jefe: clon agrandado de un enemigo
        if (enemyList.Count > 0)
        {
            GameObject boss = UnityEngine.Object.Instantiate(enemyList[0].gameObject);
            boss.name = "Jefe";
            Transform enemiesRoot = GameObject.Find("Enemies") != null ? GameObject.Find("Enemies").transform : null;
            if (enemiesRoot != null) boss.transform.SetParent(enemiesRoot, true);
            boss.transform.localScale = enemyList[0].transform.localScale * 2f;

            EnemyController bossController = boss.GetComponent<EnemyController>();
            if (bossController != null) bossController.Path = null;

            Health bossHealth = boss.GetComponent<Health>();
            if (bossHealth != null) bossHealth.SetMaxHP(5);

            SetFeet(boss, new Vector2(-20f, SurfaceY(level, -20f, ShelfLevel)));
            Debug.Log("[VillagePropsMigrator] Jefe preparado en " + boss.transform.position);
        }

        // Monedas
        List<TokenInstance> tokenList = new List<TokenInstance>(UnityEngine.Object.FindObjectsByType<TokenInstance>(FindObjectsInactive.Include));
        tokenList.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

        TokenInstance[] tokens = tokenList.ToArray();
        for (int i = 0; i < tokens.Length; i++)
        {
            if (i < TokenSpots.Length)
            {
                TokenSpot spot = TokenSpots[i];
                tokens[i].gameObject.SetActive(true);
                tokens[i].transform.position = new Vector3(spot.x, SurfaceY(level, spot.x, spot.ceiling) + spot.height, 0f);

                foreach (SpriteRenderer sr in tokens[i].GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr.sortingOrder < TokenSortingOrder) sr.sortingOrder = TokenSortingOrder;
                }

                RecordInstance(tokens[i].gameObject);
            }
            else
            {
                tokens[i].gameObject.SetActive(false);
            }
        }
        Debug.Log("[VillagePropsMigrator] Monedas colocadas: " + Mathf.Min(tokens.Length, TokenSpots.Length));

        // Zona de muerte
        DeathZone[] zones = UnityEngine.Object.FindObjectsByType<DeathZone>(FindObjectsInactive.Include);
        for (int i = 0; i < zones.Length; i++)
        {
            if (i == 0)
            {
                zones[i].gameObject.SetActive(true);
                zones[i].transform.position = new Vector3(5f, -9f, 0f);
                zones[i].transform.localScale = new Vector3(90f, 1f, 1f);
                RecordInstance(zones[i].gameObject);
            }
            else zones[i].gameObject.SetActive(false);
        }

        // Punto de reaparicion
        GameObject spawnObject = GameObject.Find("SpawnPoint");
        if (spawnObject != null)
        {
            if (spawnObject.GetComponent<SpawnPoint>() == null)
                spawnObject.AddComponent<SpawnPoint>();
            spawnObject.transform.position = new Vector3(PlayerStartX, SurfaceY(level, PlayerStartX, FloorLevel) + 0.41f, 0f);
            RecordInstance(spawnObject);
        }

        // Zona de victoria
        VictoryZone victory = UnityEngine.Object.FindAnyObjectByType<VictoryZone>(FindObjectsInactive.Include);
        if (victory != null)
        {
            victory.transform.position = new Vector3(VictoryX, SurfaceY(level, VictoryX, UpperLevel) + 1f, 0f);
            RecordInstance(victory.gameObject);
        }
    }

    static void SetupHud()
    {
        GameObject old = GameObject.Find(HudName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        GameObject root = new GameObject(HudName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(null);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        CreateHudText(root.transform, "Tokens", new Vector2(30f, -30f), new Vector2(520f, 60f), 34f, TextAlignmentOptions.Left, font);
        CreateHudText(root.transform, "Elemento", new Vector2(30f, -90f), new Vector2(520f, 60f), 34f, TextAlignmentOptions.Left, font);
        CreateHudText(root.transform, "Vida", new Vector2(30f, -150f), new Vector2(520f, 60f), 34f, TextAlignmentOptions.Left, font);
        CreateHudText(root.transform, "Mensaje", new Vector2(0f, 50f), new Vector2(1400f, 70f), 38f, TextAlignmentOptions.Center, font, true);

        root.AddComponent<HudController>();
    }

    static void CreateHudText(Transform parent, string name, Vector2 position, Vector2 size, float fontSize,
        TextAlignmentOptions alignment, TMP_FontAsset font, bool bottom = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = bottom ? new Vector2(0.5f, 0f) : new Vector2(0f, 1f);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = bottom ? new Vector2(0.5f, 0f) : new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color(0f, 0f, 0f, 0.85f);
        text.raycastTarget = false;
        text.text = string.Empty;
    }

    static void ConfigureModel()
    {
        GameController controller = UnityEngine.Object.FindAnyObjectByType<GameController>(FindObjectsInactive.Include);
        if (controller != null && controller.model != null)
        {
            controller.model.jumpModifier = JumpModifier;
            controller.model.jumpDeceleration = JumpDeceleration;
        }

        PlatformerModel shared = Simulation.GetModel<PlatformerModel>();
        if (shared != null)
        {
            shared.jumpModifier = JumpModifier;
            shared.jumpDeceleration = JumpDeceleration;
        }

        // Fisica del jugador: velocidad horizontal y salto ajustados a los
        // saltos del mapa (hueco 2, desnivel 2). Con la velocidad anterior el
        // salto por encima de +2 unidades duraba 1,54 s (9,2 u de recorrido) y
        // el jugador sobrevolaba las plataformas de 4 u, cayendo al vacio.
        PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null)
        {
            SetProperty(player, "maxSpeed", p => p.floatValue = PlayerMaxSpeed);
            SetProperty(player, "jumpTakeOffSpeed", p => p.floatValue = PlayerJumpTakeOffSpeed);
            PrefabUtility.RecordPrefabInstancePropertyModifications(player);
            RecordInstance(player.gameObject);

            Debug.Log($"[VillagePropsMigrator] Fisica del jugador: maxSpeed={PlayerMaxSpeed} " +
                      $"jumpTakeOffSpeed={PlayerJumpTakeOffSpeed} jumpModifier={JumpModifier} " +
                      $"v0={PlayerJumpTakeOffSpeed * JumpModifier:0.##} " +
                      $"v0 real={GetFloat(player, "jumpTakeOffSpeed") * JumpModifier:0.##}");
        }

        // Vida y fisica del jugador en el prefab. Enemy.prefab es un prefab
        // independiente (no es una variante), asi que los enemigos no cambian.
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab != null)
        {
            Health health = prefab.GetComponent<Health>();
            if (health != null)
            {
                SetProperty(health, "maxHP", p => p.intValue = 3);
                EditorUtility.SetDirty(health);
            }

            PlayerController prefabPlayer = prefab.GetComponent<PlayerController>();
            if (prefabPlayer != null)
            {
                SetProperty(prefabPlayer, "maxSpeed", p => p.floatValue = PlayerMaxSpeed);
                SetProperty(prefabPlayer, "jumpTakeOffSpeed", p => p.floatValue = PlayerJumpTakeOffSpeed);
                EditorUtility.SetDirty(prefabPlayer);
            }

            PrefabUtility.SavePrefabAsset(prefab);
        }
    }

    static void UpdateConfiner()
    {
        GameObject confiner = GameObject.Find("CinemachineConfiner");
        if (confiner == null) return;

        PolygonCollider2D polygon = confiner.GetComponent<PolygonCollider2D>();
        if (polygon == null) return;

        Vector3 origin = confiner.transform.position;
        polygon.points = new[]
        {
            new Vector2(-38f - origin.x, 40f - origin.y),
            new Vector2(46f - origin.x, 40f - origin.y),
            new Vector2(46f - origin.x, -12f - origin.y),
            new Vector2(-38f - origin.x, -12f - origin.y),
        };
    }

    static void SetFeet(GameObject go, Vector2 feet)
    {
        Collider2D col = MainCollider(go);
        float halfHeight = 0.25f;
        float offsetY = 0f;

        if (col != null)
        {
            float scale = Mathf.Abs(col.transform.lossyScale.y);
            halfHeight = ColliderHeight(col) * scale * 0.5f;
            offsetY = col.offset.y * scale;
        }

        go.transform.position = new Vector3(feet.x, feet.y - offsetY + halfHeight, go.transform.position.z);
        RecordInstance(go);
    }

    /// <summary>
    /// Collider solido mas alto del objeto (el que define los pies).
    /// </summary>
    static Collider2D MainCollider(GameObject go)
    {
        Collider2D best = null;
        float bestSize = -1f;

        foreach (Collider2D col in go.GetComponents<Collider2D>())
        {
            if (col.isTrigger) continue;
            float size = ColliderHeight(col) * Mathf.Abs(col.transform.lossyScale.y);
            if (size > bestSize)
            {
                bestSize = size;
                best = col;
            }
        }

        if (best != null) return best;

        foreach (Collider2D col in go.GetComponents<Collider2D>())
        {
            float size = ColliderHeight(col) * Mathf.Abs(col.transform.lossyScale.y);
            if (size > bestSize)
            {
                bestSize = size;
                best = col;
            }
        }

        return best;
    }

    /// <summary>
    /// Altura local del collider, independientemente del tipo.
    /// </summary>
    static float ColliderHeight(Collider2D col)
    {
        BoxCollider2D box = col as BoxCollider2D;
        if (box != null) return box.size.y;

        CapsuleCollider2D capsule = col as CapsuleCollider2D;
        if (capsule != null) return capsule.size.y;

        CircleCollider2D circle = col as CircleCollider2D;
        if (circle != null) return circle.radius * 2f;

        return col.bounds.size.y;
    }

    /// <summary>
    /// Quita los componentes SceneObjectGuid que el framework de tutoriales
    /// (com.unity.learn.iet-framework) deja en los objetos de la escena.
    /// Si se mantienen, al reabrir la escena en la misma sesión el gestor
    /// registra dos veces la misma clave y lanza una excepción.
    /// </summary>
    static void RemoveSceneObjectGuids()
    {
        int removed = 0;

        foreach (GameObject go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            foreach (Component component in go.GetComponents<Component>())
            {
                if (component == null) continue;
                if (component.GetType().Name != "SceneObjectGuid") continue;

                UnityEngine.Object.DestroyImmediate(component, true);
                removed++;
            }
        }

        if (removed > 0)
            Debug.Log("[VillagePropsMigrator] SceneObjectGuid eliminados: " + removed);
    }

    /// <summary>
    /// Guarda en la escena los cambios hechos por codigo sobre instancias de prefab.
    /// </summary>
    static void RecordInstance(GameObject go)
    {
        if (go == null) return;
        if (!PrefabUtility.IsPartOfPrefabInstance(go)) return;
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
    }

    /// <summary>
    /// Refresca las modificaciones de todas las instancias de prefab de la escena.
    /// </summary>
    static void RecordAllInstances()
    {
        foreach (GameObject go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(go)) continue;
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        }
    }

    static void SetProperty(UnityEngine.Object target, string propertyName, Action<SerializedProperty> apply)
    {
        if (target == null) return;

        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogWarning($"[VillagePropsMigrator] No se encontro la propiedad '{propertyName}' en {target.GetType().Name}.");
            return;
        }

        apply(property);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Valor actual de una propiedad serializada (para comprobar los cambios).</summary>
    static float GetFloat(UnityEngine.Object target, string propertyName)
    {
        if (target == null) return 0f;

        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(propertyName);
        return property == null ? 0f : property.floatValue;
    }

    static AudioClip LoadAudio(string clipName)
    {
        return AssetDatabase.LoadAssetAtPath<AudioClip>(AudioBase + clipName + ".wav");
    }

    static GameObject Spawn(string prefabName, Vector3 position, Transform parent, string objName)
    {
        string path = PrefabBase + prefabName + ".prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("[VillagePropsMigrator] Prefab no encontrado: " + path);
            return null;
        }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.name = objName;

        foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>(true))
        {
            // Los props van por detrás del jugador (5) y de las monedas (4).
            if (sr.sortingOrder < PropSortingOrder) sr.sortingOrder = PropSortingOrder;
        }

        RecordInstance(go);
        return go;
    }
}
