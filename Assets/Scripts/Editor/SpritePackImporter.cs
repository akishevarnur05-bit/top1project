using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CyberDefender.Visuals;
using UnityEditor;
using UnityEngine;

namespace CyberDefender.EditorTools
{
    /// <summary>
    /// Меню CyberDefender → Import Sprite Pack: для каждой папки с PNG-лентами (одна анимация — один файл,
    /// кадры квадратные и идут в ряд, как в паках CraftPix) режет кадры и собирает SpriteAnimationSet.
    /// Назначение клипов определяется по именам файлов: idle, run, punch, attack1, hurt, death…
    /// </summary>
    public static class SpritePackImporter
    {
        private static readonly string[] IdleKeys = { "idle", "stand" };
        private static readonly string[] MoveKeys = { "run", "walk", "move" };
        private static readonly string[] JumpKeys = { "jump" };
        private static readonly string[] HurtKeys = { "hurt", "hit", "damage" };
        private static readonly string[] DeathKeys = { "death", "die", "dead" };
        private static readonly string[] BlockKeys = { "block", "defend", "guard", "shield" };
        private static readonly string[] LightKeys = { "punch", "attack1", "attack_1", "attack 1", "attack" };
        private static readonly string[] HeavyKeys = { "attack2", "attack_2", "attack 2", "kick" };
        private static readonly string[] SuperKeys = { "special", "super", "attack3", "attack_3", "attack 3" };

