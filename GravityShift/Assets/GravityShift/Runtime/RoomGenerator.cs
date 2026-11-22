using System;
using System.Collections.Generic;

namespace GravityShift
{
    internal struct SeedRandom
    {
        private uint state;
        public SeedRandom(uint seed) => state = seed;

        private double Next()
        {
            unchecked
            {
                uint value = state += 0x6d2b79f5u;
                value = (value ^ (value >> 15)) * (value | 1);
                value ^= value + (value ^ (value >> 7)) * (value | 61);
                return (value ^ (value >> 14)) / 4294967296.0;
            }
        }

        public int Int(int min, int max) => min + (int)(Next() * (max - min + 1));
        public bool Chance(double probability) => Next() < probability;

        public void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Int(0, i);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        public static int FacingSign(uint seed, int roomIndex, int bodyIndex)
        {
            uint mixed = seed ^ ((uint)(roomIndex + 1) * 747796405u + (uint)bodyIndex * 2891336453u);
            return new SeedRandom(mixed).Chance(0.5) ? 1 : -1;
        }
    }

    internal sealed class RoomDefinition
    {
        public readonly string Title;
        public readonly string Subtitle;
        public readonly int Zone;
        public readonly GravityDirection Gravity;
        public readonly float RotateEvery;
        public readonly string[] Map;

        public RoomDefinition(ZoneDefinition zone, int zoneIndex, int index, string subtitle, string[] map)
        {
            Title = $"{zone.Label} {index + 1}/3";
            Subtitle = subtitle;
            Zone = zoneIndex;
            Gravity = zone.Gravity;
            RotateEvery = zone.RotateEvery == 0 ? 0 : Math.Max(4, zone.RotateEvery - index);
            Map = map;
        }
    }

    internal static class RoomGenerator
    {
        private static readonly string[][] Chunks =
        {
            new[] { "......", ".....#", "...##.", ".##..." },
            new[] { "......", ".####.", "......", "......" },
            new[] { "......", "......", "#^^..#", "......" },
        };

        private const int Width = RoomMap.Width;
        private const int Height = RoomMap.Height;
        private const int Floor = Height - 2;
        private const int Spawn = 2;
        private const int Door = Width - 4;

        public static List<RoomDefinition> BuildRun(uint seed)
        {
            var rng = new SeedRandom(seed);
            var rooms = new List<RoomDefinition>(9);
            for (int zoneIndex = 0; zoneIndex < ZoneDefinition.All.Length; zoneIndex++)
            {
                var zone = ZoneDefinition.All[zoneIndex];
                rooms.Add(new RoomDefinition(zone, zoneIndex, 0, zone.IntroSubtitle, zone.IntroMap));
                var subtitles = (string[])zone.Subtitles.Clone();
                rng.Shuffle(subtitles);
                for (int i = 0; i < 2; i++)
                {
                    int difficulty = zoneIndex * 2 + i;
                    int crates = rng.Int(1, 2);
                    bool mirror = rng.Chance(0.5);
                    string[] map = Generate(ref rng, zone, difficulty, crates, mirror);
                    rooms.Add(new RoomDefinition(zone, zoneIndex, i + 1, subtitles[i], map ?? zone.IntroMap));
                }
            }
            return rooms;
        }

