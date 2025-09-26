using Autodesk.Revit.DB;
using GvcRevitPlugins.Shared.UI;
using System.Collections.Generic;

namespace GvcRevitPlugins.TerrainCheck
{
    public class Store : OnPropertyChangedBase
    {
        // talude
        public List<Element> selectedRetainWalls { get; set; }
        public List<Material> retainWallsMaterials { get; set; }

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
        private double _subdivisionLevel = 30; // valor inicial em cm
        public double SubdivisionLevel
        {
            get => _subdivisionLevel;
            set
            {
                if (_subdivisionLevel != value)
                {
                    _subdivisionLevel = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _platformElevation = 0;
        public double PlatformElevation
        {
            get => _platformElevation;
            set { _platformElevation = value; OnPropertyChanged(); }
        }

        private double _minimumDistance;
        public double MinimumDistance //TODO: auto calculado 
        {
            get => _minimumDistance;
            set { _minimumDistance = value; OnPropertyChanged(); }
        }

        private double _terrainCheckStrucWallHeight;
        public double TerrainCheckStrucWallHeight
        {
            get => _terrainCheckStrucWallHeight;
            set { _terrainCheckStrucWallHeight = value; OnPropertyChanged(); } //TODO: auto calculado 
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
        // objeto <-> elemento
        // tirar isolamento
        // pegar maior desnivel
        // 

        private string Version { get; set; } = "1.3.3";
        public string Version_
        {
            get => Version;
        }

        public void Reset()
        {
            // Resetar listas
            selectedRetainWalls = new List<Element>();
            retainWallsMaterials = new List<Material>();
            SelectedMaterials = new List<string>();
            TerrainBoundaryIds = new List<ElementId>();
            Elementmaterials = new List<string>();

            // Resetar objetos
            IntersectionElementId = null;
            IntersectionGeometricObject = null;
            ElementTransform = null;
            Element = null;
            selection = null;

            // Resetar valores numéricos
            SubdivisionLevel = 30; // valor inicial
            PlatformElevation = 0;
            MinimumDistance = 0;
            TerrainCheckStrucWallHeight = 0;
            TerrainCheckCalcDistance = 0;
            TerrainCheckCalcHeight = 0;

            // Resetar strings
            BoundarySelectionType = "Linha de Divisa";
            ObjectSelectionType = "Elemento";
        }
    }
}
