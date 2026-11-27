using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RPG.Control;
using UnityEngine;

namespace RPG.Game
{
    /// <summary>A saved character as listed on the character select screen.</summary>
    [Serializable]
    public class CharacterMeta
    {
        public string Name;
        public string Slug;
        public int CombatLevel = 3;
        public int TotalLevel = 21;
        public long LastPlayedTicks;
        public AppearanceData Look = new AppearanceData();

        public string SaveFile { get { return CharacterStore.SaveFileFor(Slug); } }
    }

    /// <summary>
    /// Character list on disk: one small JSON file per character (name, levels, looks) in
    /// persistentDataPath/characters, and the full save (char_&lt;slug&gt;.sav) next to the other saves.
    /// Names are unique (ignoring case and spaces).
    /// </summary>
    public static class CharacterStore
    {
        public const int MinName = 3, MaxName = 12;

        // Tests point this somewhere else so they never touch real characters.
        public static string Folder = null;

        static string Dir
        {
            get
            {
                string dir = Folder ?? Path.Combine(Application.persistentDataPath, "characters");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string SaveFileFor(string slug)
        {
            return "char_" + slug;
        }

        /// <summary>Lowercase letters and digits only: "Sir Bob" and "sirbob" are the same name.</summary>
        public static string Slug(string name)
        {
            StringBuilder slug = new StringBuilder();
            foreach (char c in (name ?? "").ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) slug.Append(c);
            }
            return slug.ToString();
        }

        /// <summary>Null if the name is OK to use, otherwise why not. Pure apart from the "taken" check.</summary>
        public static string Validate(string name, ICollection<string> takenSlugs)
        {
            name = name ?? "";
            if (name != name.Trim()) return "No spaces at the start or end.";
            if (name.Length < MinName) return "Names need at least " + MinName + " characters.";
            if (name.Length > MaxName) return "Names can be at most " + MaxName + " characters.";
            if (!char.IsLetter(name[0])) return "Names must start with a letter.";
            if (name.Contains("  ")) return "Only single spaces, please.";
            foreach (char c in name)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == ' ' || c == '-' || c == '_';
                if (!ok) return "Letters, numbers, spaces, - and _ only.";
            }
            if (takenSlugs != null && takenSlugs.Contains(Slug(name))) return "That name is already taken.";
            return null;
        }

        public static string Validate(string name)
        {
            HashSet<string> taken = new HashSet<string>();
            foreach (CharacterMeta meta in List()) taken.Add(meta.Slug);
            return Validate(name, taken);
        }

        public static List<CharacterMeta> List()
        {
            List<CharacterMeta> list = new List<CharacterMeta>();
            foreach (string file in Directory.GetFiles(Dir, "*.json"))
            {
                try
                {
                    CharacterMeta meta = JsonUtility.FromJson<CharacterMeta>(File.ReadAllText(file));
                    if (meta != null && !string.IsNullOrEmpty(meta.Slug)) list.Add(meta);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[RPG] skipping unreadable character file " + file + ": " + e.Message);
                }
            }
            list.Sort((a, b) => b.LastPlayedTicks.CompareTo(a.LastPlayedTicks));
            return list;
        }

        public static CharacterMeta Create(string name, AppearanceData look)
        {
            CharacterMeta meta = new CharacterMeta { Name = name, Slug = Slug(name), Look = look.Clone(), LastPlayedTicks = DateTime.UtcNow.Ticks };
            meta.Look.Name = name;
            Write(meta);
            return meta;
        }

        public static void Write(CharacterMeta meta)
        {
            File.WriteAllText(Path.Combine(Dir, meta.Slug + ".json"), JsonUtility.ToJson(meta, true));
        }

        public static void Delete(CharacterMeta meta)
        {
            string json = Path.Combine(Dir, meta.Slug + ".json");
            if (File.Exists(json)) File.Delete(json);
            string save = Path.Combine(Application.persistentDataPath, meta.SaveFile + ".sav");
            if (File.Exists(save)) File.Delete(save);
        }
    }
}
