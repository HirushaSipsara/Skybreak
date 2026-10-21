using System;
using NUnit.Framework;
using Skybreak.Disruptor.Core;

namespace Skybreak.Disruptor.Tests
{
    public sealed class GridSearchTests
    {
        /// <summary>Text map: '#' blocked, '!' dangerous (cost +4 per cell), anything else open. Row 0 is the first line.</summary>
        sealed class TextMap : IGridMap
        {
            readonly string[] rows;
            public TextMap(params string[] rows) { this.rows = rows; }
            public int Width => rows[0].Length;
            public int Height => rows.Length;
            public bool Walkable(int x, int z) => x >= 0 && z >= 0 && x < Width && z < Height && rows[z][x] != '#';
            public double Danger(int x, int z) => rows[z][x] == '!' ? 4 : 0;
        }

        [Test]
        public void OpenGridGivesTheOctileOptimum()
        {
            var map = new TextMap("....", "....", "....", "....");
            var path = GridSearch.FindPath(map, new Cell(0, 0), new Cell(3, 3));
            Assert.AreEqual(GridPathStatus.Complete, path.Status);
            Assert.AreEqual(3 * Math.Sqrt(2), path.Cost, 1e-9);
        }

        [Test]
        public void NeverCutsBetweenTouchingBlockedCorners()
        {
            // The only diagonal step from (0,0) to (1,1) squeezes between two blocked cells.
            var map = new TextMap(".#.", "#..", "...");
            var path = GridSearch.FindPath(map, new Cell(0, 0), new Cell(1, 1));
            Assert.AreEqual(GridPathStatus.NoPath, path.Status);
        }

        [Test]
        public void UnreachableGoalReturnsAPartialRouteToTheNearestCell()
        {
            var map = new TextMap("...#.", "...#.", "...#.");
            var path = GridSearch.FindPath(map, new Cell(0, 1), new Cell(4, 1));
            Assert.AreEqual(GridPathStatus.Partial, path.Status);
            var end = path.Cells[path.Cells.Count - 1];
            Assert.AreEqual(2, end.X, "stops at the wall, never walks through it");
        }

        [Test]
        public void DangerBendsTheRouteAroundTheAimCone()
        {
            var map = new TextMap(
                ".......",
                ".!!!!!.",
                ".!!!!!.",
                ".......");
            var path = GridSearch.FindPath(map, new Cell(0, 1), new Cell(6, 2));
            foreach (var c in path.Cells) Assert.AreEqual(0, map.Danger(c.X, c.Z), "route crossed the danger zone at " + c);
            Assert.Greater(path.Length, 6.0, "detour is longer than the straight line");
        }

        [Test]
        public void SmoothingKeepsTheThreatAwareDetour()
        {
            var map = new TextMap(
                ".......",
                ".!!!!!.",
                ".!!!!!.",
                ".......");
            var path = GridSearch.FindPath(map, new Cell(0, 1), new Cell(6, 2));
            var smooth = GridSearch.Smooth(map, path.Cells);
            for (int i = 1; i < smooth.Count; i++)
                Assert.IsTrue(GridSearch.LineClear(map, smooth[i - 1], smooth[i], 0), "smoothed segment enters danger");
            Assert.Less(smooth.Count, path.Cells.Count);
        }

        [Test]
        public void SmoothedSegmentsNeverCrossWalls()
        {
            var map = new TextMap(
                "........",
                "..####..",
                "..#.....",
                "..#.....");
            var path = GridSearch.FindPath(map, new Cell(0, 3), new Cell(7, 3));
            var smooth = GridSearch.Smooth(map, path.Cells);
            for (int i = 1; i < smooth.Count; i++) Assert.IsTrue(GridSearch.LineClear(map, smooth[i - 1], smooth[i]));
            Assert.AreEqual(new Cell(7, 3), smooth[smooth.Count - 1]);
        }

        [Test]
        public void InvalidInputsAreRejected()
        {
            var map = new TextMap("..", "..");
            Assert.AreEqual(GridPathStatus.Invalid, GridSearch.FindPath(map, new Cell(-1, 0), new Cell(1, 1)).Status);
            Assert.AreEqual(GridPathStatus.Invalid, GridSearch.FindPath(null, new Cell(0, 0), new Cell(1, 1)).Status);
            var blockedStart = new TextMap("#.", "..");
            Assert.AreEqual(GridPathStatus.Invalid, GridSearch.FindPath(blockedStart, new Cell(0, 0), new Cell(1, 1)).Status);
        }
    }
}
