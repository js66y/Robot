using System;
using System.Collections.Generic;
using UnityEngine;

public class AStarPathfinder : IPathfinder
{
    private const int MOVE_STRAIGHT_COST = 10;
    private GridManager gridManager;
    
    // 用于优先队列的比较器
    private class NodeComparer : IComparer<GridCell>
    {
        private Dictionary<GridCell, int> _fCostMap;
        
        public NodeComparer(Dictionary<GridCell, int> fCostMap)
        {
            _fCostMap = fCostMap;
        }
        
        public int Compare(GridCell x, GridCell y)
        {
            int fCostCompare = _fCostMap[x].CompareTo(_fCostMap[y]);
            if (fCostCompare != 0)
                return fCostCompare;
            
            int xHCost = Mathf.RoundToInt(x.hCost);
            int yHCost = Mathf.RoundToInt(y.hCost);
            return xHCost.CompareTo(yHCost);
        }
    }
    public void Initialize(GridManager gridManager)
    {
        this.gridManager = gridManager;
        Debug.Log("A* 寻路算法已初始化");
    }

    private GridCell GetCellAt(int x, int y)
    {
        return gridManager?.GetCellFromGrid(x, y);
    }
    public List<GridCell> FindPath(GridCell start, GridCell goal, GameObject requestingAgent = null)
    {
        if (start == null || goal == null)
            return null;
        
        Dictionary<GridCell, GridCell> cameFrom = new Dictionary<GridCell, GridCell>();
        Dictionary<GridCell, int> gCost = new Dictionary<GridCell, int>();
        Dictionary<GridCell, int> fCost = new Dictionary<GridCell, int>();
        
        // 使用优先队列实现开放列表
        List<GridCell> openSet = new List<GridCell>();
        HashSet<GridCell> closedSet = new HashSet<GridCell>();
        
        // 初始化起点
        openSet.Add(start);
        gCost[start] = 0;
        start.hCost = CalculateHCost(start, goal);
        fCost[start] = (int)start.hCost;
        
        NodeComparer comparer = new NodeComparer(fCost);
        
        while (openSet.Count > 0)
        {
            // 获取F值最小的节点
            openSet.Sort(comparer);
            GridCell current = openSet[0];
            openSet.RemoveAt(0);
            
            // 如果到达目标，构造路径并返回
            if (current == goal)
            {
                return ReconstructPath(cameFrom, current);
            }
            
            closedSet.Add(current);
            
            // 检查所有邻居
            foreach (GridCell neighbor in GetNeighbors(current))
            {
                // 忽略已经处理过的节点
                if (closedSet.Contains(neighbor))
                    continue;
                
                // 忽略不可通行的节点（障碍物或其他机器人）
                if (!neighbor.isWalkable || neighbor.occupiedBy != null && neighbor.occupiedBy != requestingAgent)
                    continue;
                
                // 计算通过当前节点到达邻居的成本
                int tentativeGCost = gCost[current] + MOVE_STRAIGHT_COST;
                
                // 如果找到了更好的路径或者这是一个新的节点
                if (!gCost.ContainsKey(neighbor) || tentativeGCost < gCost[neighbor])
                {
                    // 更新路径和成本
                    cameFrom[neighbor] = current;
                    gCost[neighbor] = tentativeGCost;
                    neighbor.hCost = CalculateHCost(neighbor, goal);
                    // fCost[neighbor] = gCost[neighbor] + neighbor.hCost;
                    
                    fCost[neighbor] = (int)(gCost[neighbor] + neighbor.hCost);
                    
                    // 如果是新节点，添加到开放列表
                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                }
            }
        }
        
        // 没有找到路径
        return null;
    }
    
    private int CalculateHCost(GridCell a, GridCell b)
    {    
    // Convert float to int using explicit cast    
    return (int)(MOVE_STRAIGHT_COST * (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y)));
    }
    
    private List<GridCell> GetNeighbors(GridCell cell)
    {
        List<GridCell> neighbors = new List<GridCell>();
        
        // 上下左右四个方向
        TryAddNeighbor(neighbors, cell.x, cell.y + 1); // 上
        TryAddNeighbor(neighbors, cell.x + 1, cell.y); // 右
        TryAddNeighbor(neighbors, cell.x, cell.y - 1); // 下
        TryAddNeighbor(neighbors, cell.x - 1, cell.y); // 左
        
        return neighbors;
    }
    
    private void TryAddNeighbor(List<GridCell> neighbors, int x, int y)
    {
        // 这里假设有一个方法来获取指定坐标的网格单元
        // 实际实现中，你可能需要从某个GridManager获取
        GridCell neighbor = GetCellAt(x, y);
        if (neighbor != null)
            neighbors.Add(neighbor);
    }
    
    // private GridCell GetCellAt(int x, int y)
    // {
    //     // 示例实现，实际应从GridManager获取
    //     return GridManager.Instance?.GetCellAt(x, y);
    // }
    
    private List<GridCell> ReconstructPath(Dictionary<GridCell, GridCell> cameFrom, GridCell current)
    {
        List<GridCell> path = new List<GridCell>();
        path.Add(current);
        
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        
        return path;
    }
    
    public void HandleChangedCells(List<GridCell> changedCells)
    {
        // A*算法不需要处理地图变化，每次重新搜索
    }
    
    public string GetAlgorithmName()
    {
        return "A* Pathfinding";
    }
}
