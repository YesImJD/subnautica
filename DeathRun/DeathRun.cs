﻿/**
 * DeathRun mod - Cattlesquat "but standing on the shoulders of giants"
 * 
 * * Much of the Nitrogen & Bends code from Seraphim Risen's NitrogenMod (but entirely new main algorithm and w/ more UI feedback)
 * * Radiation Mod material from libraryaddict, used with permission. 
 * * Escape Pod Unleashed material from oldark, w/ fixes provided to make pod gravity work reliably.
 * * Substantial increase in damage taken
 * * More aggressive creatures
 * * "Cause of Death" and "Time of Death" reporting
 * * Higher energy costs, especially to fabricate
 * * More warnings about explosion, radiation, etc.
 * * Murky water option
 */
namespace DeathRun
{
    using System;
    using System.Reflection;
    using HarmonyLib;
    using SMLHelper.V2.Handlers;
    using Common;
    using Items;
    using UnityEngine;
    using Patchers;
    using SMLHelper.V2.Crafting;
    using System.Collections.Generic;
    using BepInEx;

    [BepInPlugin("cattlesquat.deathrun.mod", "DeathRun", "3.0.0")]
    public class DeathRunPlugin : BaseUnityPlugin
    {        
        public const string modID = "DeathRun";
        public const string modName = "[DeathRun]";
        public const string SaveFile = modID + "_" + "SavedGame.json";
        public const string StatsFile = modID + "_" + "Stats.json";

        // DeathRun's saved games are handled in DeathRunUtils
        public static DeathRunSaveData saveData = new DeathRunSaveData();
        public static DeathRunSaveListener saveListener;

        // DeathRun's "Stats" are saved and loaded in DeathRunUtils
        public static DeathRunStats statsData = new DeathRunStats();

        public const string modFolder = "./BepInEx/plugins/DeathRun/";
        private const string assetFolder = modFolder + "Assets/";
        private const string assetBundle = assetFolder + "n2warning";

        public static GameObject N2HUD { get; set; }

        public static global::Utils.ScalarMonitor countdownMonitor { get; set; } = new global::Utils.ScalarMonitor(0f);
        public static global::Utils.ScalarMonitor playerMonitor { get; set; } = new global::Utils.ScalarMonitor(0f);

        public static Items.FilterChip filterChip = new Items.FilterChip();
        public static Items.DecoModule decoModule = new Items.DecoModule();

        //public static bool podGravity  = true;
        public static float configDirty = 0;

        public static bool murkinessDirty    = false;
        public static bool encyclopediaAdded = false;

        // These semaphore relate to "flavors" of energy consumption
        public static bool craftingSemaphore = false;
        public static bool chargingSemaphore = false;
        public static bool filterSemaphore   = false;
        public static bool scannerSemaphore  = false;

        // So that the explody fish stay hidden in ambush
        public static bool crashFishSemaphore = false;

        // Don't do extra warnings during respawn process while player is already dead
        public static bool playerIsDead = false;

        // Let player know if patch didn't complete
        public static bool patchFailed = false;

        public const string CAUSE_UNKNOWN = "Unknown";
        public const string CAUSE_UNKNOWN_CREATURE = "Unknown Creature";

        public const float FULL_AGGRESSION = 2400; // 40 minutes
        public const float MORE_AGGRESSION = 1200; // 20 minutes

        // Temporary storage for "cause of death"
        public static string cause = CAUSE_UNKNOWN;
        public static GameObject causeObject = null;

        // An even more annoying code-path where a cinematic has to finish running before the player dies
        public static string cinematicCause = CAUSE_UNKNOWN;
        public static GameObject cinematicCauseObject = null;

        internal static DeathRun.Config config { get; } = OptionsPanelHandler.Main.RegisterModOptions<DeathRun.Config>();

