using System;
using System.Collections.Generic;
using RPG.Content;
using RPG.Core;
using RPG.Items;
using RPG.Saving;
using UnityEngine;

namespace RPG.Control
{
    /// <summary>What a character looks like: palette indices chosen on the character creation screen.</summary>
    [Serializable]
    public class AppearanceData
    {
        public string Name = "Adventurer";
        public int Skin = 1;
        public int Hair = 1;
        public int HairStyle = 0;
        public int Shirt = 0;
        public int Trousers = 0;

        public AppearanceData Clone()
        {
            return (AppearanceData)MemberwiseClone();
        }
    }

    /// <summary>
    /// Builds the player's body in code on the animated skeleton (so it uses the normal walk/attack
    /// animations): skin, hair, a shirt, trousers and boots in the chosen colours. Worn equipment is
    /// built on top from what is actually equipped: helm / cowl, platebody / leather, platelegs / chaps,
    /// the wielded weapon or tool in the right hand (bow in the left), the shield on the left forearm and a
    /// quiver when arrows are equipped. The original soldier mesh is hidden. Rebuilt when equipment changes.
    /// </summary>
    public class CharacterAppearance : MonoBehaviour, ISaveable
    {
        public const int PlayerLayer = 10;

        public static readonly string[] SkinColors = { "#F2D3B3", "#E3B78F", "#C99066", "#A56C45", "#7C4B2D", "#523220" };
        public static readonly string[] HairColors = { "#241A14", "#5A3A22", "#9A5A2A", "#D8B060", "#ECE4D4", "#8E2C20", "#3A4A8A", "#707070" };
        public static readonly string[] ShirtColors = { "#3F5E8C", "#7A2E2E", "#3E6B3A", "#6B4E8A", "#A08050", "#2E2E34", "#CFC2A0", "#B86A2A" };
        public static readonly string[] TrouserColors = { "#4A3A2A", "#2E3440", "#5A5A48", "#34466A", "#6E5E4C", "#1E1E22" };
        public static readonly string[] HairStyles = { "Short", "Long", "Ponytail", "Bald" };

        public AppearanceData Data { get; private set; }
        public event Action onChanged;

        Inventory inventory;
        Transform bodyRoot;
        readonly List<GameObject> bodyParts = new List<GameObject>();
        readonly List<GameObject> gearParts = new List<GameObject>();
        string gearSignature;
        bool built;

        // pose measured from the skeleton when the body is first built
        Transform hips, spine1, spine2, spine3, neck, head;
        Transform[] shoulder = new Transform[2], elbow = new Transform[2], hand = new Transform[2];
        Transform[] upperLeg = new Transform[2], lowerLeg = new Transform[2], ankle = new Transform[2], toes = new Transform[2];
        Vector3 F, R, U;
        float k;
        Vector3 headCenter;
        bool hasRig;

        private void Awake()
        {
            Data = new AppearanceData();
            inventory = GetComponent<Inventory>();
        }

        private void Start()
        {
            if (inventory != null) inventory.onChanged += OnInventoryChanged;
            Rebuild();
        }

        private void OnDestroy()
        {
            if (inventory != null) inventory.onChanged -= OnInventoryChanged;
        }

        void OnInventoryChanged()
        {
            if (Signature() != gearSignature) BuildGear();
        }

        public void SetAppearance(AppearanceData data)
        {
            Data = data.Clone();
            Rebuild();
            if (onChanged != null) onChanged();
        }

        public void Rebuild()
        {
            if (!FindRig()) return;
            InBindPose(() => { BuildBody(); BuildGearNow(); });
        }

        // Parts are fitted to the skeleton's rest pose, whatever animation frame it is on right now.
        readonly List<Transform> rigBones = new List<Transform>();
        readonly List<Vector3> restPositions = new List<Vector3>();
        readonly List<Quaternion> restRotations = new List<Quaternion>();

        void InBindPose(Action build)
        {
            int count = rigBones.Count;
            Vector3[] positions = new Vector3[count];
            Quaternion[] rotations = new Quaternion[count];
            for (int i = 0; i < count; i++)
            {
                if (rigBones[i] == null) continue;
                positions[i] = rigBones[i].localPosition;
                rotations[i] = rigBones[i].localRotation;
                rigBones[i].localPosition = restPositions[i];
                rigBones[i].localRotation = restRotations[i];
            }
            Measure();
            try { build(); }
            finally
            {
                for (int i = 0; i < count; i++)
                {
                    if (rigBones[i] == null) continue;
                    rigBones[i].localPosition = positions[i];
                    rigBones[i].localRotation = rotations[i];
                }
            }
        }

