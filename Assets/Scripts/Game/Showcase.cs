using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using RPG.Combat;
using RPG.Content;
using RPG.Control;
using RPG.Core;
using RPG.Gathering;
using RPG.Items;
using RPG.UI;
using RPG.World;
using UnityEngine;
using UnityEngine.AI;

namespace RPG.Game
{
    /// <summary>
    /// -rpgShowcase &lt;dir&gt;: photographs the world (aerial view, each town), lines up every monster and
    /// every tree/rock type, shows the HUD at a shop, writes a sheet of all item icons, then quits.
    /// Used to check the art without playing.
    /// </summary>
    public class Showcase : MonoBehaviour
    {
        public static string RequestedDir
        {
            get
            {
                string[] args = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(args, "-rpgShowcase");
                return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
            }
        }

        Camera shotCamera;

        private IEnumerator Start()
        {
            string dir = RequestedDir;
            Directory.CreateDirectory(dir);
            WriteIconSheet(Path.Combine(dir, "icons.png"));

            yield return new WaitForSeconds(1.5f);
            if (DayNightCycle.Instance != null) DayNightCycle.Instance.TimeOfDay = 0.42f;
            GameObject player = GameObject.FindWithTag("Player");
            RpgHud hud = FindFirstObjectByType<RpgHud>();

            // 1. the HUD at Brookhollow's general store, from the normal game camera
            if (WorldBuilder.Root != null)
            {
                Station shop = null;
                foreach (Station station in Station.All)
                {
                    if (station.Kind == StationKind.Shop && station.Town == "Brookhollow" && station.GetComponent<Animator>() == null) shop = station;
                }
                if (shop != null)
                {
                    player.GetComponent<StationUser>().Use(shop);
                    yield return new WaitForSeconds(6f);
                    yield return Shot(Path.Combine(dir, "hud_shop.png"));
                    player.GetComponent<StationUser>().Close();
                }
            }

            if (hud != null) hud.enabled = false;
            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller != null) controller.enabled = false;
            Camera main = Camera.main;
            if (main != null) main.enabled = false;
            shotCamera = new GameObject("Showcase Camera").AddComponent<Camera>();
            shotCamera.fieldOfView = 45f;
            shotCamera.farClipPlane = 900f;
            Vector3 origin = player.transform.position;
            player.GetComponent<NavMeshAgent>().Warp(origin + new Vector3(0, 0, -6f));

            if (WorldBuilder.Root != null)
            {
                RenderSettings.fogStartDistance = 400f;
                RenderSettings.fogEndDistance = 900f;
                Aim(new Vector3(0, 0, 0), new Vector3(0, 430f, -250f));
                yield return Shot(Path.Combine(dir, "world_aerial.png"));
                foreach (TownInfo town in WorldLayout.Towns)
                {
                    Vector3 c = WorldBuilder.Ground(town.Center.x, town.Center.y);
                    Aim(c, new Vector3(-22f, 30f, -30f));
                    yield return Shot(Path.Combine(dir, "town_" + town.Id + ".png"));
                }
                Vector3 lair = WorldBuilder.BossLairPosition;
                Aim(lair, new Vector3(-10f, 12f, -16f));
                yield return Shot(Path.Combine(dir, "boss_lair.png"));
                RenderSettings.fogStartDistance = 70f;
                RenderSettings.fogEndDistance = 210f;
            }

