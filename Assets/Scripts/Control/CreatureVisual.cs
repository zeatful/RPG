using RPG.Combat;
using RPG.Content;
using RPG.Core;
using UnityEngine;
using UnityEngine.AI;

namespace RPG.Control
{
    /// <summary>
    /// Replaces an enemy's human body with its own look: a CC0 model (wolf, slime) or a low-poly creature
    /// built in code (thistlehog, gnarl). The original humanoid stays as an invisible "puppet" so its
    /// animation events still time the attacks; this component animates the new body from movement,
    /// attacks and death.
    /// </summary>
    public class CreatureVisual : MonoBehaviour
    {
        enum Motion { Model, Hop, Waddle }

        /// <summary>Height of the visual in world units (after the enemy's own scale).</summary>
        public float Height { get; private set; }

        Transform pivot;
        Motion motion;
        Animation anim;
        string walkClip, idleClip, attackClip, deathClip;
        float walkClipSpeed = 1.6f;

        NavMeshAgent agent;
        Health health;
        float lunge;
        float deathTime;
        bool wasDead;
        float gait;

        /// <summary>
        /// Give this enemy a custom body. Returns false (and changes nothing) for enemies that keep the human
        /// model. tint is set to false for creatures whose colours are hand-picked and shouldn't be tinted.
        /// </summary>
        public static bool Apply(GameObject enemy, EnemyInfo info, out bool tint)
        {
            tint = true;
            RpgArt art = RpgArt.Instance;
            GameObject body = null;
            Motion motion = Motion.Model;
            float height = 0f;

            switch (info.Id)
            {
                case "grey_wolf":
                    if (art == null || art.Wolf == null) return false;
                    body = Normalized(art.Wolf, 1.15f, out height);
                    RpgUtil.Recolor(body, null, RpgUtil.FromHex(info.TintHex, Color.gray));
                    tint = false;
                    break;
                case "ash_wolf":
                    if (art == null || art.Wolf == null) return false;
                    body = Normalized(art.Wolf, 1.3f, out height);
                    RpgUtil.Recolor(body, null, RpgUtil.FromHex(info.TintHex, Color.gray));
                    AddEmbers(body.transform, height, 6);
                    tint = false;
                    break;
                case "old_gullet":
                    if (art == null || art.Slime == null) return false;
                    body = Normalized(art.Slime, 1.7f, out height);
                    break;
                case "bog_slime":
                    if (art == null || art.Slime == null) return false;
                    body = Normalized(art.Slime, 1.0f, out height);
                    RpgUtil.Recolor(body, "Body", RpgUtil.FromHex("#6E7A3A", Color.green));
                    tint = false;
                    break;
                case "boar":
                    body = BuildBoar(out height);
                    motion = Motion.Hop;
                    tint = false;
                    break;
                case "stone_golem":
                    body = BuildGolem(false, out height);
                    motion = Motion.Waddle;
                    tint = false;
                    break;
                case "magma_golem":
                    body = BuildGolem(true, out height);
                    motion = Motion.Waddle;
                    tint = false;
                    break;
                case "cinder_imp":
                    body = BuildImp(out height);
                    motion = Motion.Hop;
                    tint = false;
                    break;
                case "thistlehog":
                    body = BuildThistlehog(out height);
                    motion = Motion.Hop;
                    tint = false;
                    break;
                case "gnarl":
                    body = BuildGnarl(out height);
                    motion = Motion.Waddle;
                    tint = false;
                    break;
                default:
                    return false;
            }

            // hide the human but keep its animator running (its Hit() events drive attacks)
            foreach (Renderer renderer in enemy.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            Animator animator = enemy.GetComponent<Animator>();
            if (animator != null) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            CreatureVisual visual = enemy.GetComponent<CreatureVisual>();
            if (visual == null) visual = enemy.AddComponent<CreatureVisual>();
            visual.Attach(body, motion, height);
            return true;
        }

        void Attach(GameObject body, Motion bodyMotion, float height)
        {
            if (pivot != null) Destroy(pivot.gameObject);
            pivot = body.transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = Vector3.zero;
            pivot.localRotation = Quaternion.identity;
            motion = bodyMotion;
            Height = height * transform.lossyScale.y;

            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            anim = body.GetComponentInChildren<Animation>();
            if (anim != null)
            {
                foreach (AnimationState state in anim)
                {
                    string name = state.name.ToLower();
                    if (name.Contains("walk") || name.Contains("run")) walkClip = state.name;
                    else if (name.Contains("idle")) idleClip = state.name;
                    else if (name.Contains("attack")) attackClip = state.name;
                    else if (name.Contains("death") || name.Contains("die")) deathClip = state.name;
                }
                if (walkClip != null) anim[walkClip].wrapMode = WrapMode.Loop;
                if (idleClip != null)
                {
                    anim[idleClip].wrapMode = WrapMode.Loop;
                    anim.Play(idleClip);
                }
                if (attackClip != null) anim[attackClip].wrapMode = WrapMode.Once;
                if (deathClip != null) anim[deathClip].wrapMode = WrapMode.ClampForever;
            }
        }

        private void OnEnable() { Fighter.onAttackResolved += OnAttack; }
        private void OnDisable() { Fighter.onAttackResolved -= OnAttack; }

        void OnAttack(GameObject attacker, GameObject defender, int damage)
        {
            if (attacker != gameObject || pivot == null) return;
            lunge = 1f;
            if (anim != null && attackClip != null)
            {
                anim.CrossFade(attackClip, 0.05f);
                if (idleClip != null) anim.CrossFadeQueued(idleClip, 0.2f);
            }
        }

        private void Update()
        {
            if (pivot == null || health == null) return;
            bool dead = health.IsDead();

            if (dead != wasDead)
            {
                wasDead = dead;
                deathTime = 0f;
                if (dead && anim != null && deathClip != null) anim.CrossFade(deathClip, 0.1f);
                if (!dead && anim != null && idleClip != null) anim.Play(idleClip);
            }

            if (dead)
            {
                deathTime += Time.deltaTime;
                if (deathClip == null)
                {
                    // keel over sideways
                    float t = Mathf.Clamp01(deathTime / 0.45f);
                    pivot.localRotation = Quaternion.Euler(0, 0, Mathf.SmoothStep(0, 90f, t));
                    pivot.localPosition = new Vector3(0, -Mathf.Clamp01(deathTime - 2f) * 0.3f, 0);
                }
                return;
            }

            float speed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
            bool moving = speed > 0.15f;

            if (anim != null)
            {
                bool attacking = attackClip != null && anim.IsPlaying(attackClip);
                if (!attacking)
                {
                    string clip = moving ? walkClip : idleClip;
                    if (clip != null && !anim.IsPlaying(clip)) anim.CrossFade(clip, 0.2f);
                }
                if (walkClip != null) anim[walkClip].speed = Mathf.Clamp(speed / walkClipSpeed, 0.6f, 2.5f);
            }

            gait += Time.deltaTime * (moving ? 4f + speed * 3f : 1.5f);
            lunge = Mathf.Max(0f, lunge - Time.deltaTime * 3.5f);
            float lungeOffset = Mathf.Sin(lunge * Mathf.PI) * 0.35f;

            Vector3 position = new Vector3(0, 0, lungeOffset);
            Quaternion rotation = Quaternion.identity;
            if (motion == Motion.Hop)
            {
                position.y = moving ? Mathf.Abs(Mathf.Sin(gait)) * 0.12f : Mathf.Sin(gait) * 0.01f;
                rotation = Quaternion.Euler(moving ? Mathf.Sin(gait * 2f) * 4f : 0f, 0, 0);
            }
            else if (motion == Motion.Waddle)
            {
                position.y = moving ? Mathf.Abs(Mathf.Sin(gait)) * 0.06f : 0f;
                rotation = Quaternion.Euler(-lunge * 18f, 0, moving ? Mathf.Sin(gait) * 9f : Mathf.Sin(gait) * 2f);
            }
            pivot.localPosition = position;
            pivot.localRotation = rotation;
        }

        static GameObject Normalized(GameObject prefab, float targetHeight, out float height)
        {
            height = targetHeight;
            GameObject root = RpgUtil.FitModel(prefab, new Vector3(0, targetHeight, 0));
            root.name = "Visual";
            return root;
        }

        static GameObject BuildThistlehog(out float height)
        {
            GameObject root = new GameObject("Visual");
            Transform t = root.transform;
            Color body = RpgUtil.FromHex("#7A5A3A", Color.gray);
            Color belly = RpgUtil.FromHex("#C9A27A", Color.gray);
            Color spine = RpgUtil.FromHex("#5E8C31", Color.green);
            Color bloom = RpgUtil.FromHex("#A35BC2", Color.magenta);
            Color dark = RpgUtil.FromHex("#1E1612", Color.black);

            LowPoly.Part(LowPoly.Ico(1, 0.06f, 3), t, new Vector3(0, 0.42f, -0.05f), new Vector3(0.9f, 0.62f, 1.05f), body);
            LowPoly.Part(LowPoly.Ico(1, 0.04f, 4), t, new Vector3(0, 0.32f, 0.08f), new Vector3(0.72f, 0.42f, 0.8f), belly);

            // snout, nose, eyes, ears
            LowPoly.Spike(t, new Vector3(0, 0.36f, 0.38f), new Vector3(0, -0.1f, 1f), 0.38f, 0.17f, belly, 6);
            LowPoly.Part(LowPoly.Ico(0), t, new Vector3(0, 0.33f, 0.76f), Vector3.one * 0.09f, dark);
            LowPoly.Part(LowPoly.Ico(0), t, new Vector3(0.13f, 0.48f, 0.5f), Vector3.one * 0.075f, dark);
            LowPoly.Part(LowPoly.Ico(0), t, new Vector3(-0.13f, 0.48f, 0.5f), Vector3.one * 0.075f, dark);
            LowPoly.Spike(t, new Vector3(0.17f, 0.58f, 0.36f), new Vector3(0.4f, 1f, 0.1f), 0.12f, 0.06f, body, 4);
            LowPoly.Spike(t, new Vector3(-0.17f, 0.58f, 0.36f), new Vector3(-0.4f, 1f, 0.1f), 0.12f, 0.06f, body, 4);

            // feet
            foreach (Vector3 foot in new[] { new Vector3(0.24f, 0, 0.28f), new Vector3(-0.24f, 0, 0.28f), new Vector3(0.24f, 0, -0.3f), new Vector3(-0.24f, 0, -0.3f) })
            {
                LowPoly.Part(LowPoly.Prism(5), t, foot, new Vector3(0.12f, 0.16f, 0.12f), dark);
            }

            // thistle spines over the back, tipped with purple blooms
            int count = 34;
            for (int i = 0; i < count; i++)
            {
                float u = (i + 0.5f) / count;
                float y = Mathf.Lerp(0.15f, 1f, u);               // only the upper part of the body
                float radius = Mathf.Sqrt(1f - y * y);
                float theta = i * 2.39996f;                          // golden angle spiral
                Vector3 direction = new Vector3(Mathf.Cos(theta) * radius, y, Mathf.Sin(theta) * radius);
                if (direction.z > 0.55f) continue;                   // keep the face clear
                direction = Vector3.Scale(direction, new Vector3(1f, 1f, 1.2f)).normalized;
                direction = (direction + new Vector3(0, 0, -0.35f)).normalized;  // swept back
                Vector3 surface = new Vector3(0, 0.42f, -0.05f) + Vector3.Scale(direction, new Vector3(0.42f, 0.29f, 0.5f));
                float length = 0.24f + (i % 3) * 0.04f;
                LowPoly.Spike(t, surface, direction, length, 0.045f, spine, 4);
                if (i % 2 == 0)
                {
                    LowPoly.Part(LowPoly.Ico(0), t, surface + direction * length, Vector3.one * 0.075f, bloom);
                }
            }

            height = 0.85f;
            return root;
        }

        static void AddEmbers(Transform parent, float height, int count)
        {
            Color ember = RpgUtil.FromHex("#FF7A2A", Color.red);
            for (int i = 0; i < count; i++)
            {
                float a = i * 2.4f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 0.18f, height * (0.55f + 0.08f * (i % 3)), -0.3f + i * 0.12f);
                LowPoly.Part(LowPoly.Ico(0), parent, p, Vector3.one * 0.07f, ember, 2f);
            }
        }

