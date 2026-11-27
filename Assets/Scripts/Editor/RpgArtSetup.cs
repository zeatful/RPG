using System.IO;
using System.Text;
using RPG.Core;
using UnityEditor;
using UnityEngine;

namespace RPG.EditorTools
{
    /// <summary>
    /// Configures imported creature models (legacy animation, looping walk/idle clips) and writes
    /// Assets/Resources/RpgArt.asset pointing at the tree, rock and creature models the game uses.
    /// </summary>
    public static class RpgArtSetup
    {
        const string ArtPath = "Assets/Resources/RpgArt.asset";
        static readonly string[] CreatureModels = { "Assets/Art/Quaternius/Wolf.fbx", "Assets/Art/Quaternius/Slime.fbx" };

        [MenuItem("RPG/Setup Art")]
        public static void Run()
        {
            foreach (string path in CreatureModels) ConfigureCreature(path);

            Directory.CreateDirectory("Assets/Resources");
            RpgArt art = AssetDatabase.LoadAssetAtPath<RpgArt>(ArtPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<RpgArt>();
                AssetDatabase.CreateAsset(art, ArtPath);
            }

            art.FirTreeA = Prefab("LowPoly - FirTree A");
            art.FirTreeB = Prefab("LowPoly - FirTree B");
            art.RoundTree = Prefab("SM_Env_Tree_03");
            art.TallTree = Prefab("SM_Env_Tree_010");
            art.RockA = Prefab("LowPoly - Rock A");
            art.RockB = Prefab("LowPoly - Rock B");
            art.RockC = Prefab("SM_Env_Rock_015");
            art.Wolf = AssetDatabase.LoadAssetAtPath<GameObject>(CreatureModels[0]);
            art.Slime = AssetDatabase.LoadAssetAtPath<GameObject>(CreatureModels[1]);
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();

            Report(art);
        }

        public static void RunBatch()
        {
            Run();
            EditorApplication.Exit(0);
        }

        static GameObject Prefab(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            Debug.LogWarning("[RPG Art] prefab not found: " + name);
            return null;
        }

        static void ConfigureCreature(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning("[RPG Art] missing model " + path);
                return;
            }

            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Legacy)
            {
                importer.animationType = ModelImporterAnimationType.Legacy;
                importer.importAnimation = true;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();

            // Loop the locomotion/idle clips; attacks and death play once.
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            bool clipsChanged = false;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                string lower = clip.name.ToLower();
                bool loop = lower.Contains("walk") || lower.Contains("run") || lower.Contains("idle") || lower.Contains("fly") || lower.Contains("swim") || lower.Contains("gallop");
                WrapMode wrap = loop ? WrapMode.Loop : WrapMode.Once;
                if (clip.loopTime != loop || clip.wrapMode != wrap)
                {
                    clip.loopTime = loop;
                    clip.wrapMode = wrap;
                    clipsChanged = true;
                }
            }
            if (clipsChanged)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        static void Report(RpgArt art)
        {
            StringBuilder text = new StringBuilder("[RPG Art] setup done\n");
            foreach (GameObject model in new[] { art.FirTreeA, art.FirTreeB, art.RoundTree, art.TallTree, art.RockA, art.RockB, art.RockC, art.Wolf, art.Slime })
            {
                if (model == null) continue;
                Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
                bool first = true;
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    Bounds b = renderer is SkinnedMeshRenderer ? ((SkinnedMeshRenderer)renderer).sharedMesh.bounds : renderer.GetComponent<MeshFilter>() != null && renderer.GetComponent<MeshFilter>().sharedMesh != null ? renderer.GetComponent<MeshFilter>().sharedMesh.bounds : renderer.bounds;
                    Vector3 lossy = renderer.transform.lossyScale;
                    b = new Bounds(renderer.transform.position + Vector3.Scale(b.center, lossy), Vector3.Scale(b.size, lossy));
                    if (first) { bounds = b; first = false; } else bounds.Encapsulate(b);
                }
                text.Append("  ").Append(model.name).Append(": size ").Append(bounds.size).Append(" center ").Append(bounds.center).Append(" root scale ").Append(model.transform.localScale);
                text.Append(" renderers ").Append(model.GetComponentsInChildren<Renderer>(true).Length);
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material != null) text.Append(" [mat ").Append(material.name).Append(" ").Append(material.HasProperty("_Color") ? material.color.ToString() : "-").Append("]");
                    }
                }
                Animation animation = model.GetComponent<Animation>();
                if (animation != null)
                {
                    text.Append(" clips:");
                    foreach (AnimationState state in animation) text.Append(" ").Append(state.name).Append("(").Append(state.length.ToString("0.00")).Append("s)");
                }
                string path = AssetDatabase.GetAssetPath(model);
                foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    AnimationClip clip = sub as AnimationClip;
                    if (clip != null && !clip.name.StartsWith("__preview")) text.Append(" {clip ").Append(clip.name).Append(" ").Append(clip.length.ToString("0.00")).Append("s legacy=").Append(clip.legacy).Append("}");
                }
                text.Append("\n");
            }
            Debug.Log(text.ToString());
        }
    }
}