        void Measure()
        {
            F = transform.forward; F.y = 0; F = F.sqrMagnitude > 0.01f ? F.normalized : Vector3.forward;
            U = Vector3.up;
            R = Vector3.Cross(U, F);
            k = Mathf.Max(0.2f, Vector3.Distance(hips.position, neck.position) / 0.5f);
            headCenter = head.position + U * 0.11f * k + F * 0.01f * k;
        }

        public static Color Pick(string[] palette, int index)
        {
            int i = ((index % palette.Length) + palette.Length) % palette.Length;
            return RpgUtil.FromHex(palette[i], Color.gray);
        }

        // ------------------------------------------------------------------ rig

        Transform Find(string name)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        bool FindRig()
        {
            if (built) return hasRig;
            built = true;
            hips = Find("Hips");
            spine1 = Find("Spine_01");
            spine2 = Find("Spine_02") ?? spine1;
            spine3 = Find("Spine_03") ?? spine2;
            neck = Find("Neck");
            head = Find("Head");
            string[] sides = { "_L", "_R" };
            for (int s = 0; s < 2; s++)
            {
                shoulder[s] = Find("Shoulder" + sides[s]);
                elbow[s] = Find("Elbow" + sides[s]);
                hand[s] = Find("Hand" + sides[s]);
                upperLeg[s] = Find("UpperLeg" + sides[s]);
                lowerLeg[s] = Find("LowerLeg" + sides[s]);
                ankle[s] = Find("Ankle" + sides[s]);
                toes[s] = Find("Toes" + sides[s]) ?? Find("Ball" + sides[s]);
            }
            hasRig = hips != null && spine1 != null && neck != null && head != null && shoulder[0] != null && shoulder[1] != null
                && elbow[0] != null && elbow[1] != null && hand[0] != null && hand[1] != null && upperLeg[0] != null && upperLeg[1] != null
                && lowerLeg[0] != null && lowerLeg[1] != null && ankle[0] != null && ankle[1] != null;
            if (!hasRig)
            {
                Debug.LogWarning("[RPG] CharacterAppearance: skeleton bones not found; keeping the original model");
                return false;
            }

            // hide the original soldier mesh and its props (sword, coin bag...)
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;

            // remember the rest pose of every bone (before any animation has run)
            Transform rigRoot = hips.parent != null ? hips.parent : hips;
            foreach (Transform t in rigRoot.GetComponentsInChildren<Transform>(true))
            {
                rigBones.Add(t);
                restPositions.Add(t.localPosition);
                restRotations.Add(t.localRotation);
            }
            bodyRoot = transform;
            return true;
        }

        // ------------------------------------------------------------------ part helpers

        GameObject Add(List<GameObject> list, Transform bone, Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale, Color color, float glow = 0f)
        {
            GameObject part = new GameObject(mesh.name);
            part.transform.position = position;
            part.transform.rotation = rotation;
            part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = LowPoly.Mat(color, glow);
            part.layer = PlayerLayer;
            part.transform.SetParent(bone, true);
            list.Add(part);
            return part;
        }

