using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravityShift.Editor
{
    public static class GravityShiftContentBinder
    {
        private const string ArtRoot = "Assets/GravityShift/Content/Art/";
        private const string VisualAssetPath = "Assets/GravityShift/Content/VisualAssets.asset";
        private const string AudioAssetPath = "Assets/GravityShift/Content/Audio/AudioAssets.asset";
        private const string ScenePath = "Assets/Scenes/GravityShift.unity";
        private const int ExpectedSpriteReferences = 126;
        private const int ExpectedAudioReferences = 18;

        public static void BuildAndBind()
        {
            ConfigureBackgroundImporters();
            ConfigurePanelImporter();

            VisualAssets visualAssets = LoadOrCreateAsset<VisualAssets>(VisualAssetPath);
            int spriteCount = BindVisualAssets(visualAssets);
            if (spriteCount != ExpectedSpriteReferences)
                throw new InvalidOperationException($"Bound {spriteCount} sprite references; expected {ExpectedSpriteReferences}.");

            ScriptableObject audioAssets = LoadOrCreateAudioAssets();
            int audioCount = BindAudioAssets(audioAssets);
            if (audioCount != ExpectedAudioReferences)
                throw new InvalidOperationException($"Bound {audioCount} audio references; expected {ExpectedAudioReferences}.");

            AssetDatabase.SaveAssets();
            BindScene(visualAssets, audioAssets);
            AssetDatabase.SaveAssets();
            Debug.Log($"Bound {spriteCount} sprites, {audioCount} audio clips, and saved {ScenePath}.");
        }

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static ScriptableObject LoadOrCreateAudioAssets()
        {
            Type type = typeof(GravityShiftGame).Assembly.GetType("GravityShift.AudioAssets", true);
            ScriptableObject asset = AssetDatabase.LoadAssetAtPath(AudioAssetPath, type) as ScriptableObject;
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(asset, AudioAssetPath);
            return asset;
        }

        private static int BindVisualAssets(VisualAssets asset)
        {
            var serialized = new SerializedObject(asset);
            int count = 0;

            count += SetSprites(serialized, "playerIdleFrames", Sequence("Characters/Player", "Idle", 4));
            count += SetSprites(serialized, "playerRunFrames", Sequence("Characters/Player", "Run", 6));
            count += SetSprite(serialized, "playerRise", SpritePath("Characters/Player/Rise_01.png"));
            count += SetSprite(serialized, "playerFall", SpritePath("Characters/Player/Fall_01.png"));
            count += SetSprites(serialized, "playerDashFrames", Sequence("Characters/Player", "Dash", 2));
            count += SetSprites(serialized, "playerAttackFrames", Sequence("Characters/Player", "Attack", 4));
            count += SetSprite(serialized, "playerVisor", SpritePath("Characters/Player/Visor.png"));

            count += SetSprites(serialized, "walkerIdleFrames", Sequence("Characters/Walker", "Idle", 2));
            count += SetSprites(serialized, "walkerWalkFrames", Sequence("Characters/Walker", "Walk", 6));
            count += SetSprites(serialized, "anchorIdleFrames", Sequence("Characters/Anchor", "Idle", 2));
            count += SetSprites(serialized, "anchorWalkFrames", Sequence("Characters/Anchor", "Walk", 6));
            count += SetSprites(serialized, "inverterIdleFrames", Sequence("Characters/Inverter", "Idle", 2));
            count += SetSprites(serialized, "inverterWalkFrames", Sequence("Characters/Inverter", "Walk", 6));

            count += SetSprite(serialized, "enemyPupil", SpritePath("Characters/Marks/Pupil.png"));
            count += SetSprite(serialized, "anchorRing", SpritePath("Characters/Marks/AnchorRing.png"));
            count += SetSprite(serialized, "stunStar", SpritePath("Characters/Marks/StunStar.png"));

            count += SetSprite(serialized, "researchWingBackground", SpritePath("Environment/Backgrounds/ResearchWing.png"));
            count += SetSprite(serialized, "magicTowerBackground", SpritePath("Environment/Backgrounds/MagicTower.png"));
            count += SetSprite(serialized, "orbitalStationBackground", SpritePath("Environment/Backgrounds/OrbitalStation.png"));
            count += SetTileSet(serialized, "researchWingTiles", "Environment/Tiles/Lab");
            count += SetTileSet(serialized, "magicTowerTiles", "Environment/Tiles/Tower");
            count += SetTileSet(serialized, "orbitalStationTiles", "Environment/Tiles/Station");

            count += SetSprite(serialized, "crate", SpritePath("Environment/Props/Crate.png"));
            count += SetSprite(serialized, "spikes", SpritePath("Environment/Hazards/Spikes.png"));
            count += SetSprite(serialized, "exitClosed", SpritePath("Environment/Doors/Exit_Closed.png"));
            count += SetSprite(serialized, "exitOpen", SpritePath("Environment/Doors/Exit_Open.png"));

            count += SetSprite(serialized, "gravityBeam", SpritePath("Effects/GravityBeam.png"));
            count += SetSprite(serialized, "gravityArrow", SpritePath("Effects/GravityArrow.png"));
            count += SetSprite(serialized, "inverterWarningRing", SpritePath("Effects/InverterWarningRing.png"));
            count += SetSprite(serialized, "telekinesisTether", SpritePath("Effects/TelekinesisTether.png"));
            count += SetSprites(serialized, "puffFrames", Sequence("Effects/Puff", "Puff", 4));

            count += SetSprite(serialized, "panel9Slice", SpritePath("UI/Panel9Slice.png"));
            count += SetSprite(serialized, "healthFull", SpritePath("UI/Health_Full.png"));
            count += SetSprite(serialized, "healthEmpty", SpritePath("UI/Health_Empty.png"));
            count += SetSprite(serialized, "fieldFull", SpritePath("UI/Field_Full.png"));
            count += SetSprite(serialized, "fieldEmpty", SpritePath("UI/Field_Empty.png"));
            SetReference(serialized, "hudFont", LoadFont("Assets/GravityShift/Content/Fonts/Galmuri11-Bold.ttf"));
            ValidatePlayerOverlay(serialized);
            RequireSpriteSize(serialized, "enemyPupil", 16, 16);
            RequireSpriteSize(serialized, "anchorRing", 16, 16);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return count;
        }

        private static int SetTileSet(SerializedObject serialized, string propertyName, string folder)
        {
            SerializedProperty tileSet = RequireProperty(serialized, propertyName);
            int count = SetSprites(tileSet.FindPropertyRelative("edgeMasks"), EdgeMaskSequence(folder));
            count += SetSprites(tileSet.FindPropertyRelative("innerCorners"), InnerCornerSequence(folder));
            return count;
        }

        private static int SetSprites(SerializedObject serialized, string propertyName, string[] paths) =>
            SetSprites(RequireProperty(serialized, propertyName), paths);

        private static int SetSprites(SerializedProperty array, string[] paths)
        {
            if (array == null || !array.isArray)
                throw new InvalidOperationException("A required sprite-array field is missing from VisualAssets.");

            array.arraySize = paths.Length;
            for (int i = 0; i < paths.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = LoadSprite(paths[i]);
            return paths.Length;
        }

        private static int SetSprite(SerializedObject serialized, string propertyName, string path)
        {
            SetReference(serialized, propertyName, LoadSprite(path));
            return 1;
        }

        private static void SetReference(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = RequireProperty(serialized, propertyName);
            property.objectReferenceValue = value;
        }

        private static SerializedProperty RequireProperty(SerializedObject serialized, string propertyName)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException($"Serialized field '{propertyName}' was not found on '{serialized.targetObject.name}'.");
            return property;
        }

        private static Sprite LoadSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                importer.filterMode != FilterMode.Point || importer.mipmapEnabled)
                throw new InvalidOperationException($"Sprite import settings are not Sprite/Single/Point/no-mipmaps: {path}");

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException($"Could not load imported Sprite: {path}");
            if (!Mathf.Approximately(sprite.pixelsPerUnit, 32f) ||
                sprite.pivot != new Vector2(sprite.rect.width * 0.5f, sprite.rect.height * 0.5f))
                throw new InvalidOperationException($"Sprite must use centered pivot and 32 PPU: {path}");
            return sprite;
        }

        private static void ValidatePlayerOverlay(SerializedObject serialized)
        {
            SerializedProperty frames = RequireProperty(serialized, "playerIdleFrames");
            Sprite body = frames.arraySize > 0 ? frames.GetArrayElementAtIndex(0).objectReferenceValue as Sprite : null;
            Sprite visor = RequireProperty(serialized, "playerVisor").objectReferenceValue as Sprite;
            if (body == null || visor == null || body.rect.size != visor.rect.size ||
                body.pivot != visor.pivot || !Mathf.Approximately(body.pixelsPerUnit, visor.pixelsPerUnit))
                throw new InvalidOperationException("Player body and visor must share canvas size, pivot, and PPU.");
        }

        private static void RequireSpriteSize(SerializedObject serialized, string propertyName, float width, float height)
        {
            Sprite sprite = RequireProperty(serialized, propertyName).objectReferenceValue as Sprite;
            if (sprite == null || sprite.rect.width != width || sprite.rect.height != height)
                throw new InvalidOperationException($"'{propertyName}' must be {width}x{height} pixels.");
        }

        private static Font LoadFont(string path)
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (font == null) throw new InvalidOperationException($"Could not load imported HUD font: {path}");
            return font;
        }

        private static int BindAudioAssets(ScriptableObject asset)
        {
            var serialized = new SerializedObject(asset);
            int count = 0;
            count += SetAudioClip(serialized, "researchWing", "Audio/Music/ResearchWing.ogg");
            count += SetAudioClip(serialized, "magicTower", "Audio/Music/MagicTower.ogg");
            count += SetAudioClip(serialized, "orbitalStation", "Audio/Music/OrbitalStation.ogg");
            count += SetAudioClip(serialized, "clear", "Audio/SFX/Clear.wav");
            count += SetAudioClip(serialized, "dash", "Audio/SFX/Dash.wav");
            count += SetAudioClip(serialized, "death", "Audio/SFX/Death.wav");
            count += SetAudioClip(serialized, "exitOpen", "Audio/SFX/ExitOpen.wav");
            count += SetAudioClip(serialized, "fieldPlace", "Audio/SFX/FieldPlace.wav");
            count += SetAudioClip(serialized, "fieldRemove", "Audio/SFX/FieldRemove.wav");
            count += SetAudioClip(serialized, "fieldReverseAll", "Audio/SFX/FieldReverseAll.wav");
            count += SetAudioClip(serialized, "grab", "Audio/SFX/Grab.wav");
            count += SetAudioClip(serialized, "gravityFlip", "Audio/SFX/GravityFlip.wav");
            count += SetAudioClip(serialized, "impact", "Audio/SFX/Impact.wav");
            count += SetAudioClip(serialized, "jump", "Audio/SFX/Jump.wav");
            count += SetAudioClip(serialized, "land", "Audio/SFX/Land.wav");
            count += SetAudioClip(serialized, "rotationWarning", "Audio/SFX/RotationWarning.wav");
            count += SetAudioClip(serialized, "swing", "Audio/SFX/Swing.wav");
            count += SetAudioClip(serialized, "throwClip", "Audio/SFX/Throw.wav");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return count;
        }

        private static int SetAudioClip(SerializedObject serialized, string propertyName, string relativePath)
        {
            string path = "Assets/GravityShift/Content/" + relativePath;
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new InvalidOperationException($"Could not load imported AudioClip: {path}");
            SetReference(serialized, propertyName, clip);
            return 1;
        }

        private static void BindScene(VisualAssets visuals, UnityEngine.Object audioAssets)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject camera = GameObject.Find("Main Camera");
            if (camera == null) throw new InvalidOperationException("Main Camera was not found in GravityShift.unity.");
            GravityShiftGame game = camera.GetComponent<GravityShiftGame>();
            if (game == null) throw new InvalidOperationException("GravityShiftGame was not found on Main Camera.");

            var serialized = new SerializedObject(game);
            SetReference(serialized, "visualAssets", visuals);
            SetReference(serialized, "audioAssets", audioAssets);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"Could not save scene: {ScenePath}");

            VerifySceneBindings();
        }

        public static void VerifySceneBindings()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject camera = GameObject.Find("Main Camera");
            GravityShiftGame game = camera == null ? null : camera.GetComponent<GravityShiftGame>();
            if (game == null) throw new InvalidOperationException("GravityShiftGame was not found on Main Camera.");

            var serialized = new SerializedObject(game);
            if (AssetDatabase.GetAssetPath(RequireProperty(serialized, "visualAssets").objectReferenceValue) != VisualAssetPath ||
                AssetDatabase.GetAssetPath(RequireProperty(serialized, "audioAssets").objectReferenceValue) != AudioAssetPath)
                throw new InvalidOperationException("Serialized scene references did not persist.");
            Debug.Log($"Verified visual and audio catalog references in {ScenePath}.");
        }

        private static void ConfigureBackgroundImporters()
        {
            ConfigureBackgroundImporter(SpritePath("Environment/Backgrounds/ResearchWing.png"));
            ConfigureBackgroundImporter(SpritePath("Environment/Backgrounds/MagicTower.png"));
            ConfigureBackgroundImporter(SpritePath("Environment/Backgrounds/OrbitalStation.png"));
        }

        private static void ConfigureBackgroundImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType != TextureImporterType.Sprite)
                throw new InvalidOperationException($"Background is not imported as a Sprite: {path}");

            bool changed = false;
            if (importer.maxTextureSize != 1024)
            {
                importer.maxTextureSize = 1024;
                changed = true;
            }
            changed |= ClampPlatformMaximum(importer, "Standalone");
            changed |= ClampPlatformMaximum(importer, "DefaultTexturePlatform");
            if (changed) importer.SaveAndReimport();
        }

        private static bool ClampPlatformMaximum(TextureImporter importer, string platformName)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platformName);
            if (!settings.overridden || settings.maxTextureSize == 1024) return false;
            settings.maxTextureSize = 1024;
            importer.SetPlatformTextureSettings(settings);
            return true;
        }

        private static void ConfigurePanelImporter()
        {
            string path = SpritePath("UI/Panel9Slice.png");
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType != TextureImporterType.Sprite)
                throw new InvalidOperationException($"Nine-slice panel is not imported as a Sprite: {path}");

            Vector4 border = new Vector4(9f, 9f, 9f, 9f);
            if (importer.spriteBorder != border)
            {
                importer.spriteBorder = border;
                importer.SaveAndReimport();
            }
        }

        private static string[] Sequence(string folder, string prefix, int count)
        {
            var paths = new string[count];
            for (int i = 0; i < count; i++)
                paths[i] = SpritePath($"{folder}/{prefix}_{i + 1:00}.png");
            return paths;
        }

        private static string[] EdgeMaskSequence(string folder)
        {
            var paths = new string[16];
            for (int mask = 0; mask < paths.Length; mask++)
            {
                string suffix = string.Empty;
                if ((mask & 1) != 0) suffix += "N";
                if ((mask & 2) != 0) suffix += "E";
                if ((mask & 4) != 0) suffix += "S";
                if ((mask & 8) != 0) suffix += "W";
                string name = mask == 0 ? "Edge_00_Core.png" : $"Edge_{mask:00}_{suffix}.png";
                paths[mask] = SpritePath($"{folder}/{name}");
            }
            return paths;
        }

        private static string[] InnerCornerSequence(string folder) => new[]
        {
            SpritePath($"{folder}/Inner_NE.png"),
            SpritePath($"{folder}/Inner_SE.png"),
            SpritePath($"{folder}/Inner_SW.png"),
            SpritePath($"{folder}/Inner_NW.png")
        };

        private static string SpritePath(string relativePath) => ArtRoot + relativePath;
    }
}
