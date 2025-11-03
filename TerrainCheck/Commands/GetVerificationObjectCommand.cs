using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using GvcRevitPlugins.Shared.App;
using GvcRevitPlugins.Shared.Commands;
using Revit.Async;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using utils = GvcRevitPlugins.Shared.Utils;

namespace GvcRevitPlugins.TerrainCheck.Commands
{
    public class GetVerificationObjectCommand : AsyncCommandBase, IGvcCommand
    {
        public override async Task ExecuteAsync(object parameter)
        {
            bool dummy = await RevitTask.RunAsync(async app => 
            {
                return await RevitTask.RaiseGlobal<GvcExternalEventHandler, IGvcCommand, bool>(this); 
            });
        }

        public void MakeAction(object uiAppObj)
        {
            if (TerrainCheckApp._thisApp.Store.ObjectSelectionType == "")
            {
                TaskDialog.Show("Aviso", "Selecione um tipo de seleção para prosseguir.");
                return;
            }

            try
            {
                var uiApp = uiAppObj as UIApplication;
                if (uiApp == null)
                {
                    TaskDialog.Show("Erro", "Aplicação inválida.");
                    return;
                }

                var uidoc = uiApp.ActiveUIDocument;
                var doc = uidoc?.Document;
                if (doc == null)
                {
                    TaskDialog.Show("Erro", "Nenhum documento ativo encontrado.");
                    return;
                }

                string selectionType = TerrainCheckApp._thisApp.Store.ObjectSelectionType;
                ObjectType objectType = selectionType switch
                {
                    "Face" => ObjectType.Face,
                    _ => ObjectType.Element
                };

                TaskDialog.Show("Info", $"Selecione o alvo de verificação ({selectionType})");

                Reference reference = null;
                try
                {
                    reference = uidoc.Selection.PickObject(objectType, $"Selecione o alvo de verificação ({selectionType})");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    TaskDialog.Show("Cancelado", "Seleção cancelada pelo usuário.");
                    return;
                }

                if (reference == null)
                {
                    TaskDialog.Show("Aviso", "Nenhum objeto foi selecionado.");
                    return;
                }

                // Clear previous selection data
                TerrainCheckApp._thisApp.Store.IntersectionGeometricObject = null;
                TerrainCheckApp._thisApp.Store.Elementmaterials = null;

                // Set the element 
                Element element = doc.GetElement(reference.ElementId);
                if (element == null)
                {
                    TaskDialog.Show("Erro", "Elemento inválido selecionado.");
                    return;
                }

                TerrainCheckApp._thisApp.Store.IntersectionElementId = reference.ElementId;
                TerrainCheckApp._thisApp.Store.Element = element;

                // Set the material list
                try
                {
                    List<Material> elementMaterials = utils.ElementUtils.GetElementMaterials(doc, element).ToList();
                    TerrainCheckApp._thisApp.Store.Elementmaterials = elementMaterials
                        .Where(m => m != null)
                        .Select(m => m.Name)
                        .Distinct()
                        .ToList();
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Aviso", $"Falha ao obter materiais do elemento: {ex.Message}");
                }

                // Caso seja Face, trata separadamente
                if (objectType != ObjectType.Element)
                {
                    GeometryObject selectedFace = element?.GetGeometryObjectFromReference(reference);
                    if (selectedFace is not Face face)
                    {
                        TaskDialog.Show("Erro", "A referência selecionada não é uma face válida.");
                        return;
                    }

                    LocationPoint location = element?.Location as LocationPoint;
                    Mesh faceMesh;

                    try
                    {
                        if (location == null)
                        {
                            faceMesh = face.Triangulate();
                        }
                        else
                        {
                            Transform translation = Transform.CreateTranslation(location.Point ?? XYZ.Zero);
                            Transform rotation = Transform.CreateRotation(XYZ.BasisZ, location.Rotation);

                            faceMesh = face.Triangulate()
                                           .get_Transformed(rotation)
                                           .get_Transformed(translation);
                        }

                        TerrainCheckApp._thisApp.Store.IntersectionGeometricObject = faceMesh;
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Erro", $"Falha ao processar a face selecionada: {ex.Message}");
                        return;
                    }
                }

                // Sucesso final
                TaskDialog.Show("Sucesso", $"Objeto ({TerrainCheckApp._thisApp.Store.Element.Name}) configurado para verificação.");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Erro Crítico", $"Ocorreu um erro inesperado: {ex.Message}");
            }
        }
    }
} 