        public void Awake()
        {
            CattleLogger.setModName(modName);
            CattleLogger.PatchStart(DeathRunUtils.VERSION);

            try
            {
                Harmony harmony = new Harmony("cattlesquat.deathrun.mod");

                CattleLogger.Message("Asset Bundle");

                AssetBundle ab = AssetBundle.LoadFromFile(assetBundle);
                N2HUD = ab.LoadAsset("NMHUD") as GameObject;

                CattleLogger.Message("Warn-Failure Patch");

                harmony.Patch(AccessTools.Method(typeof(MainMenuController), "Start"),
                    null, new HarmonyMethod(typeof(WarnFailurePatcher).GetMethod("Postfix")));

                //harmony.Patch(AccessTools.Method(typeof(IngameMenu), "Awake"),
                //    null, new HarmonyMethod(typeof(WarnFailurePatcher).GetMethod("Postfix")));

                CattleLogger.Message("Main Patch");

                harmony.PatchAll(Assembly.GetExecutingAssembly());

                CattleLogger.Message("Items");

                DummySuitItems.PatchDummyItems();
                ReinforcedSuitsCore.PatchSuits();
                if (DeathRunPlugin.config.enableSpecialtyTanks)
                {
                    O2TanksCore.PatchTanks();
                }

                CattleLogger.Message("Items - Filter Chip");
                filterChip.Patch();
                KnownTechHandler.SetAnalysisTechEntry(filterChip.TechType, new List<TechType> { filterChip.TechType }, "Blueprint Unlocked");

                CattleLogger.Message("Items - Deco Module");
                decoModule.Patch();
                KnownTechHandler.SetAnalysisTechEntry(TechType.GhostRayBlue, new List<TechType> { decoModule.TechType }, "Blueprint Unlocked");

                //Console.WriteLine(typeof(NitroDamagePatcher).AssemblyQualifiedName);

                CattleLogger.Message("Explosion Depth");

                harmony.Patch(typeof(CrashedShipExploder).GetMethod("CreateExplosiveForce", BindingFlags.NonPublic | BindingFlags.Instance),
                     null, new HarmonyMethod(typeof(ExplosionPatcher).GetMethod("CreateExplosiveForce")));

                CattleLogger.Message("Surface Air Poisoning");
                //if (config.poisonedAir)
                //{
                    harmony.Patch(AccessTools.Method(typeof(Player), "CanBreathe"),
                        new HarmonyMethod(typeof(BreathingPatcher).GetMethod("CanBreathe")), null);

                    harmony.Patch(AccessTools.Method(typeof(Player), "GetBreathPeriod"), null,
                        new HarmonyMethod(typeof(BreathingPatcher).GetMethod("GetBreathPeriod")));

                    harmony.Patch(AccessTools.Method(typeof(OxygenManager), "AddOxygenAtSurface"),
                        new HarmonyMethod(typeof(BreathingPatcher).GetMethod("AddOxygenAtSurface")), null);

                    harmony.Patch(AccessTools.Method(typeof(WaterAmbience), "PlayReachSurfaceSound"),
                        new HarmonyMethod(typeof(BreathingPatcher).GetMethod("PlayReachSurfaceSound")), null);

                    //harmony.Patch(AccessTools.Method(typeof(PipeSurfaceFloater), "GetProvidesOxygen"),
                    //    new HarmonyMethod(typeof(PatchBreathing).GetMethod("GetProvidesOxygen")), null);
                //}

                CattleLogger.Message("Radiation Warning");
                //if (config.radiationWarning)
                //{
                    harmony.Patch(AccessTools.Method(typeof(uGUI_RadiationWarning), "IsRadiated"),
                        new HarmonyMethod(typeof(RadiationPatcher).GetMethod("IsRadiated")), null);

                    harmony.Patch(AccessTools.Method(typeof(uGUI_RadiationWarning), "Update"),
                        new HarmonyMethod(typeof(RadiationPatcher).GetMethod("Update")), null);

                    //harmony.Patch(AccessTools.Method(typeof(uGUI_DepthCompass), "UpdateDepth"),
                    //    new HarmonyMethod(typeof(PatchRadiation).GetMethod("UpdateDepth")), null);

                //}

                CattleLogger.Message("Radiation Depth");
                //if (config.radiativeDepth > 0)
                //{
                    harmony.Patch(AccessTools.Method(typeof(RadiatePlayerInRange), "Radiate"),
                        new HarmonyMethod(typeof(RadiationPatcher).GetMethod("Radiate")), null);
                 
                    harmony.Patch(AccessTools.Method(typeof(DamagePlayerInRadius), "DoDamage"),
                        new HarmonyMethod(typeof(RadiationPatcher).GetMethod("DoDamage")), null);
                //}

                CattleLogger.Message("Power Consumption");

                //if (!Config.NORMAL.Equals(DeathRunPlugin.config.powerCosts)) { 
                harmony.Patch(AccessTools.Method(typeof(PowerSystem), "AddEnergy"),
                        new HarmonyMethod(typeof(PowerPatcher).GetMethod("AddEnergyBase")), null);

                harmony.Patch(AccessTools.Method(typeof(SolarPanel), "Update"),
                        new HarmonyMethod(typeof(PowerPatcher).GetMethod("SolarPanelUpdate")), null);

                    harmony.Patch(AccessTools.Method(typeof(EnergyMixin), "AddEnergy"),
                        new HarmonyMethod(typeof(PowerPatcher).GetMethod("AddEnergyTool")), null);

                    harmony.Patch(AccessTools.Method(typeof(Vehicle), "AddEnergy", new Type[] { typeof(float) }),
                        new HarmonyMethod(typeof(PowerPatcher).GetMethod("AddEnergyVehicle")), null);


                    harmony.Patch(AccessTools.Method(typeof(PowerSystem), "ConsumeEnergy"),
                        new HarmonyMethod(typeof(PowerPatcher).GetMethod("ConsumeEnergyBase")), null);

                    harmony.Patch(AccessTools.Method(typeof(EnergyMixin), "ConsumeEnergy"),
                        new HarmonyMethod(typeof(PowerPatcher).GetMethod("ConsumeEnergyTool")), null);

                    harmony.Patch(AccessTools.Method(typeof(Vehicle), "ConsumeEnergy", new Type[] { typeof(float) }),
                        new HarmonyMethod(typeof(PowerPatcher).GetMethod("ConsumeEnergyVehicle")), null);


                    harmony.Patch(AccessTools.Method(typeof(CrafterLogic), "ConsumeEnergy"), 
                        new HarmonyMethod(typeof(PowerPatcher).GetMethod("ConsumeEnergyFabricatorPrefix")), null);

                    harmony.Patch(AccessTools.Method(typeof(CrafterLogic), "ConsumeEnergy"), null,
                        new HarmonyMethod(typeof(PowerPatcher).GetMethod("ConsumeEnergyFabricatorPostfix")), null);

                    harmony.Patch(AccessTools.Method(typeof(FiltrationMachine), "UpdateFiltering"),
                        new HarmonyMethod(typeof(PowerPatcher).GetMethod("ConsumeEnergyFiltrationPrefix")), null);

                    harmony.Patch(AccessTools.Method(typeof(FiltrationMachine), "UpdateFiltering"), null, 
                        new HarmonyMethod(typeof(PowerPatcher).GetMethod("ConsumeEnergyFiltrationPostfix")), null);

                    harmony.Patch(AccessTools.Method(typeof(MapRoomFunctionality), "UpdateScanning"),
                        new HarmonyMethod(typeof(PowerPatcher).GetMethod("ConsumeEnergyScanningPrefix")), null);

                    harmony.Patch(AccessTools.Method(typeof(MapRoomFunctionality), "UpdateScanning"), null, 
                        new HarmonyMethod(typeof(PowerPatcher).GetMethod("ConsumeEnergyScanningPostfix")), null);

                    harmony.Patch(AccessTools.Method(typeof(Charger), "Update"),
                        new HarmonyMethod(typeof(PowerPatcher).GetMethod("ConsumeEnergyChargingPrefix")), null);

                    harmony.Patch(AccessTools.Method(typeof(Charger), "Update"), null, 
                        new HarmonyMethod(typeof(PowerPatcher).GetMethod("ConsumeEnergyChargingPostfix")), null);



                    //harmony.Patch(AccessTools.Method(typeof(RegeneratePowerSource), "Start"), null,
                    //    new HarmonyMethod(typeof(PowerPatcher).GetMethod("RegeneratePowerStart")), null);



                //CattleLogger.Message("Disable Fabricator Food");
                //if (config.disableFabricatorFood)
                //{
                //    harmony.Patch(AccessTools.Method(typeof(CrafterLogic), "IsCraftRecipeFulfilled"),
                //        new HarmonyMethod(typeof(PatchItems).GetMethod("IsCraftRecipeFulfilled")), null);
                //}

                CattleLogger.Message("Food Pickup");

                // Disable the hover hand, and disable ability to click
                harmony.Patch(AccessTools.Method(typeof(PickPrefab), "OnHandHover"),
                    new HarmonyMethod(typeof(ItemPatcher).GetMethod("HandleItemPickup")), null);
                harmony.Patch(AccessTools.Method(typeof(PickPrefab), "OnHandClick"),
                    new HarmonyMethod(typeof(ItemPatcher).GetMethod("HandleItemPickup")), null);

                harmony.Patch(AccessTools.Method(typeof(RepulsionCannon), "ShootObject"),
                    new HarmonyMethod(typeof(ItemPatcher).GetMethod("ShootObject")), null);

                harmony.Patch(AccessTools.Method(typeof(PropulsionCannon), "ValidateObject"),
                    new HarmonyMethod(typeof(ItemPatcher).GetMethod("ValidateObject")), null);

                // Don't let player smash the resources for seeds
                harmony.Patch(typeof(Knife).GetMethod("GiveResourceOnDamage", BindingFlags.NonPublic | BindingFlags.Instance),
                    new HarmonyMethod(typeof(ItemPatcher).GetMethod("GiveResourceOnDamage")), null);


                CattleLogger.Message("Vehicle Costs: " + DeathRunPlugin.config.vehicleCosts);

                Dictionary<TechType, TechData> techChanges = new Dictionary<TechType, TechData>();

                // HATCHING ENZYMES - only changes if No Vehicles run (gives us extra copies to make vehicles after curing Kharaa)
                if (DeathRun.Config.NO_VEHICLES.Equals(DeathRunPlugin.config.vehicleCosts))
                {
                    CattleLogger.Message("Hatching Enzymes");

                    techChanges.Add(TechType.HatchingEnzymes,
                        new TechData
                        {
                            craftAmount = 5, // Gives you extra copies of Hatching Enzymes so that vehicles are then unlocked
                            Ingredients = new List<Ingredient>
                                    {
                                        new Ingredient(TechType.EyesPlantSeed, 1),
                                        new Ingredient(TechType.SeaCrownSeed, 1),
                                        new Ingredient(TechType.TreeMushroomPiece, 1),
                                        new Ingredient(TechType.RedGreenTentacleSeed, 1),
                                        new Ingredient(TechType.KooshChunk, 1)
                                    }
                        }
                    );
                }

                List<Ingredient> ingredients;

                // SEAGLIDE
                CattleLogger.Message("Seaglide");
                ingredients = null;
                if (DeathRun.Config.DEATH_VEHICLES.Equals(DeathRunPlugin.config.vehicleCosts) || DeathRun.Config.DEATH_VEHICLES_2.Equals(DeathRunPlugin.config.vehicleCosts) || DeathRun.Config.NO_VEHICLES.Equals(DeathRunPlugin.config.vehicleCosts))
                {
                    ingredients = new List<Ingredient> {
                        //new Ingredient(TechType.Battery, 1),
                        new Ingredient(TechType.Lubricant, 2),
                        new Ingredient(TechType.CopperWire, 1),
                        new Ingredient(TechType.Lead, 2),
                        new Ingredient(TechType.Gold, 2)
                    };
                }
                else if (DeathRun.Config.HARD_VEHICLES.Equals(DeathRunPlugin.config.vehicleCosts))
                {
                    ingredients = new List<Ingredient>
                    {
                        //new Ingredient(TechType.Battery, 1),
                        new Ingredient(TechType.Lubricant, 2),
                        new Ingredient(TechType.CopperWire, 1),
                        new Ingredient(TechType.Lead, 1),
                        new Ingredient(TechType.Gold, 1)
                    };
                } else
                {
                    ingredients = new List<Ingredient>
                    {
                        //new Ingredient(TechType.Battery, 1),
                        new Ingredient(TechType.Lubricant, 1),
                        new Ingredient(TechType.CopperWire, 1),
                        new Ingredient(TechType.Titanium, 1)
                    };

                }
                if (ingredients != null)
                {
                    if (DeathRun.Config.NORMAL.Equals(DeathRunPlugin.config.batteryCosts))
                    {
                        ingredients.Add(new Ingredient(TechType.Battery, 1));
                    }
                    techChanges.Add(TechType.Seaglide, new TechData { craftAmount = 1, Ingredients = ingredients });
                }


                // SEAMOTH
                CattleLogger.Message("Seamoth");
                if (DeathRun.Config.NO_VEHICLES.Equals(DeathRunPlugin.config.vehicleCosts))
                {
                    ingredients = new List<Ingredient> {
                        new Ingredient(TechType.TitaniumIngot, 1),
                        //new Ingredient(TechType.PowerCell, 1),
                        new Ingredient(TechType.Glass, 2),
                        new Ingredient(TechType.Lubricant, 1),
                        new Ingredient(TechType.Lead, 1),
                        new Ingredient(TechType.HatchingEnzymes, 1)
                    };
                }
                else if (DeathRun.Config.DEATH_VEHICLES.Equals(DeathRunPlugin.config.vehicleCosts))
                {
                    ingredients = new List<Ingredient> {
                        new Ingredient(TechType.PlasteelIngot, 1),
                        //new Ingredient(TechType.PowerCell, 1),
                        new Ingredient(TechType.EnameledGlass, 2),
                        new Ingredient(TechType.Lubricant, 2),
                        new Ingredient(TechType.Lead, 4),
                        new Ingredient(TechType.TreeMushroomPiece, 1),
                        new Ingredient(TechType.KooshChunk, 1),
                        new Ingredient(TechType.RedGreenTentacleSeed, 1),
                        new Ingredient(TechType.EyesPlantSeed, 1)
                    };
                }
                else if (DeathRun.Config.DEATH_VEHICLES_2.Equals(DeathRunPlugin.config.vehicleCosts))
                {
                    ingredients = new List<Ingredient> {
                        new Ingredient(TechType.PlasteelIngot, 1),
                        //new Ingredient(TechType.PowerCell, 1),
                        new Ingredient(TechType.EnameledGlass, 2),
                        new Ingredient(TechType.Lubricant, 2),
                        new Ingredient(TechType.Lead, 4),
                        new Ingredient(TechType.HydrochloricAcid, 3),
                        new Ingredient(TechType.Aerogel, 1)
                    };
                }
                else if (DeathRun.Config.HARD_VEHICLES.Equals(DeathRunPlugin.config.vehicleCosts))
                {
                    ingredients = new List<Ingredient>
                    {
                        new Ingredient(TechType.PlasteelIngot, 1),
                        //new Ingredient(TechType.PowerCell, 1),
                        new Ingredient(TechType.EnameledGlass, 2),
                        new Ingredient(TechType.Lubricant, 3),
                        new Ingredient(TechType.Lead, 4)
                    };
                } 
                else
                {
                    ingredients = new List<Ingredient>
                    {
                        new Ingredient(TechType.TitaniumIngot, 1),
                        //new Ingredient(TechType.PowerCell, 1),
                        new Ingredient(TechType.Glass, 2),
                        new Ingredient(TechType.Lubricant, 1),
                        new Ingredient(TechType.Lead, 1)
                    };
                }
                if (DeathRun.Config.NORMAL.Equals(DeathRunPlugin.config.batteryCosts))
                {
                    ingredients.Add(new Ingredient(TechType.PowerCell, 1));
                }
                techChanges.Add(TechType.Seamoth, new TechData { craftAmount = 1, Ingredients = ingredients });

                // SEAMOTH Depth Modules
                CattleLogger.Message("Seamoth Depth Modules");
                if (DeathRun.Config.DEATH_VEHICLES.Equals(DeathRunPlugin.config.vehicleCosts) || DeathRun.Config.DEATH_VEHICLES_2.Equals(DeathRunPlugin.config.vehicleCosts))
                {
                    techChanges.Add(TechType.VehicleHullModule1,
                                    new TechData
                                    {
                                        craftAmount = 1,
                                        Ingredients = new List<Ingredient>
                                        {
                                            new Ingredient(TechType.TitaniumIngot, 1),
                                            new Ingredient(TechType.Magnetite, 2),
                                            new Ingredient(TechType.Aerogel, 3),
                                            new Ingredient(TechType.EnameledGlass, 1)
                                        }
                                    });

                    techChanges.Add(TechType.VehicleHullModule2,
                                    new TechData
                                    {
                                        craftAmount = 1,
                                        Ingredients = new List<Ingredient>
                                        {
                                            new Ingredient(TechType.VehicleHullModule1, 1),
                                            new Ingredient(TechType.PlasteelIngot, 1),
                                            new Ingredient(TechType.Nickel, 3),
                                            new Ingredient(TechType.EnameledGlass, 1)
                                        }
                                    });

                    techChanges.Add(TechType.VehicleHullModule3,
                                    new TechData
                                    {
                                        craftAmount = 1,
                                        Ingredients = new List<Ingredient>
                                        {
                                            new Ingredient(TechType.VehicleHullModule2, 1),
                                            new Ingredient(TechType.PlasteelIngot, 1),
                                            new Ingredient(TechType.Sulphur, 3),
                                            new Ingredient(TechType.EnameledGlass, 1)
                                        }
                                    });

                }


                // PRAWN
                CattleLogger.Message("Prawn");
                ingredients = null;
                if (DeathRun.Config.NO_VEHICLES.Equals(DeathRunPlugin.config.vehicleCosts))
                {
                    ingredients = new List<Ingredient> {
                        new Ingredient(TechType.PlasteelIngot, 2),
                        new Ingredient(TechType.Aerogel, 1),
                        new Ingredient(TechType.EnameledGlass, 1),
                        new Ingredient(TechType.Diamond, 2),
                        new Ingredient(TechType.Lead, 2),
                        new Ingredient(TechType.HatchingEnzymes, 1)
                    };
                }
                else if (DeathRun.Config.DEATH_VEHICLES.Equals(DeathRunPlugin.config.vehicleCosts) || DeathRun.Config.DEATH_VEHICLES_2.Equals(DeathRunPlugin.config.vehicleCosts))
                {
                    ingredients = new List<Ingredient> {
                        new Ingredient(TechType.PlasteelIngot, 2),
                        new Ingredient(TechType.Aerogel, 2),
                        new Ingredient(TechType.EnameledGlass, 2),
                        new Ingredient(TechType.Diamond, 2),
                        new Ingredient(TechType.Sulphur, 3),
                        new Ingredient(TechType.Nickel, 3),
                        new Ingredient(TechType.Lubricant, 3)
                    };
                }
                else if (DeathRun.Config.HARD_VEHICLES.Equals(DeathRunPlugin.config.vehicleCosts))
                {
                    ingredients = new List<Ingredient>
                    {
                        new Ingredient(TechType.PlasteelIngot, 2),
                        new Ingredient(TechType.Aerogel, 2),
                        new Ingredient(TechType.EnameledGlass, 1),
                        new Ingredient(TechType.Diamond, 2),
                        new Ingredient(TechType.Sulphur, 2),
                        new Ingredient(TechType.Nickel, 2),
                        new Ingredient(TechType.Lubricant, 2)
                    };
                }
                if (ingredients != null)
                {
                    //if (Config.NORMAL.Equals(DeathRunPlugin.config.batteryCosts))
                    //{
                    //    ingredients.Add(new Ingredient(TechType.PowerCell, 1));
                    //}
                    techChanges.Add(TechType.Exosuit, new TechData { craftAmount = 1, Ingredients = ingredients });
                }

                // PRAWN Depth Module
                CattleLogger.Message("Prawn Depth Modules");
                if (DeathRun.Config.DEATH_VEHICLES.Equals(DeathRunPlugin.config.vehicleCosts) || DeathRun.Config.DEATH_VEHICLES_2.Equals(DeathRunPlugin.config.vehicleCosts))
                {
                    techChanges.Add(TechType.ExoHullModule1,
                                    new TechData
                                    {
                                        craftAmount = 1,
                                        Ingredients = new List<Ingredient>
                                        {
                                            new Ingredient(TechType.PlasteelIngot, 1),
                                            new Ingredient(TechType.Nickel, 3),
                                            new Ingredient(TechType.Sulphur, 3),
                                            new Ingredient(TechType.Kyanite, 1)
                                        }
                                    });
                }


                // CYCLOPS
                CattleLogger.Message("Cyclops");
                ingredients = null;
                if (DeathRun.Config.NO_VEHICLES.Equals(DeathRunPlugin.config.vehicleCosts))
                {
                    ingredients = new List<Ingredient> {
                        new Ingredient(TechType.PlasteelIngot, 3),
                        new Ingredient(TechType.EnameledGlass, 3),
                        new Ingredient(TechType.Lubricant, 1),
                        new Ingredient(TechType.AdvancedWiringKit, 1),
                        new Ingredient(TechType.Lead, 3),
                        new Ingredient(TechType.HatchingEnzymes, 1)
                    };
                }
                else if (DeathRun.Config.DEATH_VEHICLES.Equals(DeathRunPlugin.config.vehicleCosts) || DeathRun.Config.DEATH_VEHICLES_2.Equals(DeathRunPlugin.config.vehicleCosts))
                {
                    ingredients = new List<Ingredient> {
                        new Ingredient(TechType.PlasteelIngot, 3),
                        new Ingredient(TechType.EnameledGlass, 3),
                        new Ingredient(TechType.Lubricant, 4),
                        new Ingredient(TechType.AdvancedWiringKit, 1),
                        new Ingredient(TechType.UraniniteCrystal, 6),
                        new Ingredient(TechType.Nickel, 3),
                    };
                }
                else if (DeathRun.Config.HARD_VEHICLES.Equals(DeathRunPlugin.config.vehicleCosts))
                {
                    ingredients = new List<Ingredient>
                    {
                        new Ingredient(TechType.PlasteelIngot, 3),
                        new Ingredient(TechType.EnameledGlass, 3),
                        new Ingredient(TechType.Lubricant, 4),
                        new Ingredient(TechType.AdvancedWiringKit, 1),
                        new Ingredient(TechType.Lead, 4),
                        new Ingredient(TechType.UraniniteCrystal, 2),
                        new Ingredient(TechType.Nickel, 2)
                    };
                }
                if (ingredients != null)
                {
                    //if (Config.NORMAL.Equals(DeathRunPlugin.config.batteryCosts))
                    //{
                    //    ingredients.Add(new Ingredient(TechType.PowerCell, 1));
                    //}
                    techChanges.Add(TechType.Cyclops, new TechData { craftAmount = 1, Ingredients = ingredients });
                }


                // CYCLOPS Depth Modules
                CattleLogger.Message("Cyclops Depth Modules");
                if (DeathRun.Config.DEATH_VEHICLES.Equals(DeathRunPlugin.config.vehicleCosts) || DeathRun.Config.DEATH_VEHICLES_2.Equals(DeathRunPlugin.config.vehicleCosts))
                {
                    techChanges.Add(TechType.CyclopsHullModule1,
                                    new TechData
                                    {
                                        craftAmount = 1,
                                        Ingredients = new List<Ingredient>
                                        {
                                            new Ingredient(TechType.PlasteelIngot, 1),
                                            new Ingredient(TechType.AluminumOxide, 3),
                                            new Ingredient(TechType.Nickel, 3)
                                        }
                                    });

                    techChanges.Add(TechType.CyclopsHullModule2,
                                    new TechData
                                    {
                                        craftAmount = 1,
                                        Ingredients = new List<Ingredient>
                                        {
                                            new Ingredient(TechType.CyclopsHullModule1, 1),
                                            new Ingredient(TechType.PlasteelIngot, 1),
                                            new Ingredient(TechType.Sulphur, 3),
                                            new Ingredient(TechType.Kyanite, 1),
                                        }
                                    });

                    techChanges.Add(TechType.CyclopsHullModule3,
                                    new TechData
                                    {
                                        craftAmount = 1,
                                        Ingredients = new List<Ingredient>
                                        {
                                            new Ingredient(TechType.CyclopsHullModule2, 1),
                                            new Ingredient(TechType.Sulphur, 3),
                                            new Ingredient(TechType.Nickel, 3),
                                            new Ingredient(TechType.Kyanite, 5),
                                        }
                                    });
                }


                CattleLogger.Message("Habitat Builder Costs");

                if (DeathRun.Config.DEATHRUN.Equals(DeathRunPlugin.config.builderCosts))
                {
                    // Habitat Builder
                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.ComputerChip, 2),
                            new Ingredient(TechType.WiringKit, 2),
                            //new Ingredient(TechType.Battery, 1),
                            new Ingredient(TechType.Lithium, 2),
                            new Ingredient(TechType.Magnetite, 1)
                        };
                    if (DeathRun.Config.NORMAL.Equals(DeathRunPlugin.config.batteryCosts))
                    {
                        ingredients.Add(new Ingredient(TechType.Battery, 1));
                    }
                    techChanges.Add(TechType.Builder, new TechData { craftAmount = 1, Ingredients = ingredients });

                    // Stasis Rifle
                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.ComputerChip, 1),
                            new Ingredient(TechType.Magnetite, 4),
                            new Ingredient(TechType.UraniniteCrystal, 2),
                            new Ingredient(TechType.Benzene, 1),
                        };
                    if (DeathRun.Config.NORMAL.Equals(DeathRunPlugin.config.batteryCosts))
                    {
                        ingredients.Add(new Ingredient(TechType.Battery, 1));
                    }
                    techChanges.Add(TechType.StasisRifle, new TechData { craftAmount = 1, Ingredients = ingredients });

