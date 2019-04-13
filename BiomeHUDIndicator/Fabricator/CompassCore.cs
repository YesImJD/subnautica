﻿namespace BiomeHUDIndicator.Fabricator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using SMLHelper.V2.Assets;
    using SMLHelper.V2.Handlers;
    using SMLHelper.V2.Utility;
    using UnityEngine;
    using Common;

    internal abstract class CompassCore : Craftable
    {
        private const string craftTab = "HUDChips";
        private const string Assets = @"BiomeHUDIndicator/Assets";
        private static readonly string[] craftPath = new[] { craftTab };

        public static TechType BiomeChipID { get; protected set; }

        internal static void PatchIt()
        {
            var tabIcon = ImageUtils.LoadSpriteFromFile(@"./Qmods/" + Assets + @"/TabIcon.png");
            CraftTreeHandler.AddTabNode(CraftTree.Type.Workbench, craftTab, "HUD Chip Upgrades", tabIcon);

            SeraLogger.Message(Main.modName, "Creating new crafting tab");
            var BiomeChip = new BiomeHUDIndicator();
            BiomeChip.Patch();
        }

        protected abstract TechType BaseType { get; } // Gonna use base type Compass
        protected abstract EquipmentType Chip { get; } // Should ALWAYS be Chip, I think

        // Let's get us some hot constructor action
        protected CompassCore(string classID, string friendlyName, string description) : base(classID, friendlyName, description)
        {
            // Invoke when done
            OnFinishedPatching += SetEquipmentType;
        }

        // This will be built at the workbench, and also set where in the PDA/etc it shows up.
        public override CraftTree.Type FabricatorType { get; } = CraftTree.Type.Workbench;
        public override TechGroup GroupForPDA { get; } = TechGroup.Workbench;
        public override TechCategory CategoryForPDA { get; } = TechCategory.Workbench;
        public override string AssetsFolder { get; } = Assets;
        public override string[] StepsToFabricatorTab { get; } = craftPath;
        public override TechType RequiredForUnlock { get; } = TechType.Compass; // This will require the compass to unlock

        public override GameObject GetGameObject()
        {
            GameObject prefab = CraftData.GetPrefabForTechType(this.BaseType);
            var obj = GameObject.Instantiate(prefab);

            Compass compass = obj.GetComponent<Compass>();

            return obj;
        }

        private void SetEquipmentType()
        {
            // Make sure it is usable in a chip slot
            CraftDataHandler.SetEquipmentType(this.TechType, this.Chip);
        }
    }
}
