using Autodesk.Revit.DB;
using GvcRevitPlugins.Shared.UI;
using System.Collections.Generic;

namespace GvcRevitPlugins.TerrainCheck
{
    public class Store : OnPropertyChangedBase
    {
        public ElementId IntersectionElementId { get; set; }
        public GeometryObject IntersectionGeometricObject { get; set; }
        public Transform ElementTransform { get; set; }
        public Element Element { get; set; }
        public SelectionToLines selection { get; set; }
        private List<string> _allowedMaterials = new List<string>();
        public List<string> Elementmaterials
        {
            get => _allowedMaterials;
            set
            {
                _allowedMaterials = value;
                OnPropertyChanged();
            }
        }

        public List<string> SelectedMaterials { get; set; }
        public List<ElementId> TerrainBoundaryIds { get; set; }
        public int SubdivisionLevel { get; set; } = 10;

        private double _platformElevation;
        public double PlatformElevation
        {
            get => _platformElevation;
            set { _platformElevation = value; OnPropertyChanged(); }
        }

        private double _minimumDistance;
        public double MinimumDistance
        {
            get => _minimumDistance;
            set { _minimumDistance = value; OnPropertyChanged(); }
        }

        private double _terrainCheckStrucWallHeight;
        public double TerrainCheckStrucWallHeight
        {
            get => _terrainCheckStrucWallHeight;
            set { _terrainCheckStrucWallHeight = value; OnPropertyChanged(); }
        }

        private double _terrainCheckCalcDistance;
        public double TerrainCheckCalcDistance
        {
            get => _terrainCheckCalcDistance;
            set { _terrainCheckCalcDistance = value; OnPropertyChanged(); }
        }

        private double _terrainCheckCalcHeight;
        public double TerrainCheckCalcHeight
        {
            get => _terrainCheckCalcHeight;
            set { _terrainCheckCalcHeight = value; OnPropertyChanged(); }
        }

        private string _boundarySelectionType = "Linha de Divisa";
        public string BoundarySelectionType
        {
            get => _boundarySelectionType;
            set
            {
                if (_boundarySelectionType != value)
                {
                    _boundarySelectionType = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _objectSelectionType = "Elemento";
        public string ObjectSelectionType
        {
            get => _objectSelectionType;
            set
            {
                if (_objectSelectionType != value)
                {
                    _objectSelectionType = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
