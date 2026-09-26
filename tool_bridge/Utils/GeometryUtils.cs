using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Topomatic.Cad.Foundation;
using Topomatic.Visualization.Geometry;

namespace Topomatic.ToolBridge.Utils
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public static class GeometryUtils
    {
        private const double SectionTolerance = 1e-6;

        /// <summary>
        /// Строит сечение в системе координат модели с допуском 1e-6.
        /// Замкнутые контуры повторяют первую точку в конце. Открытые сетки могут
        /// давать открытые цепочки. Точки касания не возвращаются. В узлах ветвления
        /// цепочки разделяются. Направление обхода не задается.
        /// </summary>
        public static List<SectionContour> CreateSection(this GeometryModel3D geometryModel, Plane plane)
        {
            if (geometryModel == null)
                throw new ArgumentNullException(nameof(geometryModel));

            var normalLength = plane.Normal.Length;
            if (!IsFinite(normalLength) || normalLength == 0 || !IsFinite(plane.D))
                throw new ArgumentException("Плоскость должна иметь конечную ненулевую нормаль и конечный D.", nameof(plane));

            plane.Normalize();

            var builder = new SectionBuilder();
            foreach (var mesh in geometryModel.Meshes.Values)
            {
                var points = new Vector3D[mesh.Positions.Count];
                var distances = new double[points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    // Matrix — текущее преобразование сетки, как в GeometryModel3D.Append.
                    points[i] = Vector3D.Transform(mesh.Positions[i].Vector, mesh.Matrix);

                    if (!IsFinite(points[i].X) || !IsFinite(points[i].Y) || !IsFinite(points[i].Z))
                        throw new ArgumentException("Модель содержит неконечные координаты.", nameof(geometryModel));

                    var distance = plane.DotCoordinate(points[i]);
                    distances[i] = Math.Abs(distance) <= SectionTolerance ? 0 : distance;
                }

                foreach (var face in mesh.TriangleIndices)
                {
                    if (face.A >= points.Length || face.B >= points.Length || face.C >= points.Length)
                        throw new ArgumentException("Индекс вершины треугольника выходит за границы сетки.", nameof(geometryModel));

                    var a = points[face.A];
                    var b = points[face.B];
                    var c = points[face.C];

                    var longestEdge = Math.Max(
                        Vector3D.Distance(a, b),
                        Math.Max(Vector3D.Distance(b, c), Vector3D.Distance(c, a)));

                    if (longestEdge <= SectionTolerance || Vector3D.Cross(b - a, c - a).Length <= SectionTolerance * longestEdge)
                        continue;

                    var da = distances[face.A];
                    var db = distances[face.B];
                    var dc = distances[face.C];

                    if (da == 0 && db == 0 && dc == 0)
                    {
                        builder.AddSegment(plane.Project(a), plane.Project(b), true);
                        builder.AddSegment(plane.Project(b), plane.Project(c), true);
                        builder.AddSegment(plane.Project(c), plane.Project(a), true);
                        continue;
                    }

                    if ((da > 0 && db > 0 && dc > 0) || (da < 0 && db < 0 && dc < 0))
                        continue;

                    var intersections = new List<Vector3D>(3);
                    AddIntersection(intersections, a, b, da, db, plane);
                    AddIntersection(intersections, b, c, db, dc, plane);
                    AddIntersection(intersections, c, a, dc, da, plane);

                    if (intersections.Count == 2)
                        builder.AddSegment(intersections[0], intersections[1], false);
                }
            }
            return builder.BuildContours();
        }

        public static object CreateSectionContourObj(SectionContour contour)
        {
            if (contour == null)
                throw new ArgumentNullException(nameof(contour));

            return new
            {
                vertices = contour.ContourPoints.Select(point => new
                {
                    x = point.X,
                    y = point.Y,
                    z = point.Z
                }).ToArray()
            };
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static void AddIntersection(List<Vector3D> points, Vector3D a, Vector3D b, double da, double db, Plane plane)
        {
            Vector3D point;
            if (da == 0)
                point = plane.Project(a);
            else if ((da < 0 && db > 0) || (da > 0 && db < 0))
                point = plane.Intersect(new Line3D { Position = a, Direction = b - a });
            else
                return;

            foreach (var existing in points)
            {
                if (Vector3D.DistanceSquared(existing, point) <= SectionTolerance * SectionTolerance)
                    return;
            }

            points.Add(point);
        }

        private sealed class SectionEdge
        {
            public int A { get; set; }
            public int B { get; set; }
            public int CoplanarCount { get; set; }
            public bool CrossesPlane { get; set; }
            public bool Used { get; set; }
        }

        private sealed class SectionBuilder
        {
            private readonly List<Vector3D> m_Points;
            private readonly Dictionary<(double, double, double), List<int>> m_Cells;
            private readonly Dictionary<(int, int), SectionEdge> m_Edges;

            public SectionBuilder()
            {
                m_Points = new List<Vector3D>();
                m_Cells = new Dictionary<(double, double, double), List<int>>();
                m_Edges = new Dictionary<(int, int), SectionEdge>();
            }

            private int GetPointIndex(Vector3D point)
            {
                var x = Math.Floor(point.X / SectionTolerance);
                var y = Math.Floor(point.Y / SectionTolerance);
                var z = Math.Floor(point.Z / SectionTolerance);

                // Проверяем соседние ячейки, чтобы склеивать точки у границ ячеек.
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (!m_Cells.TryGetValue((x + dx, y + dy, z + dz), out var candidates))
                                continue;

                            foreach (int index in candidates)
                            {
                                if (Vector3D.DistanceSquared(m_Points[index], point) <= SectionTolerance * SectionTolerance)
                                    return index;
                            }
                        }
                    }
                }

                var key = (x, y, z);
                if (!m_Cells.TryGetValue(key, out var cell))
                {
                    cell = new List<int>();
                    m_Cells.Add(key, cell);
                }

                var result = m_Points.Count;
                m_Points.Add(point);
                cell.Add(result);
                return result;
            }

            public void AddSegment(Vector3D a, Vector3D b, bool coplanar)
            {
                var first = GetPointIndex(a);
                var second = GetPointIndex(b);
                if (first == second)
                    return;

                var key = (Math.Min(first, second), Math.Max(first, second));
                if (!m_Edges.TryGetValue(key, out var edge))
                {
                    edge = new SectionEdge { A = key.Item1, B = key.Item2 };
                    m_Edges.Add(key, edge);
                }

                if (coplanar)
                    edge.CoplanarCount++;
                else
                    edge.CrossesPlane = true;
            }

            public List<SectionContour> BuildContours()
            {
                var adjacency = new List<SectionEdge>[m_Points.Count];
                for (int i = 0; i < adjacency.Length; i++)
                {
                    adjacency[i] = new List<SectionEdge>();
                }

                foreach (var edge in m_Edges.Values)
                {
                    // Внутренние ребра лежащих в плоскости треугольников не нужны.
                    if (!edge.CrossesPlane && edge.CoplanarCount != 1)
                        continue;

                    adjacency[edge.A].Add(edge);
                    adjacency[edge.B].Add(edge);
                }

                var result = new List<SectionContour>();
                // Сначала открытые цепочки и ветвления, затем оставшиеся циклы.
                for (int i = 0; i < adjacency.Length; i++)
                {
                    if (adjacency[i].Count != 2)
                    {
                        foreach (var edge in adjacency[i])
                        {
                            if (!edge.Used)
                                result.Add(Trace(i, edge, adjacency));
                        }
                    }
                }

                for (int i = 0; i < adjacency.Length; i++)
                {
                    foreach (var edge in adjacency[i])
                    {
                        if (!edge.Used)
                            result.Add(Trace(i, edge, adjacency));
                    }
                }
                return result;
            }

            private SectionContour Trace(int start, SectionEdge edge, List<SectionEdge>[] adjacency)
            {
                var contour = new SectionContour();
                contour.ContourPoints.Add(m_Points[start]);
                var current = start;
                while (!edge.Used)
                {
                    edge.Used = true;
                    current = edge.A == current ? edge.B : edge.A;
                    contour.ContourPoints.Add(m_Points[current]);

                    if (current == start || adjacency[current].Count != 2)
                        break;

                    var next = adjacency[current];
                    edge = next[0].Used ? next[1] : next[0];
                }
                return contour;
            }
        }
    }

    public sealed class SectionContour
    {
        private readonly List<Vector3D> m_ContourPoints;

        public SectionContour()
        {
            m_ContourPoints = new List<Vector3D>();
        }

        public List<Vector3D> ContourPoints
        {
            get
            {
                return m_ContourPoints;
            }
        }
    }
}
