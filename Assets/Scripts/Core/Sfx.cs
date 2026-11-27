using System.Collections.Generic;
using UnityEngine;

namespace RPG.Core
{
    /// <summary>
    /// Plays the CC0 sound effects in Resources/Audio (see CREDITS.txt there). Clips are grouped by name
    /// without the trailing number ("slash" = slash1..slash6) and one is picked at random with a little
    /// pitch variation. Uses a small pool of AudioSources; world sounds are partly spatial.
    /// </summary>
    public static class Sfx
    {
        const int PoolSize = 16;

        static readonly Dictionary<string, AudioClip[]> groups = new Dictionary<string, AudioClip[]>();
        static readonly List<AudioSource> pool = new List<AudioSource>();
        static GameObject host;
        static int next;
        static bool loaded;

        /// <summary>Effects volume 0..1 (the settings menu also sets AudioListener.volume as master).</summary>
        public static float Volume = 0.8f;

        // Sounds played this run, for the play-through test.
        public static int PlayedCount { get; private set; }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            Dictionary<string, List<AudioClip>> found = new Dictionary<string, List<AudioClip>>();
            foreach (AudioClip clip in Resources.LoadAll<AudioClip>("Audio"))
            {
                string name = clip.name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
                List<AudioClip> list;
                if (!found.TryGetValue(name, out list)) found[name] = list = new List<AudioClip>();
                list.Add(clip);
            }
            foreach (KeyValuePair<string, List<AudioClip>> pair in found) groups[pair.Key] = pair.Value.ToArray();
        }

        public static bool Has(string group)
        {
            Load();
            return groups.ContainsKey(group);
        }

        static AudioSource Source()
        {
            if (host == null)
            {
                host = new GameObject("Sound effects");
                Object.DontDestroyOnLoad(host);
                pool.Clear();
            }
            while (pool.Count < PoolSize)
            {
                GameObject go = new GameObject("sfx");
                go.transform.SetParent(host.transform, false);
                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 10f;
                source.maxDistance = 45f;
                source.dopplerLevel = 0f;
                pool.Add(source);
            }
            // reuse the oldest source that isn't playing, else steal round-robin
            for (int i = 0; i < pool.Count; i++)
            {
                AudioSource candidate = pool[(next + i) % pool.Count];
                if (!candidate.isPlaying)
                {
                    next = (next + i + 1) % pool.Count;
                    return candidate;
                }
            }
            AudioSource stolen = pool[next];
            next = (next + 1) % pool.Count;
            return stolen;
        }

        /// <summary>A UI / non-positional sound.</summary>
        public static void Ui(string group, float volume = 0.6f)
        {
            Play(group, null, volume, 0.04f);
        }

        /// <summary>A sound at a place in the world (partly spatial so it fades with distance).</summary>
        public static void At(string group, Vector3 position, float volume = 1f, float pitchJitter = 0.08f)
        {
            Play(group, position, volume, pitchJitter);
        }

        static void Play(string group, Vector3? position, float volume, float pitchJitter)
        {
            Load();
            AudioClip[] clips;
            if (string.IsNullOrEmpty(group) || !groups.TryGetValue(group, out clips) || clips.Length == 0) return;
            AudioSource source = Source();
            source.clip = clips[Random.Range(0, clips.Length)];
            source.volume = Mathf.Clamp01(volume * Volume);
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            if (position.HasValue)
            {
                source.transform.position = position.Value;
                source.spatialBlend = 0.6f;
            }
            else
            {
                source.spatialBlend = 0f;
            }
            source.Play();
            PlayedCount++;
        }
    }
}