        static GameObject BuildBoar(out float height)
        {
            GameObject root = new GameObject("Visual");
            Transform t = root.transform;
            Color hide = RpgUtil.FromHex("#5A3E2E", Color.gray);
            Color bristle = RpgUtil.FromHex("#2E2018", Color.black);
            Color snout = RpgUtil.FromHex("#C98A78", Color.gray);
            Color tusk = RpgUtil.FromHex("#F2EAD8", Color.white);
            Color dark = RpgUtil.FromHex("#140E0A", Color.black);

            LowPoly.Part(LowPoly.Ico(1, 0.08f, 21), t, new Vector3(0, 0.62f, -0.1f), new Vector3(0.85f, 0.78f, 1.45f), hide);
            LowPoly.Part(LowPoly.Ico(1, 0.06f, 22), t, new Vector3(0, 0.66f, 0.55f), new Vector3(0.62f, 0.6f, 0.6f), hide);
            LowPoly.Spike(t, new Vector3(0, 0.6f, 0.75f), new Vector3(0, -0.15f, 1f), 0.32f, 0.18f, hide, 6);
            LowPoly.Part(LowPoly.Prism(6), t, new Vector3(0, 0.56f, 1.06f), Quaternion.Euler(90, 0, 0), new Vector3(0.2f, 0.05f, 0.17f), snout);
            foreach (float side in new[] { 1f, -1f })
            {
                LowPoly.Spike(t, new Vector3(0.11f * side, 0.5f, 0.92f), new Vector3(0.35f * side, 0.8f, 0.4f), 0.22f, 0.04f, tusk, 4);
                LowPoly.Part(LowPoly.Ico(0), t, new Vector3(0.16f * side, 0.8f, 0.78f), Vector3.one * 0.07f, dark);
                LowPoly.Spike(t, new Vector3(0.2f * side, 0.92f, 0.5f), new Vector3(0.5f * side, 1f, -0.2f), 0.18f, 0.08f, hide, 4);
            }
            // bristly ridge along the spine
            for (int i = 0; i < 9; i++)
            {
                float z = 0.5f - i * 0.14f;
                LowPoly.Spike(t, new Vector3(0, 0.98f - Mathf.Abs(z - 0.1f) * 0.25f, z), new Vector3(0, 1f, -0.4f), 0.16f + (i % 2) * 0.05f, 0.05f, bristle, 4);
            }
            foreach (Vector3 foot in new[] { new Vector3(0.26f, 0, 0.35f), new Vector3(-0.26f, 0, 0.35f), new Vector3(0.26f, 0, -0.5f), new Vector3(-0.26f, 0, -0.5f) })
            {
                LowPoly.Part(LowPoly.Prism(5, 0.8f), t, foot, new Vector3(0.15f, 0.3f, 0.15f), bristle);
            }
            LowPoly.Limb(t, new Vector3(0, 0.7f, -0.8f), new Vector3(0, 0.5f, -0.98f), 0.03f, bristle, 4, 0.4f);
            height = 1.1f;
            return root;
        }