                    // Propulsion Cannon
                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.WiringKit, 1),
                            new Ingredient(TechType.Magnetite, 2),
                            new Ingredient(TechType.Lead, 2),
                        };
                    if (DeathRun.Config.NORMAL.Equals(DeathRunPlugin.config.batteryCosts))
                    {
                        ingredients.Add(new Ingredient(TechType.Battery, 1));
                    }
                    techChanges.Add(TechType.PropulsionCannon, new TechData { craftAmount = 1, Ingredients = ingredients });

                    // Repulsion Cannon
                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.PropulsionCannon, 1),
                            new Ingredient(TechType.ComputerChip, 1),
                            new Ingredient(TechType.Magnetite, 4),
                        };
                    techChanges.Add(TechType.RepulsionCannon, new TechData { craftAmount = 1, Ingredients = ingredients });

                    // First Aid Cabinet
                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.ComputerChip, 1),
                            new Ingredient(TechType.FiberMesh, 4),
                            new Ingredient(TechType.Silver, 1),
                            new Ingredient(TechType.Titanium, 1)
                        };
                    techChanges.Add(TechType.MedicalCabinet, new TechData { craftAmount = 1, Ingredients = ingredients });

                    // Moonpool
                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.TitaniumIngot, 2),
                            new Ingredient(TechType.WiringKit, 2),
                            new Ingredient(TechType.Lubricant, 2),
                            new Ingredient(TechType.Lead, 4),
                        };
                    techChanges.Add(TechType.BaseMoonpool, new TechData { craftAmount = 1, Ingredients = ingredients });

                    // Bioreactor
                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.TitaniumIngot, 1),
                            new Ingredient(TechType.WiringKit, 1),
                            new Ingredient(TechType.FiberMesh, 1),
                            new Ingredient(TechType.Lubricant, 2),
                        };
                    techChanges.Add(TechType.BaseBioReactor, new TechData { craftAmount = 1, Ingredients = ingredients });

                    // Thermal Plant
                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.TitaniumIngot, 1),
                            new Ingredient(TechType.Magnetite, 3),
                            new Ingredient(TechType.Aerogel, 2),
                        };
                    techChanges.Add(TechType.ThermalPlant, new TechData { craftAmount = 1, Ingredients = ingredients });

                    // Nuclear Reactor
                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.PlasteelIngot, 2),
                            new Ingredient(TechType.AdvancedWiringKit, 2),
                            new Ingredient(TechType.Lead, 8),
                        };
                    techChanges.Add(TechType.BaseNuclearReactor, new TechData { craftAmount = 1, Ingredients = ingredients });
                }
                else if (DeathRun.Config.HARD.Equals(DeathRunPlugin.config.builderCosts))
                {
                    // Habitat Builder
                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.ComputerChip, 2),
                            new Ingredient(TechType.WiringKit, 2),
                            //new Ingredient(TechType.Battery, 1),
                            new Ingredient(TechType.Lithium, 1),
                        };
                    if (!DeathRun.Config.NORMAL.Equals(DeathRunPlugin.config.batteryCosts))
                    {
                        ingredients.Add(new Ingredient(TechType.Battery, 1));
                    }
                    techChanges.Add(TechType.Builder, new TechData { craftAmount = 1, Ingredients = ingredients });

                    // Stasis Rifle
                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.ComputerChip, 1),
                            new Ingredient(TechType.Magnetite, 2),
                            new Ingredient(TechType.UraniniteCrystal, 1),
                        };
                    if (DeathRun.Config.NORMAL.Equals(DeathRunPlugin.config.batteryCosts))
                    {
                        ingredients.Add(new Ingredient(TechType.Battery, 1));
                    }
                    techChanges.Add(TechType.StasisRifle, new TechData { craftAmount = 1, Ingredients = ingredients });

                    // Propulsion Cannon
                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.WiringKit, 1),
                            new Ingredient(TechType.Magnetite, 1),
                            new Ingredient(TechType.Lead, 2),
                        };
                    if (DeathRun.Config.NORMAL.Equals(DeathRunPlugin.config.batteryCosts))
                    {
                        ingredients.Add(new Ingredient(TechType.Battery, 1));
                    }
                    techChanges.Add(TechType.PropulsionCannon, new TechData { craftAmount = 1, Ingredients = ingredients });

                    // Repulsion Cannon
                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.PropulsionCannon, 1),
                            new Ingredient(TechType.ComputerChip, 1),
                            new Ingredient(TechType.Magnetite, 3),
                        };
                    techChanges.Add(TechType.RepulsionCannon, new TechData { craftAmount = 1, Ingredients = ingredients });

                    // First Aid Cabinet
                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.ComputerChip, 1),
                            new Ingredient(TechType.FiberMesh, 3),
                            new Ingredient(TechType.Silver, 1),
                            new Ingredient(TechType.Titanium, 1)
                        };
                    techChanges.Add(TechType.MedicalCabinet, new TechData { craftAmount = 1, Ingredients = ingredients });


                    // Moonpool
                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.TitaniumIngot, 1),
                            new Ingredient(TechType.WiringKit, 1),
                            new Ingredient(TechType.Lubricant, 1),
                            new Ingredient(TechType.Lead, 3),
                        };
                    techChanges.Add(TechType.BaseMoonpool, new TechData { craftAmount = 1, Ingredients = ingredients });

                    // Bioreactor
                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.TitaniumIngot, 1),
                            new Ingredient(TechType.WiringKit, 1),
                            new Ingredient(TechType.FiberMesh, 1),
                            new Ingredient(TechType.Lubricant, 1),
                        };
                    techChanges.Add(TechType.BaseBioReactor, new TechData { craftAmount = 1, Ingredients = ingredients });

                    // Thermal Plant
                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.TitaniumIngot, 1),
                            new Ingredient(TechType.Magnetite, 2),
                            new Ingredient(TechType.Aerogel, 1),
                        };
                    techChanges.Add(TechType.ThermalPlant, new TechData { craftAmount = 1, Ingredients = ingredients });

                    // Nuclear Reactor
                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.PlasteelIngot, 1),
                            new Ingredient(TechType.AdvancedWiringKit, 1),
                            new Ingredient(TechType.Lead, 8),
                        };
                    techChanges.Add(TechType.BaseNuclearReactor, new TechData { craftAmount = 1, Ingredients = ingredients });

                }

                CattleLogger.Message("Scans Required");
                if (DeathRun.Config.EXORBITANT.Equals(DeathRunPlugin.config.scansRequired))
                {
                    PDAHandler.EditFragmentsToScan(TechType.Seaglide, 6);
                    PDAHandler.EditFragmentsToScan(TechType.Seamoth, 20);
                    PDAHandler.EditFragmentsToScan(TechType.ExosuitFragment, 8);
                    PDAHandler.EditFragmentsToScan(TechType.CyclopsBridgeFragment, 7);
                    PDAHandler.EditFragmentsToScan(TechType.CyclopsDockingBayFragment, 7);
                    PDAHandler.EditFragmentsToScan(TechType.CyclopsEngineFragment, 7);
                    PDAHandler.EditFragmentsToScan(TechType.CyclopsHullFragment, 7);

                    PDAHandler.EditFragmentsToScan(TechType.Beacon, 6);
                    PDAHandler.EditFragmentsToScan(TechType.Gravsphere, 4);
                    PDAHandler.EditFragmentsToScan(TechType.StasisRifle, 6);
                    PDAHandler.EditFragmentsToScan(TechType.PropulsionCannon, 6);
                    PDAHandler.EditFragmentsToScan(TechType.LaserCutter, 6);
                    PDAHandler.EditFragmentsToScan(TechType.LaserCutterFragment, 6);
                    PDAHandler.EditFragmentsToScan(TechType.BatteryCharger, 6);
                    PDAHandler.EditFragmentsToScan(TechType.PowerCellCharger, 6);
                    PDAHandler.EditFragmentsToScan(TechType.Constructor, 10);
                    PDAHandler.EditFragmentsToScan(TechType.BaseBioReactor, 6);
                    PDAHandler.EditFragmentsToScan(TechType.BaseNuclearReactor, 6);
                    PDAHandler.EditFragmentsToScan(TechType.ThermalPlant, 6);
                    PDAHandler.EditFragmentsToScan(TechType.BaseMoonpool, 6);
                    PDAHandler.EditFragmentsToScan(TechType.PowerTransmitter, 4);
                    PDAHandler.EditFragmentsToScan(TechType.BaseMapRoom, 6);
                }
                else if (DeathRun.Config.DEATHRUN.Equals(DeathRunPlugin.config.scansRequired))
                {
                    CattleLogger.Message("Scans Required: DeathRun");

                    PDAHandler.EditFragmentsToScan(TechType.Seaglide, 4);
                    PDAHandler.EditFragmentsToScan(TechType.Seamoth, 12);
                    PDAHandler.EditFragmentsToScan(TechType.ExosuitFragment, 7);
                    PDAHandler.EditFragmentsToScan(TechType.CyclopsBridgeFragment, 5);
                    PDAHandler.EditFragmentsToScan(TechType.CyclopsDockingBayFragment, 5);
                    PDAHandler.EditFragmentsToScan(TechType.CyclopsEngineFragment, 5);
                    PDAHandler.EditFragmentsToScan(TechType.CyclopsHullFragment, 5);

                    PDAHandler.EditFragmentsToScan(TechType.Beacon, 4);
                    PDAHandler.EditFragmentsToScan(TechType.Gravsphere, 4);
                    PDAHandler.EditFragmentsToScan(TechType.StasisRifle, 6);
                    PDAHandler.EditFragmentsToScan(TechType.PropulsionCannon, 6);
                    PDAHandler.EditFragmentsToScan(TechType.LaserCutter, 6);
                    PDAHandler.EditFragmentsToScan(TechType.LaserCutterFragment, 6);
                    PDAHandler.EditFragmentsToScan(TechType.BatteryCharger, 6);
                    PDAHandler.EditFragmentsToScan(TechType.PowerCellCharger, 6);
                    PDAHandler.EditFragmentsToScan(TechType.Constructor, 10);
                    PDAHandler.EditFragmentsToScan(TechType.BaseBioReactor, 5);
                    PDAHandler.EditFragmentsToScan(TechType.BaseNuclearReactor, 5);
                    PDAHandler.EditFragmentsToScan(TechType.ThermalPlant, 5);
                    PDAHandler.EditFragmentsToScan(TechType.BaseMoonpool, 5);
                    PDAHandler.EditFragmentsToScan(TechType.PowerTransmitter, 2);
                    PDAHandler.EditFragmentsToScan(TechType.BaseMapRoom, 5);
                }
                else if (DeathRun.Config.HARD.Equals(DeathRunPlugin.config.scansRequired))
                {
                    CattleLogger.Message("Scans Required: Hard");

                    PDAHandler.EditFragmentsToScan(TechType.Seaglide, 3);
                    PDAHandler.EditFragmentsToScan(TechType.Seamoth, 8);
                    PDAHandler.EditFragmentsToScan(TechType.ExosuitFragment, 6);
                    PDAHandler.EditFragmentsToScan(TechType.CyclopsBridgeFragment, 4);
                    PDAHandler.EditFragmentsToScan(TechType.CyclopsDockingBayFragment, 4);
                    PDAHandler.EditFragmentsToScan(TechType.CyclopsEngineFragment, 4);
                    PDAHandler.EditFragmentsToScan(TechType.CyclopsHullFragment, 4);

                    PDAHandler.EditFragmentsToScan(TechType.Beacon, 3);
                    PDAHandler.EditFragmentsToScan(TechType.Gravsphere, 3);
                    PDAHandler.EditFragmentsToScan(TechType.StasisRifle, 4);
                    PDAHandler.EditFragmentsToScan(TechType.PropulsionCannon, 4);
                    PDAHandler.EditFragmentsToScan(TechType.LaserCutter, 4);
                    PDAHandler.EditFragmentsToScan(TechType.LaserCutterFragment, 4);
                    PDAHandler.EditFragmentsToScan(TechType.Welder, 4);
                    PDAHandler.EditFragmentsToScan(TechType.BatteryCharger, 4);
                    PDAHandler.EditFragmentsToScan(TechType.PowerCellCharger, 4);
                    PDAHandler.EditFragmentsToScan(TechType.Constructor, 6);
                    PDAHandler.EditFragmentsToScan(TechType.BaseBioReactor, 4);
                    PDAHandler.EditFragmentsToScan(TechType.BaseNuclearReactor, 4);
                    PDAHandler.EditFragmentsToScan(TechType.ThermalPlant, 4);
                    PDAHandler.EditFragmentsToScan(TechType.BaseMoonpool, 4);
                    PDAHandler.EditFragmentsToScan(TechType.PowerTransmitter, 2);
                    PDAHandler.EditFragmentsToScan(TechType.BaseMapRoom, 4);
                }


                CattleLogger.Message("Battery Costs");

                if (DeathRun.Config.DEATHRUN.Equals(DeathRunPlugin.config.batteryCosts) || DeathRun.Config.EXORBITANT.Equals(DeathRunPlugin.config.batteryCosts))
                {
                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.Lithium,  1),
                            new Ingredient(TechType.Diamond,  1),
                            new Ingredient(TechType.Salt,     1),
                            new Ingredient(TechType.Silicone, 1)
                        };
                    techChanges.Add(TechType.Battery, new TechData { craftAmount = 1, Ingredients = ingredients });

                    KnownTechHandler.Main.SetAnalysisTechEntry(TechType.Lithium, new List<TechType>() { TechType.Battery });
                }
                else if (DeathRun.Config.HARD.Equals(DeathRunPlugin.config.batteryCosts))
                {
                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.Lithium,  1),
                            new Ingredient(TechType.Salt,     1),
                        };
                    techChanges.Add(TechType.Battery, new TechData { craftAmount = 1, Ingredients = ingredients });

                    KnownTechHandler.Main.SetAnalysisTechEntry(TechType.Lithium, new List<TechType>() { TechType.Battery });
                }

                CattleLogger.Message("New Batteries");

                AcidBatteryCellBase.PatchAll();

                CattleLogger.Message("Remove Batteries from Recipes");

                if (!DeathRun.Config.NORMAL.Equals(DeathRunPlugin.config.batteryCosts))
                {
                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(AcidBatteryCellBase.BatteryID, 3),
                        };
                    techChanges.Add(TechType.Copper, new TechData { craftAmount = 2, Ingredients = ingredients });

                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.Glass,    1),
                            new Ingredient(TechType.Titanium, 1)
                        };
                    techChanges.Add(TechType.Flashlight, new TechData { craftAmount = 1, Ingredients = ingredients });

                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.Titanium, 1),
                            new Ingredient(TechType.Copper, 1)
                        };
                    techChanges.Add(TechType.Scanner, new TechData { craftAmount = 1, Ingredients = ingredients });

                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.Diamond, 2),
                            new Ingredient(TechType.Titanium, 1),
                            new Ingredient(TechType.CrashPowder, 1)
                        };
                    techChanges.Add(TechType.LaserCutter, new TechData { craftAmount = 1, Ingredients = ingredients });

                    ingredients = new List<Ingredient>
                        {
                            new Ingredient(TechType.AcidMushroom, 2),
                            new Ingredient(TechType.Copper, 1),
                            new Ingredient(TechType.Lead, 1),
                            new Ingredient(TechType.Titanium, 1)
                        };
                    techChanges.Add(TechType.Gravsphere, new TechData { craftAmount = 1, Ingredients = ingredients });
                }


                CattleLogger.Message("Tech Changes");

                if (techChanges != null)
                {
                    foreach (KeyValuePair<TechType,TechData> tech in techChanges)
                    {
                        CraftDataHandler.SetTechData(tech.Key, tech.Value);
                    }
                }

                if (!DeathRun.Config.NORMAL.Equals(DeathRunPlugin.config.batteryCosts))
                {
                    CattleLogger.Message("Unlock copper recycling");
                    KnownTechHandler.Main.UnlockOnStart(TechType.Copper);
                }

                CattleLogger.Message("First Aid Kits => Quick Slots");

                if (DeathRunPlugin.config.firstAidQuickSlot)
                {
                    CraftDataHandler.SetQuickSlotType(TechType.FirstAidKit, QuickSlotType.Selectable);
                    CraftDataHandler.SetEquipmentType(TechType.FirstAidKit, EquipmentType.Hand);
                }

                Console.WriteLine("[DeathRun] Patched");

            }
            catch (Exception ex)
            {
                CattleLogger.PatchFailed(ex);
                patchFailed = true;
            }

            statsData.LoadStats();

            Console.WriteLine("[DeathRun] Stats Loaded");
        }

        public static void setCause(string newCause)
        {
            cause = newCause;
        }

        public static void setCauseObject(GameObject newCause)
        {
            causeObject = newCause;
        }


        /**
         * This gets called when we detect a brand-new-game being started from the main menu.
         */
        public static void StartNewGame()
        {
            CattleLogger.Message("Start New Game -- clearing all mod-specific player data");

            saveData = new DeathRunSaveData();
            countdownMonitor = new global::Utils.ScalarMonitor(0f);
            playerMonitor = new global::Utils.ScalarMonitor(0f);

            configDirty = 0;

            murkinessDirty = false;

            craftingSemaphore = false;
            chargingSemaphore = false;
            filterSemaphore = false;
            scannerSemaphore = false;

            cause = CAUSE_UNKNOWN;
            causeObject = null;
            cinematicCause = CAUSE_UNKNOWN;
            cinematicCauseObject = null;

            playerIsDead = false;
        }
    }


    internal class WarnFailurePatcher
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (DeathRunPlugin.patchFailed)
            {
                ErrorMessage.AddMessage("PATCH FAILED - Death Run patch failed to complete. See errorlog (Logoutput.Log) for details.");
                DeathRunUtils.CenterMessage("PATCH FAILED", 10, 6);
            }

            DeathRunUtils.ShowHighScores(true);
        }
    }
}
