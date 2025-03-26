using System.Collections.Generic;
using UnityEngine;

public interface IPathfinder
{
    List<GridCell> FindPath(GridCell start, GridCell goal, GameObject requestingAgent = null);
    void HandleChangedCells(List<GridCell> changedCells);
    string GetAlgorithmName();
}