        static GameObject BuildGolem(bool magma, out float height)
        {
            GameObject root = new GameObject("Visual");
            Transform t = root.transform;
            Color stone = RpgUtil.FromHex(magma ? "#2E2624" : "#8A8578", Color.gray);
            Color stoneDark = RpgUtil.FromHex(magma ? "#1C1614" : "#6A665C", Color.gray);
            Color accent = RpgUtil.FromHex(magma ? "#FF6A1A" : "#6E9A3A", Color.green);
            Color eye = RpgUtil.FromHex(magma ? "#FFD24A" : "#9FE8FF", Color.white);
            float glow = magma ? 1.8f : 0f;

            // legs
            foreach (float side in new[] { 1f, -1f })
            {
                LowPoly.Part(LowPoly.Ico(0, 0.2f, side > 0 ? 31 : 32), t, new Vector3(0.32f * side, 0.3f, 0), new Vector3(0.42f, 0.6f, 0.45f), stoneDark);
            }
            // torso and head
            LowPoly.Part(LowPoly.Ico(1, 0.18f, 33), t, new Vector3(0, 1.15f, 0), new Vector3(1.25f, 1.05f, 0.85f), stone);
            LowPoly.Part(LowPoly.Ico(0, 0.2f, 34), t, new Vector3(0, 1.85f, 0.08f), new Vector3(0.55f, 0.45f, 0.5f), stone);
            LowPoly.Part(LowPoly.Ico(0), t, new Vector3(0.12f, 1.88f, 0.32f), new Vector3(0.1f, 0.07f, 0.05f), eye, 2f);
            LowPoly.Part(LowPoly.Ico(0), t, new Vector3(-0.12f, 1.88f, 0.32f), new Vector3(0.1f, 0.07f, 0.05f), eye, 2f);
            // arms: shoulder boulder, forearm, big fist
            foreach (float side in new[] { 1f, -1f })
            {
                LowPoly.Part(LowPoly.Ico(0, 0.2f, side > 0 ? 35 : 36), t, new Vector3(0.78f * side, 1.45f, 0), new Vector3(0.5f, 0.45f, 0.5f), stoneDark);
                LowPoly.Part(LowPoly.Ico(0, 0.2f, side > 0 ? 37 : 38), t, new Vector3(0.9f * side, 0.95f, 0.1f), new Vector3(0.36f, 0.6f, 0.36f), stone);
                LowPoly.Part(LowPoly.Ico(1, 0.15f, side > 0 ? 39 : 40), t, new Vector3(0.95f * side, 0.5f, 0.18f), new Vector3(0.5f, 0.45f, 0.5f), stoneDark);
            }
            // moss tufts, or glowing magma cracks
            for (int i = 0; i < 7; i++)
            {
                float a = i * 0.9f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 0.45f, 1.0f + (i % 3) * 0.22f, Mathf.Sin(a) * 0.3f + 0.12f);
                if (magma) LowPoly.Part(LowPoly.Prism(4), t, p, Quaternion.Euler(0, a * 57f, 30f + i * 20f), new Vector3(0.05f, 0.32f, 0.05f), accent, glow);
                else LowPoly.Part(LowPoly.Ico(0, 0.2f, 50 + i), t, p + new Vector3(0, 0.3f, 0), new Vector3(0.22f, 0.1f, 0.2f), accent);
            }
            height = 2.2f;
            return root;
        }

