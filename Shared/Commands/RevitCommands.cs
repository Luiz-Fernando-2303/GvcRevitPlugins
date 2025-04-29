namespace GvcRevitPlugins.Shared.Commands
{
    public static class RevitCommands
    {
        public static double BorderGridsOffset => Properties.Settings.Default.borderGridsOffset;
        public static string GrossVolumeParameter => Properties.Settings.Default.grossVolumeParameter;
        public static string NetVolumeParameter => Properties.Settings.Default.netVolumeParameter;
        public static bool GrossVolumeParameterIsChecked => Properties.Settings.Default.grossVolumeParameterToggleIsChecked;
        public static bool NetVolumeParameterIsChecked => Properties.Settings.Default.netVolumeParameterToggleIsChecked;
        public static bool GetAllElementsOfTypeIsChecked => Properties.Settings.Default.getAllElementsOfTypeIsChecked;
        public static int SelectedFloorPlanViewTemplateId { get; internal set; }
        public static int SelectedSheetTemplateId { get; internal set; }
        private static double platformElevation = Properties.Settings.Default.platformElevation;
        public static double PlatformElevation
        {
            get => platformElevation;
            set
            {
                platformElevation = value;
                Properties.Settings.Default.platformElevation = value;
                Properties.Settings.Default.Save();
            }
        }
        public static double MinimumDistance
        {
            get => Properties.Settings.Default.minimumDistance;
            set
            {
                Properties.Settings.Default.minimumDistance = value;
                Properties.Settings.Default.Save();
            }
        }
        public static double TerrainCheckStrucWallHeight
        {
            get => Properties.Settings.Default.terrainCheckStrucWallHeight;
            set
            {
                Properties.Settings.Default.terrainCheckStrucWallHeight = value;
                Properties.Settings.Default.Save();
            }
        }
        public static double TerrainCheckCalcDistance { get; set; }
        public static double TerrainCheckCalcHeight { get; set; }

        public static int BuildingFaceId { get; set; } = -1;
        public static int TerrainBoundaryId { get; set; } = -1;
    }
}
