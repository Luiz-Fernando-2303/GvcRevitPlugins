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
            double? lowestZ = null;

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
                Element element = TerrainCheckApp._thisApp.Store.Element;
                Face[] faces = GetElementFaces(element);

                if (faces != null && faces.Length > 0)
                {
                    foreach (Face face in faces)
                    {
                        Mesh mesh = face.Triangulate();
                        foreach (XYZ vertex in mesh.Vertices) 
                        {
                            if (lowestZ == null || vertex.Z < lowestZ.Value)
                                lowestZ = vertex.Z;
                        }
                    }
                }
            }

            if (lowestZ.HasValue)
            {
                double lowestZInMeters = UnitUtils.ConvertFromInternalUnits(lowestZ.Value, UnitTypeId.Meters);
                TerrainCheckApp._thisApp.Store.PlatformElevation = (int)Math.Ceiling(lowestZInMeters);
            }
            else
            {
                TerrainCheckApp._thisApp.Store.PlatformElevation = 0;
            }

            // demarcar objetos ao gerar (ok)
            // loading para indicar trabalho (ok)
            // reduzir tamanho minimo (ok)
            TerrainCheckCommand.Execute(uiApp as UIApplication, true);
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
