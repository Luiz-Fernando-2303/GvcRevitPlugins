using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Numerics;

using utils = GvcRevitPlugins.Shared.Utils;

namespace GvcRevitPlugins.TerrainCheck.Rules
{
    public class SlopeCheckRule : ITerrainCheckRule
    {
        public bool IsActive { get; set; } = true;
        public string Name => "Slope";
        public string Description => "Checks the slope of the terrain";
        public Color ColorRGB => new Color(255, 0, 0);
        public int WallTypeId { get; set; }
        public string WallTypeName { get; set; } = "Resultado Talude Corte";
        public string ResultFamilyName { get; } = "Linha de Afastamento Mínimo.rfa";

        public Action<UIDocument, XYZ[], XYZ, XYZ[], double, bool, Level> Execute => (uidoc, startPoints, normal, boundaryPoints, baseElevation, draw, level) =>
        {
            double wallHeight = GetConfiguredWallHeight();
            WallType wallType = Shared.Utils.RevitUtils.GetOrCreateWallType(uidoc, WallTypeName, BuiltInCategory.OST_Walls, ColorRGB);

            if (wallType == null)
            {
                TaskDialog.Show("Erro", "Tipo de parede não encontrado.");
                return;
            }

            double minimumDistance = GetMinimumDistance(wallHeight);
            BuildSlopedWallLines(uidoc, wallType, level, startPoints, boundaryPoints, baseElevation, normal, minimumDistance, draw);
        };

        /// <summary>
        /// Obtém a altura configurada para o muro de contenção (em unidades internas).
        /// </summary>
        private double GetConfiguredWallHeight()
        {
            return UnitUtils.ConvertToInternalUnits(TerrainCheckApp._thisApp.Store.TerrainCheckStrucWallHeight, UnitTypeId.Meters);
        }

        /// <summary>
        /// Define a distância mínima com base na altura da parede e configuração do usuário.
        /// </summary>
        private double GetMinimumDistance(double wallHeight)
        {
            double minDist = TerrainCheckApp._thisApp.Store.MinimumDistance;
            minDist = minDist > 2 ? minDist : 2;
            minDist = UnitUtils.ConvertToInternalUnits(minDist, UnitTypeId.Meters);

            if (wallHeight > minDist)
            {
                // Garante um afastamento mínimo coerente com a altura da contenção
                minDist = wallHeight - UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Meters);
            }

            return minDist;
        }

        /// <summary>
        /// Calcula as linhas de muro baseadas na inclinação entre pontos e cria paredes se necessário.
        /// </summary>
        private void BuildSlopedWallLines(UIDocument uidoc, WallType wallType, Level level, XYZ[] startPoints, XYZ[] boundaryPoints, double baseElevation, XYZ normal, double minDistance, bool draw)
        {
            List<Curve> wallCurves = new();
            List<XYZ> validStarts = new();
            List<XYZ> validBoundaries = new();
            List<XYZ> endPoints = new();

            int count = Math.Min(startPoints?.Length ?? 0, boundaryPoints?.Length ?? 0);

            for (int i = 0; i < count; i++)
            {
                var start = startPoints[i];
                var boundary = boundaryPoints[i];
                if (start == null || boundary == null) continue;

                validStarts.Add(start);
                validBoundaries.Add(boundary);

                // Calcula o deslocamento com base na diferença de altura
                double offset = (boundary.Z - baseElevation) / 2;
                offset = offset < minDistance ? minDistance : offset;

                var end = Shared.Utils.XYZUtils.GetEndPoint(start, normal, offset);
                endPoints.Add(end);

                try
                {
                    // Cria linha entre o ponto atual e o anterior válido
                    if (end != null && endPoints.Count > 1 && endPoints[^2] != null)
                    {
                        wallCurves.Add(Line.CreateBound(endPoints[^2], end));
                    }
                } catch
                {
                    continue;
                }
            }

            StoreWorstCase(baseElevation, validStarts.ToArray(), validBoundaries.ToArray());

            if (draw)
            {
                foreach (var curve in wallCurves)
                {
                    Wall.Create(uidoc.Document, curve, wallType.Id, level.Id, minDistance, 0.0, false, false);
                }
            }
        }

        /// <summary>
        /// Identifica a pior situação de talude com base na maior diferença de altura e armazena para exibição.
        /// </summary>
        private void StoreWorstCase(double baseElevation, XYZ[] facePoints, XYZ[] boundaryPoints)
        {
            int worstIndex = -1;
            double maxHeightDiff = 0.0;
            double worstDistance = 0.0;

            for (int i = 0; i < boundaryPoints.Length; i++)
            {
                if (boundaryPoints[i] == null) continue;

                double heightDiff = boundaryPoints[i].Z - baseElevation;

                var p1 = new Vector2((float)facePoints[i].X, (float)facePoints[i].Y);
                var p2 = new Vector2((float)boundaryPoints[i].X, (float)boundaryPoints[i].Y);
                double distance = Vector2.Distance(p1, p2);

                if (heightDiff > maxHeightDiff || worstIndex < 0)
                {
                    worstIndex = i;
                    maxHeightDiff = heightDiff;
                    worstDistance = distance;
                }
            }

            // Salva valores para interface ou relatório
            TerrainCheckApp._thisApp.Store.TerrainCheckCalcDistance = Math.Round(UnitUtils.ConvertFromInternalUnits(worstDistance, UnitTypeId.Meters), 1);
            TerrainCheckApp._thisApp.Store.TerrainCheckCalcHeight = Math.Round(UnitUtils.ConvertFromInternalUnits(maxHeightDiff, UnitTypeId.Meters), 1);
        }
    }
}
