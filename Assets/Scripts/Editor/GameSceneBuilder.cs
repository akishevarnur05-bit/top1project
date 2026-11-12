using System;
using System.IO;
using CyberDefender.Audio;
using CyberDefender.Combat;
using CyberDefender.Enemies;
using CyberDefender.InputProviders;
using CyberDefender.Player;
using CyberDefender.Tower;
using CyberDefender.UI;
using CyberDefender.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Object = UnityEngine.Object;

namespace CyberDefender.EditorTools
{
    /// <summary>
    /// Меню CyberDefender → Build Game Scene: создаёт данные вирусов, спрайты-заглушки
    /// и полностью собранную сцену Game.unity, добавляя её первой в Build Settings.
    /// </summary>
    public static class GameSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string SquarePath = "Assets/Art/Debug/Square.png";
        private const string CirclePath = "Assets/Art/Debug/Circle.png";
        private const string VirusFolder = "Assets/Data/Viruses";
        private const string CharacterFolder = "Assets/Data/Characters";
        private const string LevelFolder = "Assets/Data/Levels";
        private const int ActorLayer = 2; // Ignore Raycast: проверка земли не задевает игрока и врагов

        [MenuItem("CyberDefender/Build Game Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Sprite square = GetOrCreateSprite(SquarePath, 4, false);
            Sprite circle = GetOrCreateSprite(CirclePath, 64, true);

            VirusDefinition trojan = GetOrCreateVirus("TrojanHorse", v =>
            {
                v.displayName = "TROJAN-HORSE";
                v.color = new Color(1f, 0.55f, 0.1f);
                v.size = new Vector2(0.9f, 1.3f);
                v.maxHealth = 60f; v.moveSpeed = 3.2f;
                v.attackRange = 1.3f; v.attackDamage = 7f; v.windup = 0.45f; v.recovery = 0.45f; v.attackCooldown = 1.1f;
                v.knockback = 4f; v.hitStun = 0.3f; v.comboArmorHits = 3;
                v.special = VirusSpecial.TrojanDash; v.specialCooldown = 3.5f; v.specialPower = 11f;
            });
            VirusDefinition ransomware = GetOrCreateVirus("Ransomware", v =>
            {
                v.displayName = "RANSOMWARE";
                v.color = new Color(0.9f, 0.15f, 0.25f);
                v.size = new Vector2(1.3f, 1.6f);
                v.maxHealth = 110f; v.moveSpeed = 2f;
                v.attackRange = 1.5f; v.attackDamage = 13f; v.windup = 0.7f; v.recovery = 0.6f; v.attackCooldown = 1.6f;
                v.knockback = 6f; v.hitStun = 0.45f; v.comboArmorHits = 2;
                v.special = VirusSpecial.RansomEncrypt; v.specialCooldown = 0f; v.specialPower = 30f;
            });
            VirusDefinition rootkit = GetOrCreateVirus("RootkitUltimate", v =>
            {
                v.displayName = "ROOTKIT ULTIMATE";
                v.color = new Color(0.6f, 0.2f, 1f);
                v.size = new Vector2(1.2f, 1.9f);
                v.maxHealth = 220f; v.moveSpeed = 3.6f;
                v.attackRange = 1.4f; v.attackDamage = 11f; v.windup = 0.5f; v.recovery = 0.35f; v.attackCooldown = 0.9f;
                v.knockback = 5f; v.hitStun = 0.35f; v.comboArmorHits = 3;
                v.special = VirusSpecial.RootkitTeleport; v.specialCooldown = 4.5f; v.specialPower = 0f;
            });

            // Голоса для катсцен: троян пищит, вымогатель хрипит, руткит — низкий и «битый»
            EnsureVoice(trojan, 1.3f, 0.1f);
            EnsureVoice(ransomware, 0.75f, 0.4f);
            EnsureVoice(rootkit, 0.5f, 0.6f);

            CharacterDefinition aegis = GetOrCreateAsset<CharacterDefinition>(CharacterFolder, "Aegis", c =>
            {
                c.displayName = "AEGIS";
                c.role = "Антивирус · универсал";
                c.description = "Сбалансированный защитник ядра. Ровные характеристики — лучший выбор для первого штурма башни.";
                c.color = new Color(0.2f, 1f, 0.45f);
                c.visorColor = new Color(0.8f, 1f, 1f);
                c.size = new Vector2(0.9f, 1.4f);
                c.maxHealth = 100f; c.moveSpeed = 7f; c.jumpVelocity = 12f;
                c.lightDamage = 5f; c.lightDuration = 0.25f;
                c.heavyDamage = 12f; c.heavyDuration = 0.55f; c.heavyPush = 6f;
                c.blockDamageMultiplier = 0.2f; c.energyGainMultiplier = 1f;
                c.firewallDuration = 5f; c.purgeDamageFraction = 0.35f;
            });
            CharacterDefinition bastion = GetOrCreateAsset<CharacterDefinition>(CharacterFolder, "Bastion", c =>
            {
                c.displayName = "BASTION";
                c.role = "Файрвол · танк";
                c.description = "Медленный, но почти непробиваемый: блок срезает 95% урона, FIREWALL держится 7 секунд.";
                c.color = new Color(0.25f, 0.5f, 1f);
                c.visorColor = new Color(1f, 0.85f, 0.3f);
                c.size = new Vector2(1.1f, 1.6f);
                c.maxHealth = 150f; c.moveSpeed = 5f; c.jumpVelocity = 10.5f;
                c.lightDamage = 6f; c.lightDuration = 0.32f;
                c.heavyDamage = 16f; c.heavyDuration = 0.7f; c.heavyPush = 8f;
                c.blockDamageMultiplier = 0.05f; c.energyGainMultiplier = 0.9f;
                c.firewallDuration = 7f; c.purgeDamageFraction = 0.35f;
            });
            CharacterDefinition vector = GetOrCreateAsset<CharacterDefinition>(CharacterFolder, "Vector", c =>
            {
                c.displayName = "VECTOR";
                c.role = "Сканер · ассасин";
                c.description = "Быстрый и хрупкий. Короткие удары и ускоренный набор Kernel Energy — чаще всех использует голосовые приёмы.";
                c.color = new Color(1f, 0.45f, 0.75f);
                c.visorColor = new Color(0.2f, 1f, 0.9f);
                c.size = new Vector2(0.8f, 1.25f);
                c.maxHealth = 75f; c.moveSpeed = 9.5f; c.jumpVelocity = 13.5f;
                c.lightDamage = 4f; c.lightDuration = 0.18f;
                c.heavyDamage = 10f; c.heavyDuration = 0.45f; c.heavyPush = 5f;
                c.blockDamageMultiplier = 0.3f; c.energyGainMultiplier = 1.35f;
                c.firewallDuration = 4f; c.purgeDamageFraction = 0.4f;
            });

            // Парящие арены: нижняя строка — основной остров ('='), выше — сквозные платформы ('-') через 1.5 юнита.
            // По краям острова — пропасть: вылет за зону ринг-аута стоит вирусу жизни, игроку — бэкапа.
            LevelDefinition bootSector = GetOrCreateLevel("01_BootSector", BackdropTheme.BootSector,
                "....----......----....\n" +
                "...================...", l =>
            {
                l.backgroundColor = new Color(0.03f, 0.06f, 0.12f);
                l.platformColor = new Color(0.2f, 0.9f, 1f);
                l.groundColor = new Color(0.1f, 0.7f, 0.85f);
            });
            // Узкий остров и пирамида платформ — бой идёт по вертикали
            LevelDefinition memoryHeap = GetOrCreateLevel("02_MemoryHeap", BackdropTheme.MemoryHeap,
                "..........--..........\n" +
                ".......--....--.......\n" +
                "....---........---....\n" +
                ".....============.....", l =>
            {
                l.backgroundColor = new Color(0.03f, 0.08f, 0.07f);
                l.platformColor = new Color(0.3f, 1f, 0.6f);
                l.groundColor = new Color(0.15f, 0.75f, 0.45f);
            });
            // Два острова с ямой посередине, над ямой — мост-платформа
            LevelDefinition fileSystem = GetOrCreateLevel("03_FileSystem", BackdropTheme.FileSystem,
                "........------........\n" +
                "...=======..=======...", l =>
            {
                l.backgroundColor = new Color(0.1f, 0.05f, 0.03f);
                l.platformColor = new Color(1f, 0.65f, 0.2f);
                l.groundColor = new Color(0.8f, 0.45f, 0.15f);
            });
            LevelDefinition driverStack = GetOrCreateLevel("04_DriverStack", BackdropTheme.DriverStack,
                ".........----.........\n" +
                "....----......----....\n" +
                ".........----.........\n" +
                "......==========......", l =>
            {
                l.backgroundColor = new Color(0.1f, 0.03f, 0.07f);
                l.platformColor = new Color(1f, 0.3f, 0.55f);
                l.groundColor = new Color(0.75f, 0.2f, 0.4f);
            });
            // Арена босса: самая широкая, зона вылета дальше — Rootkit тяжело выбить
            LevelDefinition kernelCore = GetOrCreateLevel("05_KernelCore", BackdropTheme.KernelCore,
                "........------........\n" +
                "...----........----...\n" +
                "..==================..", l =>
            {
                l.blastMarginX = 8f;
                l.backgroundColor = new Color(0.07f, 0.02f, 0.12f);
                l.platformColor = new Color(0.75f, 0.35f, 1f);
                l.groundColor = new Color(0.5f, 0.2f, 0.8f);
            });

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Камера
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.05f, 0.1f);
            cameraGo.transform.position = new Vector3(0f, 1.5f, -10f);
            var cameraDirector = cameraGo.AddComponent<CameraDirector>();
            cameraGo.AddComponent<AudioListener>();

            // Арену (остров и платформы) строит LevelBuilder под каждый этаж — стен нет, с краёв можно упасть

            // Игрок
            // Корень без масштаба: размер тела задаёт коллайдер, визуал живёт в дочерних объектах
            var player = new GameObject("Player");
            player.transform.position = new Vector2(-4f, -1f);
            player.layer = ActorLayer;
            player.AddComponent<BoxCollider2D>().size = new Vector2(0.9f, 1.4f);
            var rb = player.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            player.AddComponent<Health>();
            var controller = player.AddComponent<PlayerController>();
            player.AddComponent<PlayerCombat>();
            var visuals = player.AddComponent<PlayerVisuals>();

            var body = CreateChild(player, "Body", square, Vector2.zero, new Vector2(0.9f, 1.4f), new Color(0.2f, 1f, 0.45f), 1);
            var visor = CreateChild(body, "Visor", square, new Vector2(0.25f, 0.28f), new Vector2(0.4f, 0.1f), new Color(0.8f, 1f, 1f), 2);
            var sprite = CreateChild(player, "Sprite", null, Vector2.zero, Vector2.one, Color.white, 1);
            sprite.GetComponent<SpriteRenderer>().enabled = false;
            var spriteAnimator = sprite.AddComponent<SpriteAnimator>();
            var shield = CreateChild(player, "FirewallShield", circle, Vector2.zero, new Vector2(2.2f, 2.4f), new Color(0.3f, 0.7f, 1f, 0.35f), 3);
            shield.GetComponent<SpriteRenderer>().enabled = false;

            Set(controller, "groundMask", p => p.intValue = 1 << 0);
            Set(controller, "groundCheckOffset", p => p.vector2Value = new Vector2(0f, -0.72f));
            Set(controller, "groundCheckRadius", p => p.floatValue = 0.12f);
            Set(visuals, "body", p => p.objectReferenceValue = body.GetComponent<SpriteRenderer>());
            Set(visuals, "visor", p => p.objectReferenceValue = visor.GetComponent<SpriteRenderer>());
            Set(visuals, "spriteAnimator", p => p.objectReferenceValue = spriteAnimator);
            Set(visuals, "shield", p => p.objectReferenceValue = shield.GetComponent<SpriteRenderer>());

            // Системы
            var systems = new GameObject("Systems");
            var simulated = systems.AddComponent<InputSimulatedProvider>();
            Set(simulated, "debugPlayer", p => p.objectReferenceValue = controller);
            systems.AddComponent<GestureInputProvider>();
            systems.AddComponent<VoiceInputProvider>();

            var levelBuilder = systems.AddComponent<LevelBuilder>();
            levelBuilder.EditorConfigure(camera, square);
            EditorUtility.SetDirty(levelBuilder);
            cameraDirector.EditorConfigure(player.transform, levelBuilder);
            EditorUtility.SetDirty(cameraDirector);

            // Катсцены: синтезатор «голоса», кинорамка с субтитрами, режиссёр сцены
            systems.AddComponent<AudioSource>();
            var beeper = systems.AddComponent<VoiceBeeper>();
            var overlay = new GameObject("Cutscene").AddComponent<CutsceneOverlay>();
            var cutscene = systems.AddComponent<IntroCutscene>();
            cutscene.EditorConfigure(cameraDirector, overlay, beeper);
            EditorUtility.SetDirty(cutscene);

            // Звук и музыка синтезируются кодом при запуске — аудиофайлы не нужны
            new GameObject("Audio").AddComponent<GameAudio>();

            var tower = systems.AddComponent<TowerManager>();
            tower.EditorConfigure(controller, levelBuilder, cutscene, square, new[]
            {
                new TowerManager.FloorConfig("Boot Sector", bootSector, trojan)
                {
                    introLines = new[]
                    {
                        "Пс-с... Антивирус? Здесь? В загрузочном секторе?",
                        "Я всего лишь безобидный setup.exe. Честно-честно.",
                        "...Ладно. Распаковываю полезную нагрузку!",
                    },
                },
                new TowerManager.FloorConfig("Memory Heap", memoryHeap, trojan, trojan)
                {
                    introLines = new[]
                    {
                        "Пока ты перезагружался, мы скопировались. Нас теперь двое.",
                        "Вся эта память — наша. Malloc, malloc, malloc!",
                    },
                },
                new TowerManager.FloorConfig("File System", fileSystem, ransomware)
                {
                    introLines = new[]
                    {
                        "Твои файлы? Были твои.",
                        "Все разделы зашифрованы. Ключ — полбиткоина.",
                        "Не хочешь платить? Тогда я зашифрую и тебя.",
                    },
                },
                new TowerManager.FloorConfig("Driver Stack", driverStack, ransomware, trojan)
                {
                    introLines = new[]
                    {
                        "Я подписался как драйвер видеокарты. Никто даже не проверил.",
                        "Уровень доступа — ring 0. Уровень терпения — ноль.",
                    },
                },
                new TowerManager.FloorConfig("KERNEL CORE", kernelCore, rootkit)
                {
                    introLines = new[]
                    {
                        "Ты дошёл до ядра. Впечатляет... для процесса без прав администратора.",
                        "Я здесь с самой первой загрузки. Я — каждый твой системный вызов.",
                        "Диспетчер задач меня не видит. Скоро он не увидит и тебя.",
                    },
                },
            });
            EditorUtility.SetDirty(tower);

            var hud = new GameObject("HUD").AddComponent<GameHud>();
            Set(hud, "player", p => p.objectReferenceValue = controller);

            var menu = new GameObject("Menu").AddComponent<MenuController>();
            menu.EditorConfigure(tower, controller, new[] { aegis, bastion, vector });
            EditorUtility.SetDirty(menu);

            // Для кликов мышью по карточкам меню
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            Debug.Log($"[CyberDefender] Сцена собрана: {ScenePath}. Нажмите Play.");
        }

