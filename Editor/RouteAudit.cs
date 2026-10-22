using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Auditoria temporal: simula la fisica real del jugador sobre la ruta del
/// mapa y comprueba que todos los saltos son ejecutables, que los pasillos
/// no estan tapados por paredes y que los props y las monedas estan sobre
/// el terreno.
///
/// v0 = jumpTakeOffSpeed * jumpModifier, vx = maxSpeed, g = 9.81,
/// colisionador: capsula (0.32, 0.54) con offset (-0.09, -0.14).
///
/// Se ejecuta creando "_audit_route.flag" en la raiz del proyecto.
/// </summary>
[InitializeOnLoad]
public static class RouteAudit
{
    const string FlagName = "_audit_route.flag";
    const float Gravity = 9.81f;
    const float MaxSpeed = 4f;
    const float TakeOff = 8f;

    const float ColliderRight = 0.07f;   // borde delantero hacia la derecha
    const float ColliderLeft = -0.25f;   // borde delantero hacia la izquierda
    const float ColliderHeight = 0.54f;

    const float StartX = -28f;    // donde aparece el jugador
    const float VictoryX = 35f;   // zona de victoria del nivel superior

    // Ruta del mapa: xMin, xMax, top de cada plataforma en orden de recorrido.
    static readonly float[][] Route =
    {
        new float[] { -32f, 6f, 0f },      // 0 suelo inicial
        new float[] { 8f, 12f, 2f },       // 1 foso
        new float[] { 14f, 32f, 0f },      // 2 suelo derecho
        new float[] { 34f, 38f, 2f },      // 3 escalera 1
        new float[] { 40f, 44f, 4f },      // 4 escalera 2
        new float[] { 34f, 38f, 6f },      // 5 escalera 3
        new float[] { 40f, 44f, 8f },      // 6 escalera 4
        new float[] { 34f, 38f, 10f },     // 7 escalera 5
        new float[] { 26f, 32f, 12f },     // 8 plataforma ancha
        new float[] { -34f, 24f, 14f },    // 9 estante
        new float[] { -32f, -28f, 16f },   // 10 peldaño A
        new float[] { -26f, -22f, 18f },   // 11 peldaño B
        new float[] { -32f, -28f, 20f },   // 12 peldaño C
        new float[] { -26f, -22f, 22f },   // 13 peldaño D
        new float[] { -32f, -28f, 24f },   // 14 peldaño E
        new float[] { -26f, -22f, 26f },   // 15 peldaño F
        new float[] { -20f, -16f, 28f },   // 16 peldaño G
        new float[] { -14f, -10f, 30f },   // 17 peldaño H
        new float[] { -8f, -2f, 30f },     // 18 peldaño I
        new float[] { 0f, 44f, 30f },      // 19 losa superior
    };

    static double nextCheck;
    static List<Block> blocks;

    static RouteAudit()
    {
        EditorApplication.update += WatchFlag;
    }

    static void WatchFlag()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 0.5;

        string flag = Path.Combine(Directory.GetParent(Application.dataPath).FullName, FlagName);
        if (!File.Exists(flag)) return;

