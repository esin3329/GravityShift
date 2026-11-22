using System.Collections.Generic;
using UnityEngine;

namespace GravityShift
{
    internal enum GravityDirection { Down, Left, Up, Right }

    internal static class GravityDirections
    {
        private static readonly Vector2[] Vectors = { Vector2.down, Vector2.left, Vector2.up, Vector2.right };
        public static Vector2 Vector(this GravityDirection direction) => Vectors[(int)direction];
        public static GravityDirection Turn(this GravityDirection direction, int steps) =>
            (GravityDirection)(((int)direction + steps + 4) % 4);
    }

    internal sealed class RoomMap
    {
        public const int Width = 30;
        public const int Height = 17;
        private const float Edge = 0.00001f;

        private readonly char[,] tiles = new char[Height, Width];
        public Vector2 PlayerStart { get; private set; } = new Vector2(2, -2);
        public readonly List<Vector2> Walkers = new List<Vector2>();
        public readonly List<Vector2> Anchors = new List<Vector2>();
        public readonly List<Vector2> Crates = new List<Vector2>();

        public RoomMap(string[] rows)
        {
            for (int row = 0; row < Height; row++)
            {
                for (int col = 0; col < Width; col++)
                {
                    bool border = row == 0 || col == 0 || row == Height - 1 || col == Width - 1;
                    char tile = border ? '#' : row < rows.Length && col < rows[row].Length ? rows[row][col] : '.';
                    Vector2 center = CellCenter(col, row);
                    switch (tile)
                    {
                        case 'P': PlayerStart = center; break;
                        case 'E': Walkers.Add(center); break;
                        case 'A': Anchors.Add(center); break;
                        case 'B': Crates.Add(center); break;
                        default: tiles[row, col] = tile; continue;
                    }
                    tiles[row, col] = '.';
                }
            }
        }

        public static Vector2 CellCenter(int col, int row) => new Vector2(col + 0.5f, -row - 0.5f);
        public static Rect CellRect(int col, int row) => new Rect(col, -row - 1, 1, 1);
        public char At(int col, int row) => col < 0 || col >= Width || row < 0 || row >= Height ? '#' : tiles[row, col];
        public char At(Vector2 point) => At(Mathf.FloorToInt(point.x), Mathf.FloorToInt(-point.y));
        public bool IsSolid(int col, int row) => At(col, row) == '#';

        public bool ContainsTile(Rect rect, char tile, float inset)
        {
            rect = Rect.MinMaxRect(rect.xMin + inset, rect.yMin + inset, rect.xMax - inset, rect.yMax - inset);
            var cells = OverlappingCells(rect);
            for (int row = cells.yMin; row < cells.yMax; row++)
                for (int col = cells.xMin; col < cells.xMax; col++)
                    if (At(col, row) == tile) return true;
            return false;
        }

        public static RectInt OverlappingCells(Rect rect)
        {
            int left = Mathf.FloorToInt(rect.xMin + Edge);
            int right = Mathf.FloorToInt(rect.xMax - Edge);
            int top = Mathf.FloorToInt(-rect.yMax + Edge);
            int bottom = Mathf.FloorToInt(-rect.yMin - Edge);
            return new RectInt(left, top, right - left + 1, bottom - top + 1);
        }
    }

    internal static class Tuning
    {
        public const float Pixel = 1f / 32f;
        public const float Gravity = 1800 * Pixel;
        public const float MaxFall = 900 * Pixel;
        public const float WallImpact = 480 * Pixel;
        public const float BodyImpact = 380 * Pixel;
        public const float ImpactStep = 120 * Pixel;

        public const float MoveSpeed = 250 * Pixel;
        public const float JumpSpeed = 640 * Pixel;
        public const float JumpCut = 260 * Pixel;
        public const float DashSpeed = 640 * Pixel;
        public const float AttackRange = 34 * Pixel;
        public const int MaxHealth = 5;

        public const float MaxMana = 100;
        public const float FlipCost = 20;
        public const float FlipDuration = 5;
        public const int MaxZones = 3;
        public const int BeamWidth = 3;
    }
}
