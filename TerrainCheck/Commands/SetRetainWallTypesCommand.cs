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

namespace GvcRevitPlugins.TerrainCheck.Commands
{
    public class SetRetainWallTypesCommand : AsyncCommandBase, IGvcCommand
    {
        public override async Task ExecuteAsync(object parameter)
        {
            bool dummy = await RevitTask.RunAsync(async app => { return await RevitTask.RaiseGlobal<GvcExternalEventHandler, IGvcCommand, bool>(this); });
        }
        public void MakeAction(object uiAppObj)
        {
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

                var selection = uidoc.Selection;

                List<Reference> pickedRef;
                try
                {
                    pickedRef = selection.PickObjects(ObjectType.Element, "Selecione os arrimos").ToList();
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    TaskDialog.Show("Cancelado", "Seleção de elementos cancelada pelo usuário.");
                    return;
                }

                if (pickedRef == null || pickedRef.Count == 0)
                {
                    TaskDialog.Show("Aviso", "Nenhum elemento foi selecionado.");
                    return;
                }

                List<Element> elements = pickedRef
                    .Select(r => doc.GetElement(r.ElementId))
                    .Where(e => e != null)
                    .ToList();

                if (elements.Count == 0)
                {
                    TaskDialog.Show("Aviso", "Não foi possível carregar nenhum elemento válido da seleção.");
                    return;
                }

                List<Material> materials = new();

                foreach (Element element in elements)
                {
                    try
                    {
                        var elementMaterials = GvcRevitPlugins.Shared.Utils.ElementUtils
                            .GetElementMaterials(doc, element)
                            .Where(m => m != null)
                            .ToList();

                        if (elementMaterials.Count > 0)
                            materials.AddRange(elementMaterials);
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Aviso", $"Falha ao obter materiais do elemento {element.Id}: {ex.Message}");
                    }
                }

                materials = materials
                    .Where(m => m != null)
                    .DistinctBy(m => m.Name)
                    .ToList();

                if (materials.Count == 0)
                {
                    TaskDialog.Show("Erro", "Nenhum material encontrado nos arrimos selecionados.");
                    return;
                }

                TerrainCheckApp._thisApp.Store.selectedRetainWalls = elements;
                TerrainCheckApp._thisApp.Store.retainWallsMaterials = materials;

                TaskDialog.Show("Sucesso", $"{elements.Count} arrimos selecionados e {materials.Count} materiais encontrados.");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Erro Crítico", $"Ocorreu um erro inesperado: {ex.Message}");
            }
        }
    }
}
