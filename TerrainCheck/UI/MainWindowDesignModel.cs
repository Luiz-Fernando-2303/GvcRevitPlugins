namespace GvcRevitPlugins.TerrainCheck.UI
{
    internal class MainWindowDesignModel : MainWindowViewModel
    {
        public static MainWindowDesignModel Instance = new MainWindowDesignModel();
        public MainWindowDesignModel() : base()
        {
            Store = new Store()
            {
                PlatformElevation = 3,
                MinimumDistance = 2,
                TerrainCheckStrucWallHeight = 2,
                TerrainCheckCalcDistance = 30,
                TerrainCheckCalcHeight = 10
            };
        }
    }
}