        [MenuItem("CyberDefender/Import Sprite Pack...")]
        public static void ImportMenu()
        {
            string absolute = EditorUtility.OpenFolderPanel("Папка пака (внутри Assets)", Application.dataPath, "");
            if (string.IsNullOrEmpty(absolute))
                return;

            string root = ToAssetPath(absolute);
            if (root == null)
            {
                EditorUtility.DisplayDialog("Import Sprite Pack",
                    "Папка должна лежать внутри Assets проекта.\nСкопируйте распакованный пак, например, в Assets/Art/Characters.", "OK");
                return;
            }

            var report = new StringBuilder();
            int created = 0;

            IEnumerable<IGrouping<string, string>> folders = Directory
                .GetFiles(absolute, "*.png", SearchOption.AllDirectories)
                .Select(ToAssetPath)
                .Where(p => p != null)
                .GroupBy(p => Path.GetDirectoryName(p).Replace('\\', '/'));

            foreach (IGrouping<string, string> folder in folders)
            {
                SpriteAnimationSet set = BuildSet(folder.Key, folder.ToArray(), report);
                if (set != null)
                    created++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[SpritePackImporter] {root}\n{report}");
            EditorUtility.DisplayDialog("Import Sprite Pack",
                created > 0
                    ? $"Создано наборов анимаций: {created}.\n\nОткройте ассет героя (Assets/Data/Characters) или вируса (Assets/Data/Viruses) и перетащите набор в поле Animations.\n\nПодробности — в Console."
                    : "Не найдено ни одной PNG-ленты с квадратными кадрами. Подробности — в Console.",
                "OK");
        }

        private static SpriteAnimationSet BuildSet(string folder, string[] pngPaths, StringBuilder report)
        {
            var clips = new Dictionary<string, SpriteClip>();
            foreach (string path in pngPaths)
            {
                Sprite[] frames = SliceStrip(path, report);
                if (frames == null)
                    continue;
                string key = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                clips[key] = new SpriteClip { name = Path.GetFileNameWithoutExtension(path), frames = frames, fps = 12f };
            }

            if (clips.Count == 0)
                return null;

            string setName = Path.GetFileName(folder);
            string setPath = $"{folder}/{setName}_Animations.asset";
            var set = AssetDatabase.LoadAssetAtPath<SpriteAnimationSet>(setPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<SpriteAnimationSet>();
                AssetDatabase.CreateAsset(set, setPath);
            }

            var used = new HashSet<string>();
            set.idle = Take(clips, used, IdleKeys, Array.Empty<string>(), loop: true, fps: 8f);
            set.move = Take(clips, used, MoveKeys, new[] { "attack" }, loop: true, fps: 12f);
            set.jump = Take(clips, used, JumpKeys, new[] { "double" }, loop: false, fps: 10f);
            set.hurt = Take(clips, used, HurtKeys, Array.Empty<string>(), loop: false, fps: 12f);
            set.death = Take(clips, used, DeathKeys, Array.Empty<string>(), loop: false, fps: 10f);
            set.block = Take(clips, used, BlockKeys, Array.Empty<string>(), loop: false, fps: 12f);
            set.super = Take(clips, used, SuperKeys, new[] { "run" }, loop: false, fps: 12f);
            set.heavyAttack = Take(clips, used, HeavyKeys, new[] { "run" }, loop: false, fps: 12f);
            set.lightAttack = Take(clips, used, LightKeys, new[] { "run" }, loop: false, fps: 12f);
            EditorUtility.SetDirty(set);

            report.AppendLine($"■ {setPath}");
            report.AppendLine($"   idle={Describe(set.idle)}  move={Describe(set.move)}  jump={Describe(set.jump)}");
            report.AppendLine($"   light={Describe(set.lightAttack)}  heavy={Describe(set.heavyAttack)}  super={Describe(set.super)}");
            report.AppendLine($"   block={Describe(set.block)}  hurt={Describe(set.hurt)}  death={Describe(set.death)}");
            if (!set.IsUsable)
                report.AppendLine("   ⚠ нет анимации idle — набор не будет использоваться, назначьте клип вручную");
            return set;
        }

        /// <summary>Берёт первый ещё не занятый клип, имя которого содержит ключ (по порядку приоритета).</summary>
        private static SpriteClip Take(Dictionary<string, SpriteClip> clips, HashSet<string> used, string[] keys, string[] exclude, bool loop, float fps)
        {
            foreach (string key in keys)
            {
                foreach (KeyValuePair<string, SpriteClip> pair in clips.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    if (used.Contains(pair.Key) || !pair.Key.Contains(key) || exclude.Any(pair.Key.Contains))
                        continue;

                    used.Add(pair.Key);
                    pair.Value.loop = loop;
                    pair.Value.fps = fps;
                    return pair.Value;
                }
            }
            return new SpriteClip { loop = loop, fps = fps };
        }

        private static Sprite[] SliceStrip(string path, StringBuilder report)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return null;

            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (height <= 0 || width % height != 0)
            {
                report.AppendLine($"   пропущен {Path.GetFileName(path)}: {width}x{height} — не лента квадратных кадров");
                return null;
            }

            int count = width / height;
            string baseName = Path.GetFileNameWithoutExtension(path);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 8192;
            importer.spritePixelsPerUnit = height / 2f; // кадр = 2 юнита в высоту; тонкая подстройка — SpriteAnimationSet.scale

            var metas = new SpriteMetaData[count];
            for (int i = 0; i < count; i++)
            {
                metas[i] = new SpriteMetaData
                {
                    name = $"{baseName}_{i:00}",
                    rect = new Rect(i * height, 0, height, height),
                    alignment = (int)SpriteAlignment.BottomCenter,
                    pivot = new Vector2(0.5f, 0f),
                };
            }
#pragma warning disable CS0618 // spritesheet устарел, но работает без пакета com.unity.2d.sprite
            importer.spritesheet = metas;
#pragma warning restore CS0618
            importer.SaveAndReimport();

            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(s => s.name, StringComparer.Ordinal)
                .ToArray();

            if (sprites.Length != count)
                report.AppendLine($"   ⚠ {Path.GetFileName(path)}: ожидалось {count} кадров, нарезано {sprites.Length}. " +
                                  "Если так у всех файлов — сообщите, переведу импорт на пакет 2D Sprite.");
            return sprites;
        }

        private static string Describe(SpriteClip clip) => clip.IsValid ? $"{clip.name}({clip.frames.Length})" : "—";

        private static string ToAssetPath(string absolutePath)
        {
            string full = Path.GetFullPath(absolutePath).Replace('\\', '/');
            string data = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            if (!full.StartsWith(data, StringComparison.OrdinalIgnoreCase))
                return null;
            return "Assets" + full.Substring(data.Length);
        }
    }
}
