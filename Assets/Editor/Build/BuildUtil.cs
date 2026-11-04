using System.IO;
using UnityEditor;
using UnityEngine;

namespace SSRP.EditorTools
{
    /// <summary>Paths and small helpers shared by the project builder.</summary>
    internal static class BuildUtil
    {
        // ---- folders
        public const string Art = "Assets/Art";
        public const string Sprites = "Assets/Art/Sprites";
        public const string Materials = "Assets/Art/Materials";
        public const string PhysicsFolder = "Assets/Art/Physics";
        public const string Sfx = "Assets/Audio/SFX";
        public const string SoCore = "Assets/ScriptableObjects/Core";
        public const string SoInput = "Assets/ScriptableObjects/Input";
        public const string SoGameplay = "Assets/ScriptableObjects/Gameplay";
        public const string SoTiles = "Assets/ScriptableObjects/Gameplay/Tiles";
        public const string SoTileBehaviours = "Assets/ScriptableObjects/Gameplay/Tiles/Behaviours";
        public const string SoAudio = "Assets/ScriptableObjects/Audio";
        public const string SoArena = "Assets/ScriptableObjects/Arena";
        public const string PrefabsCore = "Assets/Prefabs/Core";
        public const string PrefabsGameplay = "Assets/Prefabs/Gameplay";
        public const string PrefabsArena = "Assets/Prefabs/Arena";
        public const string PrefabsUi = "Assets/Prefabs/UI";
        public const string PrefabsAudio = "Assets/Prefabs/Audio";
        public const string Scenes = "Assets/Scenes";
        public const string ResourcesSsrp = "Assets/Resources/SSRP";

        public static readonly Color Background = new Color(0.03f, 0.04f, 0.09f, 1f);
        public static readonly Color Field = new Color(0.055f, 0.075f, 0.15f, 1f);
        public static readonly Color WallColour = new Color(0.35f, 0.43f, 0.63f, 1f);
        public static readonly Color LineColour = new Color(0.24f, 0.28f, 0.4f, 1f);

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>Load the asset at path, or create a new one there (GUIDs stay stable on re-runs).</summary>
        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset != null)
                return asset;

            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static void Save(Object asset)
        {
            EditorUtility.SetDirty(asset);
        }

        public static GameObject SavePrefab(GameObject root, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        public static T LoadPrefabComponent<T>(string path) where T : Component
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return go != null ? go.GetComponent<T>() : null;
        }

        // ---- SerializedObject setters (private [SerializeField] fields) -------------------------

        private static SerializedProperty Find(SerializedObject so, string property)
        {
            SerializedProperty p = so.FindProperty(property);

            if (p == null)
                Debug.LogError($"[SSRP Builder] Property '{property}' not found on {so.targetObject.GetType().Name}.");

            return p;
        }

        public static void Set(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, property);
            if (p == null) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(Object target, string property, float value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, property);
            if (p == null) return;
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetInt(Object target, string property, int value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, property);
            if (p == null) return;
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetBool(Object target, string property, bool value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, property);
            if (p == null) return;
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetString(Object target, string property, string value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, property);
            if (p == null) return;
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetEnum(Object target, string property, int value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, property);
            if (p == null) return;
            p.enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetColor(Object target, string property, Color value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, property);
            if (p == null) return;
            p.colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetVector2(Object target, string property, Vector2 value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, property);
            if (p == null) return;
            p.vector2Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetArray(Object target, string property, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, property);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- art ----------------------------------------------------------------------------------

        public static Sprite ImportSprite(string fileName, float pixelsPerUnit, FilterMode filter = FilterMode.Bilinear)
        {
            string path = $"{Sprites}/{fileName}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null)
            {
                Debug.LogError($"[SSRP Builder] Missing sprite {path}. Run: python tools/generate_art.py");
                return null;
            }

            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single ||
                           !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit) ||
                           importer.filterMode != filter ||
                           importer.mipmapEnabled;

            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = pixelsPerUnit;
                importer.filterMode = filter;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static Material CreateSpriteMaterial(string name, Texture texture = null)
        {
            string path = $"{Materials}/{name}.mat";
            EnsureFolder(Materials);

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            if (texture != null)
                material.mainTexture = texture;

            EditorUtility.SetDirty(material);
            return material;
        }

        public static void AssignLayer(GameObject go, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            go.layer = layer >= 0 ? layer : 0;
        }

        public static SpriteRenderer AddSprite(GameObject go, Sprite sprite, Material material, Color colour, int order)
        {
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = material;
            sr.color = colour;
            sr.sortingOrder = order;
            return sr;
        }

        public static GameObject Child(GameObject parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPosition;
            return go;
        }
    }
}
