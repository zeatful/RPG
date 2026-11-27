using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using RPG.Combat;
using RPG.Content;
using RPG.Control;
using RPG.Core;
using RPG.Gathering;
using RPG.UI;
using UnityEngine;
using UnityEngine.AI;

namespace RPG.Game
{
    /// <summary>
    /// -rpgShowcase &lt;dir&gt;: lines up every enemy type and every tree/rock type, photographs them, writes a
    /// sheet of all item icons, then quits. Used to check the art without playing.
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

        private IEnumerator Start()
        {
            string dir = RequestedDir;
            Directory.CreateDirectory(dir);
            WriteIconSheet(Path.Combine(dir, "icons.png"));

            yield return new WaitForSeconds(1.5f);
            if (DayNightCycle.Instance != null) DayNightCycle.Instance.TimeOfDay = 0.45f;
            RpgHud hud = FindFirstObjectByType<RpgHud>();
            if (hud != null) hud.enabled = false;

            GameObject player = GameObject.FindWithTag("Player");
            Vector3 origin = player.transform.position;
            player.GetComponent<NavMeshAgent>().Warp(origin + new Vector3(0, 0, -30f));

            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller != null) controller.enabled = false;
            Camera main = Camera.main;
            if (main != null) main.enabled = false;
            Camera camera = new GameObject("Showcase Camera").AddComponent<Camera>();
            camera.fieldOfView = 45f;

            // one of each enemy, in a row facing the camera
            List<EnemyProfile> lineup = new List<EnemyProfile>();
            foreach (string id in new[] { "thistlehog", "gnarl", "grey_wolf", "bandit", GameContent.BossId })
            {
                foreach (EnemyProfile enemy in EnemyProfile.All)
                {
                    if (enemy.Info != null && enemy.Info.Id == id) { lineup.Add(enemy); break; }
                }
            }
            for (int i = 0; i < lineup.Count; i++)
            {
                EnemyProfile enemy = lineup[i];
                AIController ai = enemy.GetComponent<AIController>();
                if (ai != null) ai.enabled = false;
                enemy.GetComponent<Fighter>().Cancel();
                Vector3 spot = origin + new Vector3((i - (lineup.Count - 1) * 0.5f) * 2.6f, 0, 0);
                NavMeshHit hit;
                if (NavMesh.SamplePosition(spot, out hit, 4f, NavMesh.AllAreas)) spot = hit.position;
                enemy.GetComponent<NavMeshAgent>().Warp(spot);
                enemy.transform.rotation = Quaternion.Euler(0, 200f, 0);
            }

            Aim(camera, origin + new Vector3(0, 1.0f, 0), new Vector3(0, 2.0f, -8.5f));
            yield return Shot(Path.Combine(dir, "enemies.png"));

            // close-ups of each creature
            for (int i = 0; i < lineup.Count; i++)
            {
                Vector3 at = lineup[i].transform.position;
                float size = Mathf.Max(1.2f, lineup[i].NameplateHeight);
                Aim(camera, at + new Vector3(0, size * 0.4f, 0), new Vector3(size * 0.9f, size * 0.55f, -size * 1.6f));
                yield return Shot(Path.Combine(dir, "enemy_" + lineup[i].Info.Id + ".png"));
            }

            // attack pose: make the wolf and slime lunge
            foreach (EnemyProfile enemy in lineup) Fighter.RaiseAttackResolved(enemy.gameObject, player, 0);
            yield return new WaitForSeconds(0.15f);
            Aim(camera, origin + new Vector3(0, 1.0f, 0), new Vector3(0, 2.0f, -8.5f));
            yield return Shot(Path.Combine(dir, "enemies_attack.png"));

            // one of each tree and rock in a row further away
            Vector3 grove = origin + new Vector3(0, 0, 14f);
            string[] nodes = { "pine", "oak", "willow", "copper", "tin", "iron" };
            for (int i = 0; i < nodes.Length; i++)
            {
                Vector3 spot = grove + new Vector3((i - 2.5f) * 4.2f, 0, nodes[i] == "copper" || nodes[i] == "tin" || nodes[i] == "iron" ? -4f : 0f);
                NavMeshHit hit;
                if (NavMesh.SamplePosition(spot, out hit, 5f, NavMesh.AllAreas)) spot = hit.position;
                ResourceNode.Create(GameContent.Nodes[nodes[i]], spot, 30f * i);
            }
            Aim(camera, grove + new Vector3(0, 2f, -2f), new Vector3(0, 4.5f, -16f));
            yield return Shot(Path.Combine(dir, "nodes.png"));

            Application.Quit(0);
        }

        static void Aim(Camera camera, Vector3 target, Vector3 offset)
        {
            camera.transform.position = target + offset;
            camera.transform.LookAt(target);
        }

        IEnumerator Shot(string path)
        {
            yield return new WaitForSeconds(0.3f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
        }

        static void WriteIconSheet(string path)
        {
            List<string> ids = new List<string>(GameContent.Items.Keys);
            int columns = 8;
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
