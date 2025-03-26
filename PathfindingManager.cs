using System.Collections.Generic;
using UnityEngine;

/// 寻路算法管理器，用于切换和使用不同的寻路算法
public class PathfindingManager : MonoBehaviour
{
    [Header("寻路算法设置")]
    public PathfindingAlgorithm currentAlgorithm = PathfindingAlgorithm.AStar;
    
    /// 支持的寻路算法枚举
    public enum PathfindingAlgorithm
    {
        AStar,      // A*算法
        DStar,      // D*算法
        DStarLite   // D* Lite算法
    }
    
    private GridManager gridManager;
    private Dictionary<PathfindingAlgorithm, IPathfinder> pathfinders = new Dictionary<PathfindingAlgorithm, IPathfinder>();
    
    private void Awake()
    {
        // 查找GridManager
        gridManager = FindObjectOfType<GridManager>();
        if (gridManager == null)
        {
            Debug.LogError("未找到GridManager，寻路系统无法正常工作");
            return;
        }
        
        // 初始化所有寻路算法
        InitializePathfinders();
    }
    
    /// 初始化所有寻路算法
    private void InitializePathfinders()
    {
        var a = new AStarPathfinder();
        var d = new DStarPathfinder();
        var dl = new DStarLitePathfinder();
        a.Initialize(gridManager);
        d.Initialize(gridManager);
        dl.Initialize(gridManager);
        // 初始化A*算法
        pathfinders[PathfindingAlgorithm.AStar] = a;
        
        // 初始化D*算法
        pathfinders[PathfindingAlgorithm.DStar] = d;
        
        // 初始化D* Lite算法
        pathfinders[PathfindingAlgorithm.DStarLite] = dl;
        
        Debug.Log("所有寻路算法已初始化");
    }
    
    /// 寻找从起点到终点的路径
    public List<GridCell> FindPath(Vector3 startPos, Vector3 endPos, GameObject requestingAgent = null)
    {
        GridCell startCell = gridManager.GetCellFromWorldPoint(startPos);
        GridCell goalCell = gridManager.GetCellFromWorldPoint(endPos);
        
        if (startCell == null || goalCell == null)
        {
            Debug.LogError("无效的起点或终点位置");
            return null;
        }
        
        // 使用当前选择的算法寻路，传入请求路径的机器人
        if (pathfinders.TryGetValue(currentAlgorithm, out IPathfinder pathfinder))
        {
            return pathfinder.FindPath(startCell, goalCell, requestingAgent);
        }
        
        Debug.LogError($"未找到{currentAlgorithm}的实现");
        return null;
    }
    
    /// 通知算法处理环境变化
    public void NotifyChangedCells(List<GridCell> changedCells)
    {
        foreach (var pathfinder in pathfinders.Values)
        {
            pathfinder.HandleChangedCells(changedCells);
        }
    }
    
    /// 设置当前使用的寻路算法
    public void SetAlgorithm(PathfindingAlgorithm algorithm)
    {
        if (pathfinders.ContainsKey(algorithm))
        {
            currentAlgorithm = algorithm;
            Debug.Log($"切换到寻路算法: {pathfinders[algorithm].GetAlgorithmName()}");
        }
        else
        {
            Debug.LogWarning($"未找到算法{algorithm}的实现");
        }
    }
    
    /// 获取当前算法名称
    public string GetCurrentAlgorithmName()
    {
        if (pathfinders.TryGetValue(currentAlgorithm, out IPathfinder pathfinder))
        {
            return pathfinder.GetAlgorithmName();
        }
        return "未知算法";
    }
}