        // A prism from a to b.
        GameObject Limb(List<GameObject> list, Transform bone, Vector3 a, Vector3 b, float radius, Color color, float taper = 1f, int sides = 6, float glow = 0f)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 1e-6f) d = U * 0.001f;
            return Add(list, bone, LowPoly.Prism(sides, taper), a, Quaternion.FromToRotation(Vector3.up, d.normalized), new Vector3(radius * 2f, d.magnitude, radius * 2f), color, glow);
        }

        // A box whose bottom face is centred on 'from', extending 'length' along 'up', 'width' along 'across'.
        GameObject Box(List<GameObject> list, Transform bone, Vector3 from, Vector3 up, Vector3 across, float width, float length, float depth, Color color, float glow = 0f)
        {
            return Add(list, bone, LowPoly.Block(), from, Basis(up, across), new Vector3(width, length, depth), color, glow);
        }

        GameObject Ball(List<GameObject> list, Transform bone, Vector3 center, Vector3 size, Color color, Quaternion rotation, int detail = 1, float glow = 0f)
        {
            return Add(list, bone, LowPoly.Ico(detail), center, rotation, size, color, glow);
        }

        GameObject Cone(List<GameObject> list, Transform bone, Vector3 from, Vector3 direction, float length, float radius, Color color, int sides = 4, float glow = 0f)
        {
            return Add(list, bone, LowPoly.Cone(sides), from, Quaternion.FromToRotation(Vector3.up, direction.normalized), new Vector3(radius * 2f, length, radius * 2f), color, glow);
        }

        static Quaternion Basis(Vector3 up, Vector3 across)
        {
            up = up.normalized;
            across = (across - up * Vector3.Dot(across, up)).normalized;
            if (across.sqrMagnitude < 0.001f) across = Vector3.Cross(up, Vector3.forward).normalized;
            Vector3 forward = Vector3.Cross(across, up);
            return Quaternion.LookRotation(forward, up);
        }

        Quaternion Facing { get { return Quaternion.LookRotation(F, U); } }

        static void Clear(List<GameObject> list)
        {
            foreach (GameObject go in list) if (go != null) Destroy(go);
            list.Clear();
        }

        // ------------------------------------------------------------------ body

        void BuildBody()
        {
            Clear(bodyParts);
            List<GameObject> p = bodyParts;
            Color skin = Pick(SkinColors, Data.Skin);
            Color hair = Pick(HairColors, Data.Hair);
            Color shirt = Pick(ShirtColors, Data.Shirt);
            Color trousers = Pick(TrouserColors, Data.Trousers);
            Color boots = RpgUtil.FromHex("#3A2A1E", Color.black);
            Color leather = RpgUtil.FromHex("#4A3424", Color.black);
            Color dark = RpgUtil.FromHex("#1E1A1A", Color.black);

            Vector3 hip = hips.position;
            Vector3 top = neck.position - U * 0.03f * k;
            Vector3 waist = Vector3.Lerp(hip, top, 0.45f);

            // pelvis, belly and chest (shirt), a belt
            Box(p, hips, hip - U * 0.11f * k, U, R, 0.31f * k, 0.2f * k, 0.21f * k, trousers);
            Add(p, spine1, LowPoly.TaperBox(1.08f, 1.05f), hip + U * 0.04f * k, Facing, new Vector3(0.3f * k, Vector3.Dot(waist - hip, U) - 0.04f * k + 0.02f * k, 0.2f * k), shirt);
            Add(p, spine3, LowPoly.TaperBox(1.3f, 1.1f), waist, Facing, new Vector3(0.32f * k, Vector3.Dot(top - waist, U), 0.21f * k), shirt);
            Box(p, hips, hip + U * 0.04f * k, U, R, 0.33f * k, 0.05f * k, 0.225f * k, leather);
            Box(p, hips, hip + U * 0.045f * k + F * 0.11f * k, U, R, 0.06f * k, 0.04f * k, 0.02f * k, RpgUtil.FromHex("#C9A040", Color.yellow));

            // neck and head
            Limb(p, neck, neck.position - U * 0.05f * k, head.position + U * 0.03f * k, 0.05f * k, skin);
            Ball(p, head, headCenter, new Vector3(0.2f, 0.245f, 0.225f) * k, skin, Facing, 1);
            Vector3 face = headCenter + F * 0.1f * k;
            foreach (float side in new[] { -1f, 1f })
            {
                Box(p, head, face + U * 0.015f * k + R * side * 0.045f * k, U, R, 0.035f * k, 0.03f * k, 0.03f * k, dark);
                Box(p, head, face + U * 0.055f * k + R * side * 0.045f * k, U, R, 0.05f * k, 0.014f * k, 0.03f * k, hair);
            }
            Box(p, head, face - U * 0.025f * k + F * 0.012f * k, U, R, 0.03f * k, 0.04f * k, 0.03f * k, Color.Lerp(skin, dark, 0.15f));
            Box(p, head, face - U * 0.065f * k, U, R, 0.06f * k, 0.012f * k, 0.03f * k, Color.Lerp(skin, dark, 0.45f));

            // arms: shirt sleeves to the elbow, bare forearms and hands
            for (int s = 0; s < 2; s++)
            {
                Vector3 sh = shoulder[s].position, el = elbow[s].position, ha = hand[s].position;
                Ball(p, shoulder[s], sh, Vector3.one * 0.12f * k, shirt, Facing);
                Limb(p, shoulder[s], sh, el, 0.052f * k, shirt, 0.9f);
                Ball(p, elbow[s], el, Vector3.one * 0.085f * k, skin, Facing);
                Limb(p, elbow[s], el, ha, 0.042f * k, skin, 0.85f);
                Vector3 f = (ha - el).normalized;
                Ball(p, hand[s], ha + f * 0.04f * k, new Vector3(0.08f, 0.1f, 0.07f) * k, skin, Quaternion.LookRotation(F, f));
            }

            // legs: trousers and boots
            for (int s = 0; s < 2; s++)
            {
                Vector3 hipJoint = upperLeg[s].position, knee = lowerLeg[s].position, foot = ankle[s].position;
                Limb(p, upperLeg[s], hipJoint + U * 0.04f * k, knee, 0.072f * k, trousers, 0.8f);
                Ball(p, lowerLeg[s], knee, Vector3.one * 0.11f * k, trousers, Facing);
                Limb(p, lowerLeg[s], knee, foot, 0.058f * k, trousers, 0.85f);
                Vector3 toe = toes[s] != null ? toes[s].position : foot + F * 0.15f * k;
                Vector3 along = toe - foot; along.y = 0;
                if (along.sqrMagnitude < 0.0001f) along = F * 0.15f * k;
                float length = along.magnitude + 0.1f * k;
                Vector3 sole = new Vector3(foot.x, transform.position.y, foot.z) - along.normalized * 0.05f * k;
                Add(p, ankle[s], LowPoly.Block(), sole + along.normalized * length * 0.5f, Basis(U, Vector3.Cross(U, along.normalized)), new Vector3(0.1f * k, 0.11f * k, length), boots);
                Limb(p, ankle[s], foot - U * 0.06f * k, foot + U * 0.09f * k, 0.062f * k, boots);
            }

            BuildHair(hair);
        }

        readonly List<GameObject> hairParts = new List<GameObject>();

        void BuildHair(Color hair)
        {
            Clear(hairParts);
            string style = HairStyles[((Data.HairStyle % HairStyles.Length) + HairStyles.Length) % HairStyles.Length];
            if (style == "Bald") return;
            List<GameObject> p = hairParts;
            Ball(p, head, headCenter + U * 0.035f * k - F * 0.025f * k, new Vector3(0.215f, 0.215f, 0.235f) * k, hair, Facing, 1);
            Box(p, head, headCenter + U * 0.075f * k + F * 0.07f * k, U, R, 0.17f * k, 0.05f * k, 0.06f * k, hair);
            if (style == "Long")
            {
                Box(p, head, headCenter - U * 0.2f * k - F * 0.085f * k, U, R, 0.2f * k, 0.28f * k, 0.07f * k, hair);
                foreach (float side in new[] { -1f, 1f })
                {
                    Box(p, head, headCenter - U * 0.12f * k + R * side * 0.095f * k - F * 0.02f * k, U, R, 0.035f * k, 0.18f * k, 0.12f * k, hair);
                }
            }
            else if (style == "Ponytail")
            {
                Vector3 tie = headCenter - F * 0.115f * k + U * 0.02f * k;
                Ball(p, head, tie, Vector3.one * 0.06f * k, hair, Facing);
                Limb(p, head, tie, tie - F * 0.06f * k - U * 0.22f * k, 0.035f * k, hair, 0.5f);
            }
        }

        // ------------------------------------------------------------------ equipment

        string Signature()
        {
            if (inventory == null) return "";
            InventoryModel m = inventory.Model;
            return m.HeadId + "|" + m.BodyId + "|" + m.LegsId + "|" + m.WeaponId + "|" + m.ShieldId + "|" + (m.AmmoCount > 0 ? m.AmmoId : "");
        }

        static Color TierColor(ItemInfo info, out float glow)
        {
            glow = 0f;
            MaterialTier tier = info != null && !string.IsNullOrEmpty(info.Tier) ? GameContent.Tier(info.Tier) : null;
            if (tier != null)
            {
                if (!string.IsNullOrEmpty(tier.GlowHex)) glow = 0.35f;
                return RpgUtil.FromHex(tier.ColorHex, Color.gray);
            }
            return info != null ? RpgUtil.FromHex(info.ColorHex, Color.gray) : Color.gray;
        }

        void BuildGear()
        {
            if (!hasRig) return;
            InBindPose(BuildGearNow);
        }

        void BuildGearNow()
        {
            Clear(gearParts);
            gearSignature = Signature();
            if (inventory == null) return;
            InventoryModel m = inventory.Model;
            ItemInfo helm = GameContent.Item(m.HeadId);
            foreach (GameObject h in hairParts) if (h != null) h.SetActive(helm == null);
            BuildHelm(helm);
            BuildBodyArmour(GameContent.Item(m.BodyId));
            BuildLegArmour(GameContent.Item(m.LegsId));
            BuildWeapon(GameContent.Item(m.WeaponId));
            BuildShield(GameContent.Item(m.ShieldId));
            if (m.AmmoCount > 0) BuildQuiver();
        }

        void BuildHelm(ItemInfo helm)
        {
            if (helm == null) return;
            List<GameObject> p = gearParts;
            float glow;
            Color metal = TierColor(helm, out glow);
            Color dark = Color.Lerp(metal, Color.black, 0.6f);
            if (helm.Id.StartsWith("helm_"))
            {
                Ball(p, head, headCenter + U * 0.012f * k, new Vector3(0.245f, 0.27f, 0.26f) * k, metal, Facing, 1, glow);
                Box(p, head, headCenter + F * 0.123f * k + U * 0.005f * k, U, R, 0.15f * k, 0.022f * k, 0.03f * k, dark);
                Box(p, head, headCenter + F * 0.118f * k - U * 0.06f * k, U, R, 0.022f * k, 0.06f * k, 0.03f * k, dark);
                Box(p, head, headCenter + U * 0.1f * k - F * 0.13f * k, U, F, 0.035f * k, 0.07f * k, 0.26f * k, Color.Lerp(metal, Color.white, 0.25f), glow);
                Box(p, head, headCenter - U * 0.13f * k - F * 0.03f * k, U, R, 0.21f * k, 0.05f * k, 0.2f * k, metal, glow);
            }
            else
            {
                // leather cowl: a hood that leaves the face open
                Ball(p, head, headCenter + U * 0.02f * k - F * 0.04f * k, new Vector3(0.25f, 0.26f, 0.26f) * k, metal, Facing, 1);
                Box(p, head, headCenter - U * 0.17f * k - F * 0.02f * k, U, R, 0.24f * k, 0.09f * k, 0.22f * k, Color.Lerp(metal, Color.black, 0.15f));
            }
        }

        void BuildBodyArmour(ItemInfo body)
        {
            if (body == null) return;
            List<GameObject> p = gearParts;
            float glow;
            Color color = TierColor(body, out glow);
            bool plate = body.Id.StartsWith("platebody_");
            Color trim = plate ? Color.Lerp(color, Color.white, 0.25f) : Color.Lerp(color, Color.black, 0.35f);
            Vector3 hip = hips.position;
            Vector3 top = neck.position - U * 0.02f * k;
            Vector3 waist = Vector3.Lerp(hip, top, 0.45f);
            Add(p, spine1, LowPoly.TaperBox(1.08f, 1.05f), hip + U * 0.02f * k, Facing, new Vector3(0.335f * k, Vector3.Dot(waist - hip, U), 0.235f * k), color, glow);
            Add(p, spine3, LowPoly.TaperBox(1.3f, 1.12f), waist - U * 0.01f * k, Facing, new Vector3(0.355f * k, Vector3.Dot(top - waist, U) + 0.01f * k, 0.24f * k), color, glow);
            for (int s = 0; s < 2; s++)
            {
                Vector3 sh = shoulder[s].position, el = elbow[s].position;
                Limb(p, shoulder[s], sh, Vector3.Lerp(sh, el, 0.92f), 0.062f * k, color, 0.9f, 6, glow);
                if (plate)
                {
                    Vector3 outward = (sh - neck.position); outward.y = 0;
                    Ball(p, shoulder[s], sh + U * 0.025f * k + outward.normalized * 0.01f * k, new Vector3(0.17f, 0.11f, 0.17f) * k, trim, Facing, 1, glow);
                }
            }
            if (plate)
            {
                // chest ridge and a belt plate
                Box(p, spine3, waist + F * 0.125f * k, U, R, 0.04f * k, Vector3.Dot(top - waist, U) * 0.85f, 0.02f * k, trim, glow);
                Box(p, hips, hip + U * 0.0f * k, U, R, 0.35f * k, 0.06f * k, 0.25f * k, Color.Lerp(color, Color.black, 0.4f));
            }
            else
            {
                // leather: stitched seams and, for wolfhide, a fur collar
                Box(p, spine3, waist + F * 0.12f * k + R * 0.06f * k, U, R, 0.012f * k, Vector3.Dot(top - waist, U) * 0.8f, 0.02f * k, trim);
                Box(p, spine3, waist + F * 0.12f * k - R * 0.06f * k, U, R, 0.012f * k, Vector3.Dot(top - waist, U) * 0.8f, 0.02f * k, trim);
                if (body.Id.StartsWith("wolfhide"))
                {
                    Add(p, spine3, LowPoly.Prism(8, 0.8f), top - U * 0.04f * k, Facing, new Vector3(0.34f * k, 0.08f * k, 0.26f * k), RpgUtil.FromHex("#B8B8BC", Color.gray));
                }
            }
        }

        void BuildLegArmour(ItemInfo legs)
        {
            if (legs == null) return;
            List<GameObject> p = gearParts;
            float glow;
            Color color = TierColor(legs, out glow);
            bool plate = legs.Id.StartsWith("platelegs_");
            Box(p, hips, hips.position - U * 0.12f * k, U, R, 0.335f * k, 0.17f * k, 0.23f * k, color, glow);
            for (int s = 0; s < 2; s++)
            {
                Vector3 hipJoint = upperLeg[s].position, knee = lowerLeg[s].position, foot = ankle[s].position;
                Limb(p, upperLeg[s], hipJoint, knee, 0.083f * k, color, 0.8f, 6, glow);
                if (!plate) continue;
                Ball(p, lowerLeg[s], knee + F * 0.03f * k, new Vector3(0.12f, 0.12f, 0.1f) * k, Color.Lerp(color, Color.white, 0.25f), Facing, 1, glow);
                Limb(p, lowerLeg[s], knee, Vector3.Lerp(knee, foot, 0.82f), 0.068f * k, color, 0.85f, 6, glow);
            }
        }

        // weapon axes in the right hand: f = along the forearm, t = out of the fist (thumb side)
        void HandAxes(int side, out Vector3 center, out Vector3 f, out Vector3 t)
        {
            f = (hand[side].position - elbow[side].position).normalized;
            t = F - f * Vector3.Dot(F, f);
            t = t.sqrMagnitude > 0.001f ? t.normalized : U;
            center = hand[side].position + f * 0.05f * k;
        }

        const int Left = 0, RightHand = 1;

        void BuildWeapon(ItemInfo weapon)
        {
            if (weapon == null) return;
            List<GameObject> p = gearParts;
            float glow;
            Color metal = TierColor(weapon, out glow);
            Color wood = RpgUtil.FromHex("#6B4A2B", Color.gray);
            Color grip = RpgUtil.FromHex("#3A2618", Color.black);
            Color gold = RpgUtil.FromHex("#C9A040", Color.yellow);
            Vector3 c, f, t;
            string id = weapon.Id;

            if (id.StartsWith("shortbow"))
            {
                HandAxes(Left, out c, out f, out t);
                Transform bone = hand[Left];
                Color bowWood = RpgUtil.FromHex(weapon.ColorHex, Color.gray);
                const int segments = 6;
                Vector3 previous = Vector3.zero;
                for (int i = 0; i <= segments; i++)
                {
                    float s = -1f + 2f * i / segments;
                    Vector3 point = c + t * s * 0.55f * k - f * 0.13f * k * s * s;
                    if (i > 0) Limb(p, bone, previous, point, 0.018f * k * (1.2f - 0.5f * Mathf.Abs(s)), bowWood, 1f, 5);
                    previous = point;
                }
                Vector3 tipA = c + t * 0.55f * k - f * 0.13f * k, tipB = c - t * 0.55f * k - f * 0.13f * k;
                Limb(p, bone, tipA, tipB, 0.004f * k, RpgUtil.FromHex("#EDE6D6", Color.white), 1f, 3);
                Limb(p, bone, c - t * 0.07f * k, c + t * 0.07f * k, 0.026f * k, grip);
                return;
            }

            HandAxes(RightHand, out c, out f, out t);
            Transform right = hand[RightHand];

            if (id.StartsWith("staff"))
            {
                Color staffWood = RpgUtil.FromHex(weapon.ColorHex, Color.gray);
                Limb(p, right, c - t * 0.5f * k, c + t * 0.9f * k, 0.022f * k, Color.Lerp(staffWood, Color.black, 0.3f), 1f, 6);
                Vector3 gem = c + t * 0.98f * k;
                Ball(p, right, gem, Vector3.one * 0.09f * k, RpgUtil.FromHex("#FFB347", Color.yellow), Facing, 1, 1.6f);
                for (int i = 0; i < 3; i++)
                {
                    Vector3 around = Quaternion.AngleAxis(i * 120f, t) * f;
                    Limb(p, right, c + t * 0.88f * k, gem + around * 0.06f * k + t * 0.05f * k, 0.01f * k, staffWood, 0.6f, 4);
                }
                return;
            }

            if (weapon.Tool == ToolKind.Axe || weapon.Tool == ToolKind.Pickaxe)
            {
                Limb(p, right, c - t * 0.12f * k, c + t * 0.46f * k, 0.02f * k, wood, 1f, 6);
                Vector3 headPos = c + t * 0.4f * k;
                if (weapon.Tool == ToolKind.Axe)
                {
                    Box(p, right, headPos - f * 0.045f * k, f, t, 0.12f * k, 0.18f * k, 0.03f * k, metal, glow);
                    Box(p, right, headPos + f * 0.125f * k, f, t, 0.15f * k, 0.03f * k, 0.018f * k, Color.Lerp(metal, Color.white, 0.4f), glow);
                }
                else
                {
                    Box(p, right, headPos - t * 0.03f * k, t, f, 0.06f * k, 0.07f * k, 0.05f * k, metal, glow);
                    Cone(p, right, headPos + f * 0.02f * k, (f - t * 0.25f), 0.24f * k, 0.028f * k, metal, 4, glow);
                    Cone(p, right, headPos - f * 0.02f * k, (-f - t * 0.25f), 0.17f * k, 0.026f * k, metal, 4, glow);
                }
                return;
            }

            if (id == "alpha_fang")
            {
                Limb(p, right, c - t * 0.06f * k, c + t * 0.06f * k, 0.02f * k, grip);
                Cone(p, right, c + t * 0.06f * k, t + f * 0.12f, 0.32f * k, 0.035f * k, RpgUtil.FromHex("#EDEDE6", Color.white), 4);
                return;
            }
            if (id == "gullet_hook")
            {
                Limb(p, right, c - t * 0.08f * k, c + t * 0.32f * k, 0.022f * k, grip);
                Vector3 a = c + t * 0.32f * k, b = a + t * 0.18f * k + f * 0.08f * k, d = b + f * 0.14f * k - t * 0.02f * k;
                Limb(p, right, a, b, 0.03f * k, metal, 0.8f, 5);
                Limb(p, right, b, d, 0.024f * k, metal, 0.5f, 5);
                Cone(p, right, d, f - t * 0.6f, 0.1f * k, 0.022f * k, metal);
                return;
            }

            // swords (and anything else): grip, pommel, cross-guard, blade, point
            Limb(p, right, c - t * 0.07f * k, c + t * 0.07f * k, 0.019f * k, grip);
            Ball(p, right, c - t * 0.09f * k, Vector3.one * 0.04f * k, gold, Facing);
            Box(p, right, c + t * 0.07f * k, t, f, 0.18f * k, 0.028f * k, 0.04f * k, Color.Lerp(metal, gold, 0.35f), glow);
            Box(p, right, c + t * 0.095f * k, t, f, 0.055f * k, 0.6f * k, 0.014f * k, metal, glow);
            Box(p, right, c + t * 0.095f * k, t, f, 0.012f * k, 0.6f * k, 0.018f * k, Color.Lerp(metal, Color.white, 0.35f), glow);
            Add(p, right, LowPoly.Cone(4), c + t * 0.695f * k, Basis(t, f), new Vector3(0.055f * k, 0.09f * k, 0.014f * k), metal, glow);
        }

        void BuildShield(ItemInfo shield)
        {
            if (shield == null) return;
            List<GameObject> p = gearParts;
            Vector3 el = elbow[Left].position, ha = hand[Left].position;
            Vector3 f = (ha - el).normalized;
            Vector3 n = Vector3.Cross(f, F).normalized;
            if (Vector3.Dot(n, U) < 0) n = -n;
            Vector3 center = Vector3.Lerp(el, ha, 0.5f) + n * 0.06f * k;
            Transform bone = elbow[Left];
            float glow;
            Color face = TierColor(shield, out glow);
            if (shield.Id == "shield_wood")
            {
                Color rim = RpgUtil.FromHex("#4A3424", Color.black);
                Add(p, bone, LowPoly.Prism(10), center - n * 0.012f * k, Quaternion.FromToRotation(Vector3.up, n), new Vector3(0.5f, 0.024f, 0.5f) * k, rim);
                Add(p, bone, LowPoly.Prism(10), center, Quaternion.FromToRotation(Vector3.up, n), new Vector3(0.45f, 0.024f, 0.45f) * k, face);
                Box(p, bone, center + n * 0.02f * k - f * 0.2f * k, f, Vector3.Cross(n, f), 0.025f * k, 0.4f * k, 0.01f * k, rim);
                Ball(p, bone, center + n * 0.03f * k, Vector3.one * 0.08f * k, RpgUtil.FromHex("#8A8D91", Color.gray), Facing);
                return;
            }
            // kiteshield: an elongated hexagon along the forearm with a rim and an emblem
            Quaternion rotation = Quaternion.LookRotation(f, n);
            Color rimColor = Color.Lerp(face, Color.black, 0.45f);
            Add(p, bone, LowPoly.Prism(6), center - n * 0.014f * k, rotation, new Vector3(0.44f * k, 0.026f * k, 0.66f * k), rimColor, glow);
            Add(p, bone, LowPoly.Prism(6), center, rotation, new Vector3(0.39f * k, 0.026f * k, 0.6f * k), face, glow);
            Color emblem = Color.Lerp(face, Color.white, 0.45f);
            Vector3 across = Vector3.Cross(n, f);
            Box(p, bone, center + n * 0.026f * k - f * 0.22f * k, f, across, 0.035f * k, 0.44f * k, 0.01f * k, emblem, glow);
            Box(p, bone, center + n * 0.026f * k + f * 0.06f * k - across * 0.13f * k, across, f, 0.035f * k, 0.26f * k, 0.01f * k, emblem, glow);
        }

        void BuildQuiver()
        {
            List<GameObject> p = gearParts;
            Vector3 back = Vector3.Lerp(spine3.position, neck.position, 0.4f) - F * 0.14f * k;
            Vector3 bottom = back - U * 0.22f * k + R * 0.08f * k;
            Vector3 top = back + U * 0.2f * k - R * 0.06f * k;
            Limb(p, spine3, bottom, top, 0.05f * k, RpgUtil.FromHex("#6B4528", Color.gray), 1.1f, 6);
            Vector3 along = (top - bottom).normalized;
            for (int i = 0; i < 4; i++)
            {
                Vector3 offset = Quaternion.AngleAxis(i * 90f, along) * R * 0.022f * k;
                Limb(p, spine3, top + offset, top + offset + along * 0.12f * k, 0.006f * k, RpgUtil.FromHex("#C9A06A", Color.gray), 1f, 3);
                Box(p, spine3, top + offset + along * 0.07f * k, along, R, 0.03f * k, 0.06f * k, 0.004f * k, RpgUtil.FromHex("#EDE6D6", Color.white));
            }
        }

        // ------------------------------------------------------------------ saving

        public object CaptureState()
        {
            return Data.Clone();
        }

        public void RestoreState(object state)
        {
            AppearanceData data = state as AppearanceData;
            if (data == null) return;
            Data = data.Clone();
            if (hasRig) InBindPose(() => { BuildBody(); BuildGearNow(); });
            if (onChanged != null) onChanged();
        }
    }
}
