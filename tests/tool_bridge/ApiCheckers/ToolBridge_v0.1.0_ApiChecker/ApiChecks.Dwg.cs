using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Topomatic.Cad.Foundation;
using Topomatic.Dwg;
using Topomatic.Dwg.Entities;
using Topomatic.Landscaping;
using Topomatic.ToolBridge;
using Topomatic.ToolBridge.Services;
using Topomatic.Visualization;
using Topomatic.Visualization.Constructions;
using Topomatic.Visualization.Runtime;

namespace ToolBridge_v0_1_0_ApiChecker
{
    internal static partial class ApiChecks
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CheckDwg()
        {
            // A private drawing prevents changes to the active Robur project.
            using (var drawing = new Drawing())
            {
                var font = FontManager.Current.LoadedTTF.FirstOrDefault() ?? FontManager.Current.LoadedSHX.FirstOrDefault();
                Require(font != null, "Для проверки DWG требуется хотя бы один загруженный шрифт Robur.");
                drawing.Styles.Standard.LoadFontFile(font);
                Require(DwgUtils.GetDrawing(null) == null, "GetDrawing without a view");
                var line = new DwgLine { StartPoint = new Vector3D(0, 0, 0), EndPoint = new Vector3D(3, 4, 0) };
                var circle = new DwgCircle { Center = new Vector3D(2, 3, 0), Radius = 4 };
                var polyline = new DwgPolyline { Closed = false };
                polyline.Add(new BugleVector2D(new Vector2D(0, 0)));
                polyline.Add(new BugleVector2D(new Vector2D(2, 3)));
                var text = new DwgText { Style = drawing.Styles.Standard, Content = "Single line", Height = 2 };
                var mtext = new DwgMText { Style = drawing.Styles.Standard, Content = "Multiple lines", Height = 3 };
                var table = new DwgTable(drawing.TableStyles.Standard, 2, 1, 2, 1);
                var hatch = new DwgHatch { PatternName = "SOLID", PatternScale = 1 };
                var block = drawing.Blocks.Add("ApiProbeBlock");
                block.Add(new DwgLine { StartPoint = new Vector3D(0, 0, 0), EndPoint = new Vector3D(1, 1, 0) });
                var insert = new DwgInsert { Block = block, Scale = new Vector3D(1, 1, 1) };
                var plant = new ProbePlant();
                plant.Prepare(drawing);
                plant.PlantElement = CreateSolid("Probe plant", new ImProperties
                {
                    new ImProperty("type", "Type", Enum.GetNames(typeof(Topomatic.Landscaping.LandscapeLibrary.TreeType))[0]),
                    new ImProperty("gost", "Gost", "probe"), new ImProperty("group", "Group", "probe"),
                    new ImProperty("subgroup", "Subgroup", "probe"), new ImProperty("sort", "Sort", "probe")
                });
                var solid = new DwgModel3DElement { Element = CreateSolid("Probe solid", new ImProperties()) };
                var tlc = new DwgModel3DElement { Element = new ConstructedModel3dElement() };
                var entities = new DwgEntity[] { line, circle, polyline, text, mtext, table, hatch, insert, plant, solid, tlc };
                var types = new[] { "dwg_line", "dwg_circle", "dwg_polyline", "dwg_text", "dwg_mtext", "dwg_table",
                    "dwg_hatch", "dwg_block_insert", "landscp_point_plant", "dwg_solid", "tlc_model" };
                foreach (var entity in entities)
                {
                    entity.Prepare(drawing);
                    drawing.ActiveSpace.Add(entity);
                }

                var guid = Guid.NewGuid();
                var guidText = guid.ToString();
                var name = "API probe";
                for (int i = 0; i < entities.Length; i++)
                {
                    var type = DwgUtils.GetEntityType(entities[i]);
                    Require(type.Item1 == types[i] && !string.IsNullOrEmpty(type.Item2), "entity type " + types[i]);
                    CheckEntityResult(DwgUtils.CreateEntityObj(entities[i], guidText, name), types[i], guidText, name);
                }
                Require(DwgUtils.GetEntityType(null).Item1 == "none", "unknown entity type");
                CheckEntityResult(DwgUtils.CreateEntityInfoObj(line, guidText, name), "dwg_line", guidText, name);

                var polylineResult = DwgUtils.CreatePolylineObj(polyline, guidText, name);
                Require(((Array)Property(polylineResult, "points")).Length == 2 && !(bool)Property(polylineResult, "closed"), "polyline vertices");
                var tableResult = DwgUtils.CreateTableObj(table, guidText, name);
                Number(tableResult, "rowCount", table.RowsCount);
                Number(tableResult, "columnCount", table.ColumnsCount);
                Require(Property(tableResult, "cells") is Array, "table cells");
                Require((string)Property(DwgUtils.CreateMTextObj(mtext, guidText, name), "text") == mtext.Content, "multiline text");
                Require((string)Property(DwgUtils.CreateTextObj(text, guidText, name), "text") == text.Content, "single-line text");
                Number(DwgUtils.CreateCircleObj(circle, guidText, name), "radius", 4);
                Number(DwgUtils.CreateLineObj(line, guidText, name), "length", 5);
                Require((string)Property(DwgUtils.CreateHatchObj(hatch, guidText, name), "patternName") == hatch.PatternName, "hatch pattern");
                Require((string)Property(DwgUtils.CreateBlockInsertObj(insert, guidText, name), "blockName") == block.Name, "block reference");
                var plantInfo = Property(DwgUtils.CreatePointPlantObj(plant, "probe-library", guidText, name), "plantElement");
                Require((string)Property(plantInfo, "libUid") == "probe-library" &&
                    (string)Property(plantInfo, "name") == plant.PlantElement.Name, "plant element");
                CheckEntityResult(DwgUtils.CreateSolidObj(solid, guidText, name), "dwg_solid", guidText, name);
                var tlcInfo = DwgUtils.CreateTlcObj(tlc, guidText, name);
                CheckEntityResult(tlcInfo, "tlc_model", guidText, name);
                Require(Property(tlcInfo, "meshBounds") != null, "TLC mesh bounds");
                Expect<ArgumentException>(() => DwgUtils.CreateSolidObj(tlc, guidText, name), "non-solid element");
                Expect<ArgumentException>(() => DwgUtils.CreateTlcObj(solid, guidText, name), "non-TLC element");

                var bounds = new BoundingBox3D(new Vector3D(1, 2, 3), new Vector3D(4, 5, 6));
                var boundsInfo = DwgUtils.CreateBounds3DObj(bounds);
                Number(boundsInfo, "left", bounds.Left); Number(boundsInfo, "right", bounds.Right);
                Number(boundsInfo, "top", bounds.Top); Number(boundsInfo, "bottom", bounds.Bottom);
                Number(boundsInfo, "near", bounds.Near); Number(boundsInfo, "far", bounds.Far);

                var layer = drawing.Layers.Add("ApiProbeLayer");
                DwgUtils.ApplyEntityLayer(drawing, line, layer.Name);
                Require(ReferenceEquals(line.Layer, layer), "ApplyEntityLayer");
                DwgUtils.ApplyEntityLayer(drawing, line, null);
                Require(ReferenceEquals(line.Layer, layer), "null layer keeps value");
                Expect<InvalidOperationException>(() => DwgUtils.ApplyEntityLayer(drawing, line, ""), "empty layer");
                Expect<InvalidOperationException>(() => DwgUtils.ApplyEntityLayer(drawing, line, "missing-layer"), "unknown layer");
                DwgUtils.ApplyEntityColor(line, "ByLayer", null);
                Require(DwgUtils.GetColorMode(line.Color) == "ByLayer", "ByLayer color");
                DwgUtils.ApplyEntityColor(line, "byblock", null);
                Require(DwgUtils.GetColorMode(line.Color) == "ByBlock", "ByBlock color");
                DwgUtils.ApplyEntityColor(line, "Indexed", 3);
                Require(DwgUtils.GetColorMode(line.Color) == "Indexed" && line.Color.ColorIndex == 3, "indexed color");
                DwgUtils.ApplyEntityColor(line, null, null);
                Require(line.Color.ColorIndex == 3, "null color keeps value");
                Expect<InvalidOperationException>(() => DwgUtils.ApplyEntityColor(line, "Indexed", null), "missing color index");
                Expect<InvalidOperationException>(() => DwgUtils.ApplyEntityColor(line, "Indexed", -1), "negative color index");
                Expect<InvalidOperationException>(() => DwgUtils.ApplyEntityColor(line, "invalid", null), "unknown color mode");

                foreach (AcPatternType value in Enum.GetValues(typeof(AcPatternType)))
                    Require(DwgUtils.ParsePatternType(value.ToString().ToLowerInvariant()) == value, "pattern type parser");
                foreach (AcHatchStyle value in Enum.GetValues(typeof(AcHatchStyle)))
                    Require(DwgUtils.ParseHatchStyle(value.ToString().ToLowerInvariant()) == value, "hatch style parser");
                foreach (TextAlignment value in Enum.GetValues(typeof(TextAlignment)))
                    Require(DwgUtils.ParseTextAlignment(value.ToString().ToLowerInvariant()) == value, "text alignment parser");
                foreach (AttachmentPoint value in Enum.GetValues(typeof(AttachmentPoint)))
                    Require(DwgUtils.ParseAttachmentPoint(value.ToString().ToLowerInvariant()) == value, "attachment parser");
                Expect<InvalidOperationException>(() => DwgUtils.ParsePatternType("invalid"), "invalid pattern type");
                Expect<InvalidOperationException>(() => DwgUtils.ParseHatchStyle("invalid"), "invalid hatch style");
                Expect<InvalidOperationException>(() => DwgUtils.ParseTextAlignment("invalid"), "invalid alignment");
                Expect<InvalidOperationException>(() => DwgUtils.ParseAttachmentPoint("invalid"), "invalid attachment");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CheckDwgStorageIntegration()
        {
            using (var drawing = new Drawing())
            {
                var line = new DwgLine { StartPoint = new Vector3D(0, 0, 0), EndPoint = new Vector3D(3, 4, 0) };
                line.Prepare(drawing);
                drawing.ActiveSpace.Add(line);
                var guid = Guid.NewGuid();
                var guidText = guid.ToString();
                var name = "API lookup probe";
                var storage = new ObjectStorage();
                line.CreateExtensionDictionary();
                line.GetExtensionDictionary().SetString("guid", guidText);
                line.GetExtensionDictionary().SetString("name", name);
                var found = DwgUtils.FindEntity<DwgLine>(drawing, storage, guid);
                Require(ReferenceEquals(found.Item1, line) && found.Item2 == name, "FindEntity scan");
                found = DwgUtils.FindEntity<DwgLine>(drawing, storage, guid);
                Require(ReferenceEquals(found.Item1, line) && found.Item2 == name, "FindEntity cache");
                Require(DwgUtils.FindEntity<DwgLine>(drawing, storage, Guid.NewGuid()).Item1 == null, "FindEntity missing GUID");
                using (var otherDrawing = new Drawing())
                    Expect<InvalidOperationException>(() => DwgUtils.FindEntity<DwgLine>(otherDrawing, storage, guid), "entity from another drawing");

            }
        }

        private static void CheckEntityResult(object result, string type, string guid, string name)
        {
            Require((string)Property(result, "guid") == guid && (string)Property(result, "name") == name &&
                (string)Property(result, "type") == type, "entity result " + type);
        }

        // The serializer only needs the plant's public data. An empty representation
        // avoids depending on external landscaping library drawings in this fixture.
        private sealed class ProbePlant : DwgSmdxPointLandscaping
        {
            protected override void OnRegen(EventArgs e)
            {
            }
        }
    }
}