        Run();
        File.Delete(flag);
    }

    struct Block
    {
        public float xMin, xMax, top, bottom;
    }

    static void Run()
    {
        blocks = ReadBlocks();

        Debug.Log($"[RouteAudit] Bloques: {blocks.Count}. v0={TakeOff} vx={MaxSpeed} g={Gravity}");

        int fails = 0;
        fails += CheckRoute();
        fails += CheckProps("Decor");
        fails += CheckProps("TokenSpots");

        Debug.Log(fails == 0
            ? "[RouteAudit] RESULTADO: OK, toda la ruta es jugable."
            : $"[RouteAudit] RESULTADO: {fails} problemas.");
    }

    static List<Block> ReadBlocks()
    {
        List<Block> list = new List<Block>();

        FieldInfo field = typeof(VillagePropsMigrator).GetField("Blocks",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (field == null)
        {
            Debug.LogError("[RouteAudit] No se encontro el array Blocks.");
            return list;
        }

        Array raw = (Array)field.GetValue(null);
        Type type = raw.GetType().GetElementType();
        FieldInfo xMin = type.GetField("xMin"), xMax = type.GetField("xMax");
        FieldInfo top = type.GetField("top"), bottom = type.GetField("bottom");

        foreach (object item in raw)
        {
            list.Add(new Block
            {
                xMin = (float)xMin.GetValue(item),
                xMax = (float)xMax.GetValue(item),
                top = (float)top.GetValue(item),
                bottom = (float)bottom.GetValue(item),
            });
        }

        return list;
    }

    static Block? Find(float xMin, float xMax, float top)
    {
        foreach (Block b in blocks)
        {
            if (Mathf.Abs(b.xMin - xMin) < 0.01f && Mathf.Abs(b.xMax - xMax) < 0.01f && Mathf.Abs(b.top - top) < 0.01f)
                return b;
        }
        return null;
    }

    // ------------------------------------------------------------------- ruta

    static int CheckRoute()
    {
        int fails = 0;
        int n = Route.Length;

        // Posiciones reales de despegue (en la plataforma origen) y de
        // aterrizaje (en la plataforma destino) de cada salto.
        float[] takeoff = new float[n];
        float[] landing = new float[n];

        for (int i = 0; i < n - 1; i++)
        {
            Block? src = Find(Route[i][0], Route[i][1], Route[i][2]);
            Block? dst = Find(Route[i + 1][0], Route[i + 1][1], Route[i + 1][2]);

            if (src == null || dst == null)
            {
                Debug.Log($"[RouteAudit] FALLO: la plataforma {i} ({Route[i][0]}..{Route[i][1]} top {Route[i][2]}) " +
                          $"o la {i + 1} no existe en la geometria.");
                fails++;
                continue;
            }

            // El jugador llega a la plataforma y solo puede salir desde donde
            // puede caminar: si una pared tapa el pasillo, el tramo es
            // imposible aunque la plataforma sea la correcta.
            float entryX = (i == 0) ? StartX : landing[i];
            float walkLeft, walkRight;
            Block? blocker;
            WalkableRange(src.Value, entryX, out walkLeft, out walkRight, out blocker);

            bool drop = dst.Value.top < src.Value.top;
            float x0, xl;
            string why;
            if (!IsReachable(src.Value, dst.Value, drop, walkLeft, walkRight, out x0, out xl, out why))
            {
                if (blocker != null)
                {
                    Debug.Log($"[RouteAudit] FALLO: {Describe(src.Value)} -> {Describe(dst.Value)}. " +
                              $"el pasillo esta tapado por {Describe(blocker.Value)} " +
                              $"(se puede caminar de x={walkLeft:0.##} a x={walkRight:0.##}). {why}");
                }
                else
                {
                    Debug.Log($"[RouteAudit] FALLO: {Describe(src.Value)} -> {Describe(dst.Value)}. {why}");
                }
                fails++;
                continue;
            }

            takeoff[i] = x0;
            landing[i + 1] = xl;
        }

        // El jugador tiene que poder llegar a la zona de victoria.
        int last = n - 1;
        Block? final = Find(Route[last][0], Route[last][1], Route[last][2]);
        if (final != null)
        {
            float walkLeft, walkRight;
            Block? blocker;
            WalkableRange(final.Value, landing[last], out walkLeft, out walkRight, out blocker);

            if (VictoryX < walkLeft - 0.001f || VictoryX > walkRight + 0.001f)
            {
                Debug.Log($"[RouteAudit] FALLO: la zona de victoria (x={VictoryX}) no se puede alcanzar a pie " +
                          $"desde {Describe(final.Value)} (se camina de x={walkLeft:0.##} a x={walkRight:0.##}).");
                fails++;
            }
        }

        return fails;
    }

    static string Describe(Block b)
    {
        return $"[x {b.xMin:0.##}..{b.xMax:0.##} top {b.top:0.##}]";
    }

    /// <summary>
    /// Tramo de la plataforma que el jugador puede recorrer caminando desde
    /// x0. Si una pared baja hasta la superficie, el tramo se corta ahi.
    /// </summary>
    static void WalkableRange(Block platform, float x0, out float left, out float right, out Block? blocker)
    {
        left = platform.xMin;
        right = platform.xMax;
        blocker = null;

        for (float x = x0; x <= platform.xMax + 0.001f; x += 0.05f)
        {
            if (CanStand(x, platform)) continue;
            right = x - 0.05f;
            blocker = FindBlocker(x, platform);
            break;
        }

        for (float x = x0; x >= platform.xMin - 0.001f; x -= 0.05f)
        {
            if (CanStand(x, platform)) continue;
            left = x + 0.05f;
            blocker = FindBlocker(x, platform);
            break;
        }
    }

    /// <summary>Bloque que impide al jugador estar de pie en x sobre la plataforma.</summary>
    static Block? FindBlocker(float x, Block platform)
    {
        float head = platform.top + ColliderHeight;

        foreach (Block b in blocks)
        {
            if (b.top <= platform.top + 0.01f) continue;   // queda por debajo de los pies
            if (b.bottom >= head) continue;                // muy alto para estorbar
            if (x + ColliderRight <= b.xMin || x + ColliderLeft >= b.xMax) continue;
            return b;
        }

        return null;
    }

    /// <summary>Busca una forma de pasar de source a target con la fisica real.</summary>
    static bool IsReachable(Block source, Block target, bool drop, float walkLeft, float walkRight,
                             out float takeoffX, out float landingX, out string why)
    {
        why = "";
        takeoffX = 0f;
        landingX = 0f;

        float rise = target.top - source.top;

        if (!drop && rise > TakeOff * TakeOff / (2f * Gravity))
        {
            why = $"la altura ({rise:0.##}) supera el apice del salto.";
            return false;
        }

        bool right = target.xMin >= source.xMax - 0.001f;
        bool left = target.xMax <= source.xMin + 0.001f;

        int[] dirs = right ? new[] { 1 } : left ? new[] { -1 } : new[] { 1, -1 };

        float bestReach = float.MinValue;
        string bestWhy = "";

        // Solo se prueban los puntos de despegue que se pueden alcanzar a pie.
        float from = Mathf.Max(source.xMin + 0.05f, walkLeft);
        float to = Mathf.Min(source.xMax - 0.05f, walkRight);

        foreach (int dir in dirs)
        {
            for (float x0 = from; x0 <= to + 0.001f; x0 += 0.05f)
            {
                if (!CanStand(x0, source)) continue;

                // Deriva inmediata o retardada (para los mantles).
                for (float delay = 0f; delay <= 1.2f; delay += 0.05f)
                {
                    float reach, xl;
                    string reason;
                    if (Simulate(source, target, x0, dir, delay, drop, out reach, out xl, out reason))
                    {
                        takeoffX = x0;
                        landingX = xl;
                        return true;
                    }

                    if (reach > bestReach)
                    {
                        bestReach = reach;
                        bestWhy = reason;
                    }
                }
            }
        }

        why = bestWhy.Length > 0
            ? $"no hay despegue valido ({bestWhy})."
            : "no hay despegue valido.";
        return false;
    }

    /// <summary>El jugador tiene sitio para estar de pie en x sobre la superficie.</summary>
    static bool CanStand(float x, Block source)
    {
        float head = source.top + ColliderHeight;

        foreach (Block b in blocks)
        {
            if (b.top <= source.top + 0.01f) continue;          // queda por debajo de los pies
            if (b.bottom >= head) continue;                      // muy alto para estorbar
            if (x + ColliderRight <= b.xMin || x + ColliderLeft >= b.xMax) continue;
            return false;
        }

        return true;
    }

    /// <summary>Simula el vuelo. Devuelve true si el jugador aterriza en target.</summary>
    static bool Simulate(Block source, Block target, float x0, int dir, float delay, bool drop,
                          out float reach, out float landingX, out string why)
    {
        const float dt = 1f / 240f;

        float x = x0;
        float y = source.top;              // pies
        float vy = drop ? 0f : TakeOff;
        float vx = 0f;
        float t = 0f;

        reach = float.MinValue;
        landingX = 0f;
        why = "";

        for (int step = 0; step < 2000; step++)
        {
            t += dt;
            if (t >= delay) vx = dir * MaxSpeed;

            float ny;
            bool onSource = drop && vy <= 0f && y <= source.top + 0.001f &&
                            x + ColliderRight > source.xMin && x + ColliderLeft < source.xMax;
            if (onSource)
            {
                // El jugador sigue caminando sobre la plataforma de origen.
                vy = 0f;
                ny = source.top;
            }
            else
            {
                vy -= Gravity * dt;
                ny = y + vy * dt;
            }

            float nx = x + vx * dt;

            float leadOld = x + (dir > 0 ? ColliderRight : ColliderLeft);
            float leadNew = nx + (dir > 0 ? ColliderRight : ColliderLeft);

            // Colisiones horizontales: chocar con la cara de un bloque.
            if (vx != 0f)
            {
                foreach (Block b in blocks)
                {
                    if (b.top <= ny + 0.001f) continue;              // se pasa por encima
                    if (ny + ColliderHeight <= b.bottom) continue;    // pasa por debajo
                    bool hits = dir > 0
                        ? (leadNew > b.xMin && leadOld <= b.xMin + 0.0001f)
                        : (leadNew < b.xMax && leadOld >= b.xMax - 0.0001f);
                    if (hits)
                    {
                        why = $"choca con la cara de {Describe(b)}";
                        reach = ny - source.top;
                        return false;
                    }
                }
            }

            // Colisiones verticales: golpear con la cabeza un techo.
            foreach (Block b in blocks)
            {
                if (b.bottom <= source.top + 0.01f) continue;
                if (leadNew <= b.xMin || nx + ColliderLeft >= b.xMax) continue;
                if (ny + ColliderHeight > b.bottom && ny < b.top)
                {
                    ny = b.bottom - ColliderHeight;
                    vy = 0f;
                }
            }

            float prevY = y;
            x = nx;
            y = ny;

            // Aterrizaje en la plataforma destino: los pies bajan hasta su
            // superficie (o la plataforma esta por debajo y se cae a ella).
            if (prevY > target.top && y <= target.top)
            {
                bool over = x + ColliderRight > target.xMin && x + ColliderLeft < target.xMax;
                if (over)
                {
                    reach = y - source.top;
                    landingX = x;
                    return true;
                }

                why = $"aterriza en x={x:0.##} fuera de {Describe(target)}";
                reach = y - source.top;
                return false;
            }

            if (y < source.top - 2f)
            {
                why = "cae al vacio";
                reach = y - source.top;
                return false;
            }
        }

        why = "vuelo sin final";
        return false;
    }

    // ------------------------------------------------------------- superficies

    static int CheckProps(string fieldName)
    {
        int fails = 0;

        FieldInfo field = typeof(VillagePropsMigrator).GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Static);
        if (field == null) return 0;

        Array raw = (Array)field.GetValue(null);
        Type type = raw.GetType().GetElementType();
        FieldInfo xField = type.GetField("x");
        FieldInfo nameField = type.GetField("objName");

        foreach (object item in raw)
        {
            float x = (float)xField.GetValue(item);
            string name = nameField != null ? (string)nameField.GetValue(item) : fieldName;

            float cx = Mathf.Floor(x);
            bool hasColumn = false;
            foreach (Block b in blocks)
            {
                if (cx >= b.xMin && cx < b.xMax) { hasColumn = true; break; }
            }

            if (!hasColumn)
            {
                Debug.Log($"[RouteAudit] FALLO: {name} en x={x:0.##} esta fuera del terreno.");
                fails++;
            }
        }

        return fails;
    }
}
