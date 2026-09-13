using System.Collections.Generic;
using System.IO;
using EmberDeck.Combat;
using EmberDeck.Content;
using EmberDeck.Content.Effects;
using EmberDeck.Content.Relics;
using EmberDeck.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EmberDeck.EditorTools
{
    /// <summary>
    /// Generates every content asset and the playable scene from code.
    ///
    /// Content is generated rather than committed as .asset files because ScriptableObject
    /// and scene YAML carry hand-assigned fileIDs and GUIDs: they are unreadable in review,
    /// merge badly, and a single wrong GUID produces a silent null at runtime. Regenerating
    /// from a script keeps the whole starting set in one diffable file — and makes a balance
    /// pass a code edit plus one menu click.
    ///
    /// Re-running it overwrites the generated assets. Anything authored by hand belongs in a
    /// different folder.
    /// </summary>
    public static class ContentGenerator
    {
        const string Root = "Assets/EmberDeck";
        const string ContentRoot = Root + "/Content";
        const string ScenePath = Root + "/Scenes/Combat.unity";

        [MenuItem("EmberDeck/Generate Content and Scene")]
        public static void Generate()
        {
            EnsureFolders();

            // ── Effects ──────────────────────────────────────────────────────────────
            var dmg3  = Damage("Damage_3", 3);
            var dmg4x2 = Damage("Damage_4x2", 4, hits: 2);
            var dmg5  = Damage("Damage_5", 5);
            var dmg6  = Damage("Damage_6", 6);
            var dmg8  = Damage("Damage_8", 8);
            var dmg22 = Damage("Damage_22", 22);

            var block5  = Block("Block_5", 5);
            var block6  = Block("Block_6", 6);
            var block12 = Block("Block_12", 12);

            var vulnerable2 = Status("Status_Vulnerable2", StatusType.Vulnerable, 2);
            var weak2       = Status("Status_Weak2", StatusType.Weak, 2);
            var poison4     = Status("Status_Poison4", StatusType.Poison, 4);
            var strength2   = Status("Status_Strength2Self", StatusType.Strength, 2, toSelf: true);

            var draw1 = Draw("Draw_1", 1);
            var draw2 = Draw("Draw_2", 2);
            var energy1 = CreateAsset<GainEnergyEffect>($"{ContentRoot}/Effects/Energy_1.asset", e => e.Amount = 1);
            var heal6 = CreateAsset<HealEffect>($"{ContentRoot}/Effects/Heal_6.asset", e => e.Amount = 6);

            var attackTint = new Color(0.86f, 0.42f, 0.28f);
            var skillTint  = new Color(0.38f, 0.60f, 0.82f);
            var powerTint  = new Color(0.72f, 0.52f, 0.85f);

            // ── Cards ────────────────────────────────────────────────────────────────
            var strike = Card("strike", "Strike", CardType.Attack, 1, TargetMode.SingleEnemy, attackTint,
                              CardRarity.Starter, dmg6);
            var guard = Card("guard", "Guard", CardType.Skill, 1, TargetMode.Self, skillTint,
                             CardRarity.Starter, block5);
            var emberLash = Card("ember_lash", "Ember Lash", CardType.Attack, 1, TargetMode.SingleEnemy, attackTint,
                                 CardRarity.Common, dmg5, vulnerable2);
            var cinderToss = Card("cinder_toss", "Cinder Toss", CardType.Attack, 0, TargetMode.SingleEnemy, attackTint,
                                  CardRarity.Common, dmg3, draw1);
            var twinFangs = Card("twin_fangs", "Twin Fangs", CardType.Attack, 1, TargetMode.SingleEnemy, attackTint,
                                 CardRarity.Common, dmg4x2);
            var sweepingFlame = Card("sweeping_flame", "Sweeping Flame", CardType.Attack, 2, TargetMode.AllEnemies,
                                     attackTint, CardRarity.Uncommon, dmg8);
            var bulwark = Card("bulwark", "Bulwark", CardType.Skill, 2, TargetMode.Self, skillTint,
                               CardRarity.Common, block12);
            var stoke = Card("stoke", "Stoke", CardType.Power, 1, TargetMode.Self, powerTint,
                             CardRarity.Uncommon, strength2);
            var focus = Card("focus", "Focus", CardType.Skill, 1, TargetMode.Self, skillTint,
                             CardRarity.Common, draw2);
            var secondWind = Card("second_wind", "Second Wind", CardType.Skill, 1, TargetMode.Self, skillTint,
                                  CardRarity.Common, block6, draw1);
            var venomDart = Card("venom_dart", "Venom Dart", CardType.Attack, 1, TargetMode.SingleEnemy, attackTint,
                                 CardRarity.Uncommon, dmg3, poison4);
            var weakeningCry = Card("weakening_cry", "Weakening Cry", CardType.Skill, 1, TargetMode.AllEnemies,
                                    skillTint, CardRarity.Uncommon, weak2);
            var ashenBrew = Card("ashen_brew", "Ashen Brew", CardType.Skill, 0, TargetMode.Self, skillTint,
                                 CardRarity.Uncommon, energy1, draw1);
            ashenBrew.Exhaust = true;
            var mend = Card("mend", "Mend", CardType.Skill, 1, TargetMode.Self, skillTint,
                            CardRarity.Common, heal6);
            mend.Exhaust = true;
            var lastEmber = Card("last_ember", "Last Ember", CardType.Attack, 3, TargetMode.SingleEnemy, attackTint,
                                 CardRarity.Rare, dmg22);
            lastEmber.Exhaust = true;

            foreach (var card in new[] { ashenBrew, mend, lastEmber })
                EditorUtility.SetDirty(card);

            // ── Enemy moves ──────────────────────────────────────────────────────────
            var bite    = Move("Move_Bite", "Bite", IntentKind.Attack, 1, Damage("Damage_Bite", 10));
            var skitter = Move("Move_Skitter", "Skitter", IntentKind.Block, 1, Block("Block_Skitter", 8));
            var maul    = Move("Move_Maul", "Maul", IntentKind.Attack, 3, Damage("Damage_Maul", 14));
            var howl    = Move("Move_Howl", "Howl", IntentKind.Buff, 1,
                               Status("Status_Howl", StatusType.Strength, 3, toSelf: true));
            var brace   = Move("Move_Brace", "Brace", IntentKind.Block, 1, Block("Block_Brace", 10));
            var spark   = Move("Move_Spark", "Spark", IntentKind.Attack, 1, Damage("Damage_Spark", 7));
            var ignite  = Move("Move_Ignite", "Ignite", IntentKind.Debuff, 1,
                               Damage("Damage_Ignite", 4), Status("Status_Ignite", StatusType.Weak, 1));

            // ── Enemies ──────────────────────────────────────────────────────────────
            var cinderRat = MakeEnemy("cinder_rat", "Cinder Rat", 20, 24, MovePattern.Sequence,
                                  new Color(0.70f, 0.33f, 0.26f), bite, bite, skitter);
            var ashHound = MakeEnemy("ash_hound", "Ash Hound", 30, 35, MovePattern.WeightedRandom,
                                 new Color(0.45f, 0.30f, 0.42f), maul, howl, brace);
            var emberling = MakeEnemy("emberling", "Emberling", 12, 15, MovePattern.Sequence,
                                  new Color(0.85f, 0.58f, 0.25f), spark, ignite);

            // ── Relic ────────────────────────────────────────────────────────────────
            var emberCore = CreateAsset<EmberCoreRelic>($"{ContentRoot}/Relics/Relic_EmberCore.asset", relic =>
            {
                relic.Id = "ember_core";
                relic.DisplayName = "Ember Core";
                relic.Description = "The first attack you play each turn deals 3 additional damage.";
                relic.BonusDamage = 3;
            });

            // ── Run config ───────────────────────────────────────────────────────────
            var config = CreateAsset<RunConfig>($"{ContentRoot}/RunConfig.asset", cfg =>
            {
                cfg.PlayerName = "Ember";
                cfg.MaxHp = 65;
                cfg.EnergyPerTurn = 3;
                cfg.CardsPerTurn = 5;

                cfg.StarterDeck = new List<RunConfig.DeckEntry>
                {
                    Entry(strike, 4),
                    Entry(guard, 4),
                    Entry(emberLash, 1),
                    Entry(twinFangs, 1),
                    Entry(focus, 1),
                    Entry(bulwark, 1),
                    Entry(sweepingFlame, 1),
                    Entry(stoke, 1)
                };

                cfg.Relics = new List<RelicData> { emberCore };

                // Three enemies so the AoE card has a reason to exist and single-target
                // damage has a reason not to be the only answer.
                cfg.Encounter = new List<EnemyData> { emberling, cinderRat, ashHound };
            });

            // Keep the unused cards in the project: they are the reward pool the next
            // milestone draws from, and generating them now proves the effect set covers them.
            _ = new[] { cinderToss, secondWind, venomDart, weakeningCry, ashenBrew, mend, lastEmber };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            CreateScene(config);

            Debug.Log("[EmberDeck] Content and scene generated. Open Assets/EmberDeck/Scenes/Combat.unity and press Play.");
        }

        // ── Scene ────────────────────────────────────────────────────────────────────

        static void CreateScene(RunConfig config)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Background;

            var combatGo = new GameObject("Combat");
            var view = combatGo.AddComponent<CombatView>();

            // _config is private and [SerializeField]; SerializedObject is the only supported
            // way to write it without widening the field's visibility for the editor's sake.
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_config").objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.SaveScene(scene, ScenePath);

            var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!buildScenes.Exists(entry => entry.path == ScenePath))
            {
                buildScenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        static RunConfig.DeckEntry Entry(CardData card, int count) => new() { Card = card, Count = count };

        static DealDamageEffect Damage(string file, int amount, int hits = 1) =>
            CreateAsset<DealDamageEffect>($"{ContentRoot}/Effects/{file}.asset", effect =>
            {
                effect.Amount = amount;
                effect.Hits = hits;
            });

        static GainBlockEffect Block(string file, int amount) =>
            CreateAsset<GainBlockEffect>($"{ContentRoot}/Effects/{file}.asset", effect => effect.Amount = amount);

        static ApplyStatusEffect Status(string file, StatusType status, int amount, bool toSelf = false) =>
            CreateAsset<ApplyStatusEffect>($"{ContentRoot}/Effects/{file}.asset", effect =>
            {
                effect.Status = status;
                effect.Amount = amount;
                effect.ApplyToSelf = toSelf;
            });

        static DrawCardsEffect Draw(string file, int amount) =>
            CreateAsset<DrawCardsEffect>($"{ContentRoot}/Effects/{file}.asset", effect => effect.Amount = amount);

        static CardData Card(string id, string name, CardType type, int cost, TargetMode target, Color tint,
                             CardRarity rarity, params CardEffect[] effects) =>
            CreateAsset<CardData>($"{ContentRoot}/Cards/Card_{id}.asset", card =>
            {
                card.Id = id;
                card.DisplayName = name;
                card.Type = type;
                card.Cost = cost;
                card.Target = target;
                card.TintColor = tint;
                card.Rarity = rarity;
                card.Exhaust = false;
                card.Effects = new List<CardEffect>(effects);
            });

        static EnemyMove Move(string file, string label, IntentKind kind, int weight, params CardEffect[] effects) =>
            CreateAsset<EnemyMove>($"{ContentRoot}/Enemies/{file}.asset", move =>
            {
                move.Label = label;
                move.Kind = kind;
                move.Weight = weight;
                move.Effects = new List<CardEffect>(effects);
            });

        static EnemyData MakeEnemy(string id, string name, int minHp, int maxHp, MovePattern pattern, Color tint,
                               params EnemyMove[] moves) =>
            CreateAsset<EnemyData>($"{ContentRoot}/Enemies/Enemy_{id}.asset", enemy =>
            {
                enemy.Id = id;
                enemy.DisplayName = name;
                enemy.MinHp = minHp;
                enemy.MaxHp = maxHp;
                enemy.Pattern = pattern;
                enemy.TintColor = tint;
                enemy.Moves = new List<EnemyMove>(moves);
            });

        /// <summary>
        /// Reuses the existing asset when there is one, so regenerating never breaks a
        /// reference held by something the generator does not own.
        /// </summary>
        static T CreateAsset<T>(string path, System.Action<T> configure) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            configure(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static void EnsureFolders()
        {
            foreach (var folder in new[]
                     {
                         Root, ContentRoot,
                         ContentRoot + "/Effects", ContentRoot + "/Cards",
                         ContentRoot + "/Enemies", ContentRoot + "/Relics",
                         Root + "/Scenes"
                     })
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                var parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }
            AssetDatabase.Refresh();
        }
    }
}