        private static GameObject CreateBlock(string name, Sprite sprite, Vector2 position, Vector2 size, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return go;
        }

        private static GameObject CreateChild(GameObject parent, string name, Sprite sprite, Vector2 localPosition, Vector2 localScale, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(localScale.x, localScale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return go;
        }

        /// <summary>
        /// Уровни старого формата (без острова '=') получают новую схему; цвета, тайлы и фон, настроенные вручную, сохраняются.
        /// </summary>
        private static LevelDefinition GetOrCreateLevel(string fileName, BackdropTheme theme, string layout, Action<LevelDefinition> setup)
        {
            LevelDefinition level = GetOrCreateAsset<LevelDefinition>(LevelFolder, fileName, l =>
            {
                l.layout = layout;
                l.theme = theme;
                setup(l);
            });

            // Ассеты, созданные до появления неоновых фонов, получают мотив своего этажа
            if (level.theme == BackdropTheme.Unset)
            {
                level.theme = theme;
                EditorUtility.SetDirty(level);
                AssetDatabase.SaveAssets();
            }

            if (level.layout == null || level.layout.IndexOf('=') < 0)
            {
                level.layout = layout;
                EditorUtility.SetDirty(level);
                AssetDatabase.SaveAssets();
                Debug.Log($"[CyberDefender] {fileName}: схема обновлена под парящую арену.");
            }
            return level;
        }

        private static void Set(Object target, string property, Action<SerializedProperty> assign)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null)
            {
                Debug.LogError($"[CyberDefender] Поле '{property}' не найдено в {target.GetType().Name}");
                return;
            }
            assign(p);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Голос задаётся, только если в ассете остались значения по умолчанию — ручную настройку не трогаем.</summary>
        private static void EnsureVoice(VirusDefinition virus, float pitch, float grit)
        {
            if (!Mathf.Approximately(virus.voicePitch, 1f) || !Mathf.Approximately(virus.voiceGrit, 0.15f))
                return;
            virus.voicePitch = pitch;
            virus.voiceGrit = grit;
            EditorUtility.SetDirty(virus);
            AssetDatabase.SaveAssets();
        }

        private static VirusDefinition GetOrCreateVirus(string fileName, Action<VirusDefinition> setup) =>
            GetOrCreateAsset(VirusFolder, fileName, setup);

        private static T GetOrCreateAsset<T>(string folder, string fileName, Action<T> setup) where T : ScriptableObject
        {
            string path = $"{folder}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing; // не перетираем ручной баланс

            Directory.CreateDirectory(folder);
            var asset = ScriptableObject.CreateInstance<T>();
            setup(asset);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static Sprite GetOrCreateSprite(string path, int size, bool circle)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
                return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = 1f;
                    if (circle)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                        alpha = Mathf.Clamp01(r - d);
                    }
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pixels);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = size; // любой спрайт = 1x1 юнит
            importer.filterMode = circle ? FilterMode.Bilinear : FilterMode.Point;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