        static GameObject BuildImp(out float height)
        {
            GameObject root = new GameObject("Visual");
            Transform t = root.transform;
            Color skin = RpgUtil.FromHex("#8E2A22", Color.red);
            Color dark = RpgUtil.FromHex("#2A1210", Color.black);
            Color horn = RpgUtil.FromHex("#D8C8A8", Color.white);
            Color fire = RpgUtil.FromHex("#FF8A2A", Color.yellow);
            Color wing = RpgUtil.FromHex("#4A1A18", Color.black);

            foreach (float side in new[] { 1f, -1f })
            {
                LowPoly.Limb(t, new Vector3(0.14f * side, 0.45f, 0), new Vector3(0.18f * side, 0.02f, 0.05f), 0.06f, dark, 5, 0.7f);
            }
            LowPoly.Part(LowPoly.Ico(1, 0.08f, 61), t, new Vector3(0, 0.68f, 0), new Vector3(0.5f, 0.55f, 0.42f), skin);
            LowPoly.Part(LowPoly.Ico(1, 0.06f, 62), t, new Vector3(0, 1.12f, 0.05f), new Vector3(0.44f, 0.4f, 0.4f), skin);
            LowPoly.Part(LowPoly.Ico(0), t, new Vector3(0.09f, 1.15f, 0.22f), new Vector3(0.08f, 0.06f, 0.04f), fire, 2.2f);
            LowPoly.Part(LowPoly.Ico(0), t, new Vector3(-0.09f, 1.15f, 0.22f), new Vector3(0.08f, 0.06f, 0.04f), fire, 2.2f);
            LowPoly.Part(LowPoly.Prism(4), t, new Vector3(0, 1.02f, 0.22f), Quaternion.Euler(90, 0, 0), new Vector3(0.14f, 0.02f, 0.03f), dark);
            foreach (float side in new[] { 1f, -1f })
            {
                LowPoly.Spike(t, new Vector3(0.12f * side, 1.28f, 0.02f), new Vector3(0.5f * side, 1f, -0.3f), 0.24f, 0.05f, horn, 4);
                LowPoly.Limb(t, new Vector3(0.22f * side, 0.82f, 0.02f), new Vector3(0.38f * side, 0.55f, 0.15f), 0.045f, skin, 5, 0.7f);
                // bat wings: thin stretched prisms
                LowPoly.Part(LowPoly.Prism(3), t, new Vector3(0.32f * side, 0.95f, -0.2f), Quaternion.Euler(10f, 25f * side, 60f * side), new Vector3(0.45f, 0.03f, 0.5f), wing);
            }
            // flaming tail tip
            LowPoly.Limb(t, new Vector3(0, 0.55f, -0.2f), new Vector3(0, 0.35f, -0.55f), 0.03f, dark, 4, 0.5f);
            LowPoly.Spike(t, new Vector3(0, 0.35f, -0.55f), new Vector3(0, 1f, -0.3f), 0.2f, 0.07f, fire, 4).GetComponent<Renderer>().sharedMaterial = LowPoly.Mat(fire, 1.8f);
            height = 1.35f;
            return root;
        }