        private static string[] Generate(ref SeedRandom rng, ZoneDefinition zone, int difficulty, int crates, bool mirror)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                char[][] grid = Layout(ref rng, difficulty, zone.RotateEvery > 0);
                if (!Reachable(grid, out bool[,] reachable)) continue;
                if (!Populate(ref rng, grid, difficulty, crates, reachable)) continue;
                grid[Floor][Spawn] = 'P';
                if (mirror)
                    foreach (char[] row in grid) Array.Reverse(row);
                if (zone.Gravity == GravityDirection.Up) Array.Reverse(grid);
                return Array.ConvertAll(grid, row => new string(row));
            }
            return null;
        }

        private static char[][] Layout(ref SeedRandom rng, int difficulty, bool wallSpikes)
        {
            var grid = new char[Height][];
            for (int y = 0; y < Height; y++)
            {
                grid[y] = new char[Width];
                for (int x = 0; x < Width; x++)
                    grid[y][x] = x == 0 || y == 0 || x == Width - 1 || y == Height - 1 ? '#' : '.';
            }

            for (int i = rng.Int(0, 2); i > 0; i--)
            {
                int width = rng.Int(2, 5);
                int height = rng.Int(1, 2);
                int start = rng.Int(5, Door - 3 - width);
                for (int y = Floor - height + 1; y <= Floor; y++)
                    for (int x = start; x < start + width; x++) grid[y][x] = '#';
            }

            for (int i = rng.Int(difficulty >= 1 ? 1 : 0, difficulty >= 2 ? 2 : 1); i > 0; i--)
            {
                int width = rng.Int(2, 4);
                int start = rng.Int(5, Door - 3 - width);
                for (int x = start; x < start + width; x++)
                {
                    int y = Floor;
                    while (Solid(grid, x, y)) y--;
                    grid[y][x] = '^';
                }
            }

            for (int row = 13; row >= 7; row -= 3)
            {
                int wanted = rng.Int(1, 3);
                for (int placed = 0, tries = 0; placed < wanted && tries < 25; tries++)
                {
                    int width = rng.Int(3, 7);
                    int start = rng.Int(2, Width - 3 - width);
                    if (row == 13 && (start <= Spawn + 2 || start + width - 1 >= Door - 2)) continue;
                    if (!Empty(grid, row, start - 2, start + width + 1) ||
                        !Empty(grid, row - 1, start - 1, start + width) ||
                        !Empty(grid, row + 1, start - 1, start + width)) continue;
                    for (int x = start; x < start + width; x++) grid[row][x] = '#';
                    placed++;
                }
            }

            for (int i = rng.Int(1, 2); i > 0; i--)
            {
                int width = rng.Int(4, 9);
                int start = rng.Int(2, Width - 3 - width);
                for (int x = start; x < start + width; x++)
                    if (grid[1][x] == '.') grid[1][x] = '^';
            }

            if (wallSpikes)
            {
                foreach (int x in new[] { 1, Width - 2 })
                {
                    if (!rng.Chance(0.7)) continue;
                    int start = rng.Int(3, 7);
                    int height = rng.Int(3, 5);
                    for (int y = start; y < start + height; y++)
                        if (grid[y][x] == '.') grid[y][x] = '^';
                }
            }

            StampChunk(ref rng, grid);
            grid[Floor][Door] = grid[Floor - 1][Door] = 'D';
            return grid;
        }

        private static void StampChunk(ref SeedRandom rng, char[][] grid)
        {
            if (!rng.Chance(0.65)) return;
            string[] chunk = Chunks[rng.Int(0, Chunks.Length - 1)];
            const int height = 4;
            const int width = 6;
            int y0 = rng.Int(3, Floor - height);
            int x0 = rng.Int(3, Width - 3 - width);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int col = x0 + x;
                    int row = y0 + y;
                    if (col == Spawn || col == Door || grid[row][col] != '.') return;
                }
            }
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    grid[y0 + y][x0 + x] = chunk[y][x];
        }

        private static bool Solid(char[][] grid, int x, int y) =>
            x < 0 || y < 0 || x >= Width || y >= Height || grid[y][x] == '#';

        private static bool Blocked(char[][] grid, int x, int y) => Solid(grid, x, y) || grid[y][x] == '^';
        private static bool Standable(char[][] grid, int x, int y) => !Blocked(grid, x, y) && Solid(grid, x, y + 1);

        private static bool Empty(char[][] grid, int y, int left, int right)
        {
            for (int x = left; x <= right; x++)
                if (x < 1 || x > Width - 2 || grid[y][x] != '.') return false;
            return true;
        }

        private static bool ClearLine(char[][] grid, int x, int y, int endX, int endY)
        {
            for (int row = Math.Min(y, endY); row <= Math.Max(y, endY); row++)
                for (int col = Math.Min(x, endX); col <= Math.Max(x, endX); col++)
                    if (Blocked(grid, col, row)) return false;
            return true;
        }

        // The generator checks the original walking, dropping and jumping approximation.
        private static bool Reachable(char[][] grid, out bool[,] reachable)
        {
            var queue = new Queue<(int x, int y)>();
            var seen = new bool[Height, Width];
            reachable = seen;


            void Visit(int x, int y)
            {
                if (seen[y, x]) return;
                seen[y, x] = true;
                queue.Enqueue((x, y));
            }

            Visit(Spawn, Floor);
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();

                for (int step = -1; step <= 1; step += 2)
                {
                    int next = x + step;
                    if (Blocked(grid, next, y)) continue;
                    if (Standable(grid, next, y))
                    {
                        Visit(next, y);
                        continue;
                    }
                    for (int drift = -2; drift <= 2; drift++)
                    {
                        int fallX = next + drift;
                        if (!ClearLine(grid, next, y, fallX, y)) continue;
                        int fallY = y;
                        while (fallY < Floor && !Blocked(grid, fallX, fallY + 1)) fallY++;
                        if (Standable(grid, fallX, fallY)) Visit(fallX, fallY);
                    }
                }

                for (int dy = -3; dy <= 6; dy++)
                {
                    for (int dx = -5; dx <= 5; dx++)
                    {
                        if ((dx == 0 && dy == 0) || (dy < 0 && Math.Abs(dx) > 4)) continue;
                        int tx = x + dx, ty = y + dy;
                        int peak = Math.Min(y, ty) - 1;
                        if (tx < 1 || tx >= Width - 1 || ty > Floor || peak < 1 || !Standable(grid, tx, ty)) continue;
                        if (ClearLine(grid, x, y, x, peak) &&
                            ClearLine(grid, x, peak, tx, peak) &&
                            ClearLine(grid, tx, peak, tx, ty)) Visit(tx, ty);
                    }
                }
            }

            return seen[Floor, Door];
        }

        private static bool Populate(ref SeedRandom rng, char[][] grid, int difficulty, int crates, bool[,] reachable)
        {
            var candidates = new List<(int x, int y)>();
            for (int y = 2; y <= Floor; y++)
                for (int x = Spawn + 8; x < Width - 1; x++)
                    if (grid[y][x] == '.' && Standable(grid, x, y) && reachable[y, x] &&
                        (Math.Abs(x - Door) > 1 || y < Floor - 1)) candidates.Add((x, y));
            rng.Shuffle(candidates);

            var used = new List<(int x, int y)>();
            bool Place(char marker, int minY, int separation)
            {
                foreach (var cell in candidates)
                {
                    if (cell.y < minY ||
                        used.Exists(other => Math.Abs(cell.x - other.x) + Math.Abs(cell.y - other.y) < separation)) continue;
                    used.Add(cell);
                    grid[cell.y][cell.x] = marker;
                    return true;
                }
                return false;
            }

            int anchors = difficulty >= 5 ? 2 : difficulty >= 2 ? 1 : 0;
            for (int i = 0; i < anchors; i++)
                if (!Place('A', Floor - 2, 3)) return false;
            int inverters = difficulty >= 5 ? 2 : difficulty >= 2 ? 1 : 0;
            for (int i = 0; i < inverters; i++) Place('I', 0, 4);
            for (int i = 0; i < 2 + difficulty / 2; i++)
                if (!Place('E', 0, 3)) return false;
            for (int i = 0; i < crates; i++) Place('B', 0, 2);
            return true;
        }
    }
}
