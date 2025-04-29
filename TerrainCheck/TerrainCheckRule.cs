using System.Diagnostics;

namespace GvcRevitPlugins.TerrainCheck
{
    internal class TerrainCheckRule
    {
        public bool IsActive { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Color { get; set; }
        public int WallTypeId { get; set; }
        public string WallTypeName { get; set; }
        public bool ExecuteCommand()
        {
            Debug.WriteLine("ExecuteCommand!");
            return true;
        }
    }
}