        static GameObject BuildGnarl(out float height)
        {
            GameObject root = new GameObject("Visual");
            Transform t = root.transform;
            Color bark = RpgUtil.FromHex("#5E4128", Color.gray);
            Color barkDark = RpgUtil.FromHex("#3E2A1A", Color.gray);
            Color cut = RpgUtil.FromHex("#C9A06A", Color.yellow);
            Color moss = RpgUtil.FromHex("#6E9A3A", Color.green);
            Color leaf = RpgUtil.FromHex("#4E7F2A", Color.green);
            Color eye = RpgUtil.FromHex("#D6FF5A", Color.yellow);

            // root legs
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                Vector3 out1 = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                LowPoly.Limb(t, new Vector3(0, 0.35f, 0) + out1 * 0.15f, out1 * 0.45f + new Vector3(0, 0.02f, 0), 0.09f, barkDark, 5, 0.5f);
            }

            // trunk body with a cut top
            LowPoly.Part(LowPoly.Prism(7, 0.85f), t, new Vector3(0, 0.25f, 0), new Vector3(0.78f, 1.0f, 0.78f), bark);
            LowPoly.Part(LowPoly.Prism(7), t, new Vector3(0, 1.24f, 0), new Vector3(0.64f, 0.04f, 0.64f), cut);
            LowPoly.Part(LowPoly.Prism(7), t, new Vector3(0, 1.26f, 0), new Vector3(0.36f, 0.03f, 0.36f), RpgUtil.FromHex("#A87C48", Color.yellow));
            LowPoly.Part(LowPoly.Ico(1, 0.15f, 7), t, new Vector3(-0.12f, 1.28f, -0.1f), new Vector3(0.34f, 0.18f, 0.3f), moss);

