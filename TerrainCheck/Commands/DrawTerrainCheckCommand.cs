using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using GvcRevitPlugins.Shared.App;
using GvcRevitPlugins.Shared.Commands;
using Revit.Async;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GvcRevitPlugins.TerrainCheck.Commands
{
    public class DrawTerrainCheckCommand : AsyncCommandBase, IGvcCommand
    {
        public override async Task ExecuteAsync(object parameter)
        {
            bool dummy = await RevitTask.RunAsync(async app => { return await RevitTask.RaiseGlobal<GvcExternalEventHandler, IGvcCommand, bool>(this); });
        }

        public void MakeAction(object uiApp)
        {
            try
            {
                double? lowestZ = null;

                // Só calcula se a plataforma ainda não tiver sido definida
                if (TerrainCheckApp._thisApp.Store.PlatformElevation == 0)
                {
                    // Caso tenha sido selecionada uma face diretamente
                    if (TerrainCheckApp._thisApp.Store.IntersectionGeometricObject is Mesh faceMesh)
                    {
                        foreach (XYZ vertex in faceMesh.Vertices)
                        {
                            if (lowestZ == null || vertex.Z < lowestZ.Value)
                                lowestZ = vertex.Z;
                        }
                    }
                    else
                    {
                        // Caso contrário, calcula pelas faces do elemento
                        Element element = TerrainCheckApp._thisApp.Store.Element;
                        if (element == null)
                        {
                            TaskDialog.Show("Erro", "Nenhum elemento válido foi encontrado para calcular a plataforma.");
                            return;
                        }

                        Face[] faces = GetElementFaces(element);
                        if (faces == null || faces.Length == 0)
                        {
                            TaskDialog.Show("Erro", $"O elemento {element.Id} não possui faces válidas.");
                            return;
                        }

                        foreach (Face face in faces)
                        {
                            try
                            {
                                Mesh mesh = face.Triangulate();
                                foreach (XYZ vertex in mesh.Vertices)
                                {
                                    if (lowestZ == null || vertex.Z < lowestZ.Value)
                                        lowestZ = vertex.Z;
                                }
                            }
                            catch (Exception ex)
                            {
                                TaskDialog.Show("Aviso", $"Falha ao triangulizar face do elemento {element.Id}: {ex.Message}");
                            }
                        }
                    }

                    if (lowestZ.HasValue)
                    {
                        double lowestZInMeters = UnitUtils.ConvertFromInternalUnits(lowestZ.Value, UnitTypeId.Meters);
                        TerrainCheckApp._thisApp.Store.PlatformElevation = (int)Math.Ceiling(lowestZInMeters);

                        TaskDialog.Show("Sucesso", $"Plataforma definida na cota {TerrainCheckApp._thisApp.Store.PlatformElevation} m.");
                    }
                    else
                    {
                        TerrainCheckApp._thisApp.Store.PlatformElevation = 0;
                        TaskDialog.Show("Aviso", "Não foi possível determinar a cota da plataforma. Valor definido como 0.");
                    }
                }

                // Executa o comando principal
                TerrainCheckCommand.Execute(uiApp as UIApplication, true);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Erro Crítico", $"Ocorreu um erro inesperado: {ex.Message}");
            }
        }

        public Face[] GetElementFaces(Element element)
        {
            if (element == null) return null;

            GeometryElement geomElement = element.get_Geometry(new Options());
            if (geomElement == null) return null;

            List<Face> faces = new();

            foreach (GeometryObject geoObj in geomElement)
            {
                if (geoObj is Solid solid && solid.Faces.Size > 0)
                {
                    faces.AddRange(solid.Faces.Cast<Face>());
                }
                else if (geoObj is Face face)
                {
                    faces.Add(face);
                }
                else if (geoObj is GeometryInstance geoInstance)
                {
                    GeometryElement instanceGeometry = geoInstance.GetInstanceGeometry();
                    foreach (GeometryObject instObj in instanceGeometry)
                    {
                        if (instObj is Solid instSolid && instSolid.Faces.Size > 0)
                        {
                            faces.AddRange(instSolid.Faces.Cast<Face>());
                        }
                        else if (instObj is Face instFace)
                        {
                            faces.Add(instFace);
                        }
                    }
                }
            }

            for (int i = faces.Count - 1; i >= 0; i--)
            {
                Material material = element.Document.GetElement(faces[i].MaterialElementId) as Material;
                if (material == null || !TerrainCheckApp._thisApp.Store.SelectedMaterials.Contains(material.Name))
                {
                    faces.RemoveAt(i);
                }
            }

            return faces.ToArray();
        }
    }
}
