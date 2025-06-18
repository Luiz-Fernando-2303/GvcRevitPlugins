using Autodesk.Revit.DB;
using GvcRevitPlugins.Shared.UI;
using System.Collections.Generic;

namespace GvcRevitPlugins.TerrainCheck
{
    public class Store : OnPropertyChangedBase
    {
        public int BuildingFaceId { get; set; }
        public int TerrainBoundaryId { get; set; }
        public int SubdivisionLevel { get; set; } = 10;
        public List<XYZ> RailingPoins { get; set; }

        private double _platformElevation;
        public double PlatformElevation
        {
            get { return _platformElevation; }
            set { _platformElevation = value; OnPropertyChanged(); }
        }

        private double _minimumDistance;
        public double MinimumDistance
        {
            get { return _minimumDistance; }
            set { _minimumDistance = value; }
        }

        private double _terrainCheckStrucWallHeight;
        public double TerrainCheckStrucWallHeight
        {
            get { return _terrainCheckStrucWallHeight; }
            set { _terrainCheckStrucWallHeight = value; }
        }

        private double _terrainCheckCalcDistance;
        public double TerrainCheckCalcDistance
        {
            get { return _terrainCheckCalcDistance; }
            set { _terrainCheckCalcDistance = value; OnPropertyChanged(); }
        }

        private double _terrainCheckCalcHeight;
        public double TerrainCheckCalcHeight
        {
            get { return _terrainCheckCalcHeight; }
            set { _terrainCheckCalcHeight = value; OnPropertyChanged(); }
        }
    }
}