            // 2. one of each monster, in rows facing the camera (in the open field south of town)
            Vector3 field = WorldBuilder.Root != null ? WorldBuilder.Ground(0, -225) : origin;
            List<EnemyProfile> lineup = new List<EnemyProfile>();
            HashSet<string> seen = new HashSet<string>();
            foreach (EnemyProfile enemy in EnemyProfile.All)
            {
                if (enemy.Info != null && seen.Add(enemy.Info.Id)) lineup.Add(enemy);
            }
            lineup.Sort((a, b) => a.Info.Level.CompareTo(b.Info.Level));
            int perRow = 6;
            for (int i = 0; i < lineup.Count; i++)
            {
                EnemyProfile enemy = lineup[i];
                AIController ai = enemy.GetComponent<AIController>();
                if (ai != null) ai.enabled = false;
                BossController boss = enemy.GetComponent<BossController>();
                if (boss != null) boss.enabled = false;
                enemy.GetComponent<Fighter>().Cancel();
                int row = i / perRow, column = i % perRow;
                int inRow = Mathf.Min(perRow, lineup.Count - row * perRow);
                Vector3 spot = field + new Vector3((column - (inRow - 1) * 0.5f) * 3.2f, 0, row * 4.5f);
                NavMeshHit hit;
                if (NavMesh.SamplePosition(spot, out hit, 4f, NavMesh.AllAreas)) spot = hit.position;
                enemy.GetComponent<NavMeshAgent>().Warp(spot);
                enemy.transform.rotation = Quaternion.Euler(0, 180f, 0);
            }
            yield return new WaitForSeconds(0.5f);
            Aim(field + new Vector3(0, 1.2f, 2.5f), new Vector3(0, 6f, -15f));
            yield return Shot(Path.Combine(dir, "enemies.png"));

            for (int i = 0; i < lineup.Count; i++)
            {
                Vector3 at = lineup[i].transform.position;
                float size = Mathf.Max(1.2f, lineup[i].NameplateHeight);
                Aim(at + new Vector3(0, size * 0.4f, 0), new Vector3(size * 0.9f, size * 0.55f, -size * 1.6f));
                yield return Shot(Path.Combine(dir, "enemy_" + lineup[i].Info.Id + ".png"));
            }

            // 3. one of each tree and rock, in two rows
            Vector3 grove = WorldBuilder.Root != null ? WorldBuilder.Ground(-20, -228) : origin + new Vector3(0, 0, 14f);
            string[] trees = { "pine", "oak", "willow", "maple", "yew", "ashwood" };
            string[] rocks = { "copper", "tin", "iron", "coal", "verdite", "cobalt", "cinderite" };
            PlaceRow(trees, grove + new Vector3(0, 0, 6f), 4.6f);
            PlaceRow(rocks, grove, 3.0f);
            Aim(grove + new Vector3(0, 2f, 3f), new Vector3(0, 7f, -19f));
            yield return Shot(Path.Combine(dir, "nodes.png"));

            Application.Quit(0);
        }

        static void PlaceRow(string[] ids, Vector3 centre, float spacing)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                Vector3 spot = centre + new Vector3((i - (ids.Length - 1) * 0.5f) * spacing, 0, 0);
                if (WorldBuilder.Root != null) spot = WorldBuilder.Ground(spot.x, spot.z);
                ResourceNode.Create(GameContent.Nodes[ids[i]], spot, 30f * i);
            }
        }

        void Aim(Vector3 target, Vector3 offset)
        {
            shotCamera.transform.position = target + offset;
            shotCamera.transform.LookAt(target);
        }

        IEnumerator Shot(string path)
        {
            yield return new WaitForSeconds(0.4f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
        }

        static void WriteIconSheet(string path)
        {
            List<string> ids = new List<string>(GameContent.Items.Keys);
            int columns = 10;
            int cell = 72;
            int rows = (ids.Count + columns - 1) / columns;
            Texture2D sheet = new Texture2D(columns * cell, rows * cell, TextureFormat.RGBA32, false);
            Color background = new Color(0.2f, 0.18f, 0.15f, 1f);
            Color[] fill = new Color[sheet.width * sheet.height];
            for (int i = 0; i < fill.Length; i++) fill[i] = background;
            sheet.SetPixels(fill);

            for (int i = 0; i < ids.Count; i++)
            {
                Texture2D icon = ItemIcons.Get(ids[i]);
                int cx = (i % columns) * cell + 4;
                int cy = (rows - 1 - i / columns) * cell + 4;
                int size = cell - 8;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        Color c = icon.GetPixel(x * icon.width / size, y * icon.height / size);
                        if (c.a > 0.01f) sheet.SetPixel(cx + x, cy + y, Color.Lerp(background, c, c.a));
                    }
            }
            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            File.WriteAllText(Path.ChangeExtension(path, ".txt"), string.Join("\n", ids.ToArray()));
        }
    }
}
