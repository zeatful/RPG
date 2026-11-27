using System.Collections.Generic;
using UnityEngine;

namespace RPG.Core
{
    /// <summary>Chat-box style message log shown by the HUD.</summary>
    public static class GameLog
    {
        public const int MaxMessages = 8;

        public static readonly Color Normal = new Color(0.95f, 0.95f, 0.95f);
        public static readonly Color Warning = new Color(1f, 0.55f, 0.45f);
        public static readonly Color LevelUp = new Color(0.55f, 0.85f, 1f);
        public static readonly Color Rare = new Color(1f, 0.8f, 0.2f);

        public struct Message
        {
            public string Text;
            public Color Color;
            public float Time;
        }

        static readonly List<Message> messages = new List<Message>();

        public static IList<Message> Messages { get { return messages; } }

        public static void Add(string text)
        {
            Add(text, Normal);
        }

        public static void Add(string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            Message message = new Message();
            message.Text = text;
            message.Color = color;
            message.Time = UnityEngine.Time.time;
            messages.Add(message);
            while (messages.Count > MaxMessages) messages.RemoveAt(0);
        }

        public static void Clear()
        {
            messages.Clear();
        }
    }
}