            // face
            LowPoly.Part(LowPoly.Ico(0), t, new Vector3(0.13f, 0.95f, 0.36f), new Vector3(0.11f, 0.08f, 0.06f), eye, 1.6f);
            LowPoly.Part(LowPoly.Ico(0), t, new Vector3(-0.13f, 0.95f, 0.36f), new Vector3(0.11f, 0.08f, 0.06f), eye, 1.6f);
            LowPoly.Part(LowPoly.Prism(4), t, new Vector3(0, 0.68f, 0.33f), Quaternion.Euler(90, 0, 0), new Vector3(0.26f, 0.05f, 0.06f), barkDark);
            LowPoly.Limb(t, new Vector3(0.2f, 1.06f, 0.3f), new Vector3(0.04f, 1.02f, 0.34f), 0.025f, barkDark, 4);
            LowPoly.Limb(t, new Vector3(-0.2f, 1.06f, 0.3f), new Vector3(-0.04f, 1.02f, 0.34f), 0.025f, barkDark, 4);

            // branch arms with leaf clumps
            foreach (float side in new[] { 1f, -1f })
            {
                Vector3 shoulder = new Vector3(0.3f * side, 0.9f, 0);
                Vector3 elbow = new Vector3(0.62f * side, 0.78f, 0.08f);
                Vector3 hand = new Vector3(0.72f * side, 0.42f, 0.18f);
                LowPoly.Limb(t, shoulder, elbow, 0.07f, bark, 5, 0.8f);
                LowPoly.Limb(t, elbow, hand, 0.055f, bark, 5, 0.6f);
                LowPoly.Part(LowPoly.Ico(1, 0.18f, side > 0 ? 11 : 12), t, elbow + new Vector3(0.05f * side, 0.14f, 0), new Vector3(0.36f, 0.3f, 0.36f), leaf);
                LowPoly.Limb(t, hand, hand + new Vector3(0.1f * side, -0.12f, 0.08f), 0.02f, barkDark, 4, 0.3f);
                LowPoly.Limb(t, hand, hand + new Vector3(-0.02f * side, -0.14f, 0.12f), 0.02f, barkDark, 4, 0.3f);
            }

            height = 1.35f;
            return root;
        }
    }
}
