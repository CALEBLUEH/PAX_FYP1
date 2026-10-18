using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pax
{
    public readonly struct PaxBlueprintPlacement
    {
        public readonly Vector3Int Cell;
        public readonly string MaterialId;
        public PaxBlueprintPlacement(Vector3Int cell, string materialId) { Cell = cell; MaterialId = materialId; }
    }
    [CreateAssetMenu(menuName = "PAX/Construction blueprint")]
    public sealed class PaxConstructionBlueprint : ScriptableObject
    {
        public const int MaximumCells = 8;
        [Serializable] public struct Cell { public Vector3Int offset; public string materialId; }
        [SerializeField] private string blueprintTitle;
        [SerializeField] private Cell[] cells;
        public string Title => blueprintTitle;
        public IReadOnlyList<Cell> Cells => cells;
        public void Configure(string title, Cell[] values) { blueprintTitle = title; cells = values; }
        public bool TryPlan(Vector3Int anchor, int quarterTurns, out PaxBlueprintPlacement[] plan, out string reason)
        {
            plan = null; reason = "Blueprint requires 2–8 unique cells, an origin cell, material IDs and offsets within four tiles.";
            if (cells == null || cells.Length < 2 || cells.Length > MaximumCells) return false;
            var unique = new HashSet<Vector3Int>(); bool origin = false;
            foreach (var cell in cells)
            {
                var o = cell.offset;
                if (o.x < -4 || o.x > 4 || o.y < -4 || o.y > 4 || o.z < -4 || o.z > 4 || !unique.Add(o) || !PaxMaterialPalette.IsValidId(cell.materialId)) return false;
                origin |= o == Vector3Int.zero;
            }
            if (!origin) return false;
            int turn = (quarterTurns % 4 + 4) % 4; var result = new PaxBlueprintPlacement[cells.Length];
            try
            {
                for (int i = 0; i < cells.Length; i++)
                {
                    var o = cells[i].offset;
                    var rotated = turn == 0 ? o : turn == 1 ? new Vector3Int(o.z,o.y,-o.x) : turn == 2 ? new Vector3Int(-o.x,o.y,-o.z) : new Vector3Int(-o.z,o.y,o.x);
                    var target = new Vector3Int(checked(anchor.x+rotated.x),checked(anchor.y+rotated.y),checked(anchor.z+rotated.z));
                    result[i] = new PaxBlueprintPlacement(target,cells[i].materialId);
                }
            }
            catch (OverflowException) { reason = "Blueprint anchor is outside the grid range."; return false; }
            plan = result; reason = null; return true;
        }
    }
}
