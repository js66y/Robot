using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class GridManager : MonoBehaviour
{
    [Header("网格配置")]
    public Vector2 gridWorldSize = new Vector2(40, 40); // 网格覆盖的世界大小
    public float cellSize = 1.0f; // 每个格子的大小
    public bool displayGrid = true; // 是否显示网格
    public float Alpha = 0.5f; // 网格透明度
    
    [Header("可视化设置")]
    public GameObject cellPrefab; // 网格预制体
    public Color walkableColor = Color.white; // 可行走颜色
    public Color unwalkableColor = Color.red; // 不可行走颜色
    public float cellHeight = 0.1f; // 网格高度
    
    public GridCell[,] grid; // 二维网格数组
    private int gridSizeX, gridSizeZ; // 网格的X和Z维度大小
    private Transform gridParent; // 网格的父物体
    public bool isWalkable => walkable;    
    public GameObject occupiedBy => occupyingObject;    
    public int x => gridX;    
    public int y => gridZ;
    public bool IsAccessibleFor(GameObject agent)
    {
        return walkable && (occupyingObject == null || occupyingObject == agent);
    }
    private Dictionary<GameObject, GridCell> objectToCellMap = new Dictionary<GameObject, GridCell>(); // 对象到网格的映射

    private void Awake()
    {
        // 计算网格大小
        gridSizeX = Mathf.RoundToInt(gridWorldSize.x / cellSize);
        gridSizeZ = Mathf.RoundToInt(gridWorldSize.y / cellSize);
        
        // 创建网格父物体
        gridParent = new GameObject("Grid").transform;
        gridParent.SetParent(transform);
        
        // 创建网格系统
        CreateGrid();
    }
    // public List<GridCell> GetPathDStarLite(Vector3 startPos, Vector3 endPos)
    // {
    //     GridCell startCell = GetCellFromWorldPoint(startPos);
    //     GridCell goalCell = GetCellFromWorldPoint(endPos);
        
    //     DStarLite dstar = new DStarLite(this, startCell, goalCell);
    //     return dstar.ComputeShortestPath();
    // }
    // 获取所有网格的方法
    public IEnumerable<GridCell> GetAllCells()
    {
        for (int x = 0; x < gridSizeX; x++)
        {
            for (int z = 0; z < gridSizeZ; z++)
            {
                yield return grid[x, z];
            }
        }
    }

    // 强化RefreshAllObjectPositions方法
    public void RefreshAllObjectPositions()
    {
        Debug.Log("正在刷新所有物体在网格中的位置...");
        
        // 清除所有对象的位置映射
        objectToCellMap.Clear();
        
        // 重置所有网格
        for (int x = 0; x < gridSizeX; x++)
        {
            for (int z = 0; z < gridSizeZ; z++)
            {
                grid[x, z].occupyingObject = null;
                grid[x, z].walkable = true;
            }
        }
        
        // 优先标记所有货架位置，确保它们始终不可行走
        Shelf[] shelves = FindObjectsOfType<Shelf>();
        Debug.Log($"找到 {shelves.Length} 个货架");
        foreach(Shelf shelf in shelves)
        {
            GridCell cell = GetCellFromWorldPoint(shelf.transform.position);
            if (cell != null)
            {
                cell.occupyingObject = shelf.gameObject;
                cell.walkable = false; // 货架不可行走
                objectToCellMap[shelf.gameObject] = cell;
                
                // 更新可视化
                if (cell.visualObject != null)
                {
                    Renderer renderer = cell.visualObject.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        Color color = unwalkableColor;
                        color.a = Alpha;
                        renderer.material.color = color;
                    }
                }
            }
        }
        
        // 然后标记所有机器人
        Robot[] robots = FindObjectsOfType<Robot>();
        Debug.Log($"找到 {robots.Length} 个机器人");
        foreach(Robot robot in robots)
        {
            GridCell cell = GetCellFromWorldPoint(robot.transform.position);
            if (cell != null && cell.occupyingObject == null) // 不覆盖货架
            {
                cell.occupyingObject = robot.gameObject;
                // 机器人位置可行走，允许其他机器人规划路径通过这里
                cell.walkable = true;
                objectToCellMap[robot.gameObject] = cell;
            }
        }
        
        // 标记其他障碍物
        GameObject[] obstacles = GameObject.FindGameObjectsWithTag("Obstacle");
        foreach(GameObject obstacle in obstacles)
        {
            GridCell cell = GetCellFromWorldPoint(obstacle.transform.position);
            if (cell != null)
            {
                cell.occupyingObject = obstacle;
                cell.walkable = false; // 障碍物不可行走
                objectToCellMap[obstacle] = cell;
                
                // 更新可视化
                if (cell.visualObject != null)
                {
                    Renderer renderer = cell.visualObject.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        Color color = unwalkableColor;
                        color.a = Alpha;
                        renderer.material.color = color;
                    }
                }
            }
        }
        
        // 检查网格完整性
        VerifyGridIntegrity();
    }
    // 验证网格状态
    private void VerifyGridIntegrity()
    {
        int unwalkableCells = 0;
        int shelfCells = 0;
        
        for (int x = 0; x < gridSizeX; x++)
        {
            for (int z = 0; z < gridSizeZ; z++)
            {
                GridCell cell = grid[x, z];
                
                // 检查是否有货架但单元格标记为可行走
                if (cell.occupyingObject != null && cell.occupyingObject.GetComponent<Shelf>() != null && cell.walkable)
                {
                    Debug.LogWarning($"网格完整性错误：货架位置 ({x},{z}) 被错误标记为可行走，已修正");
                    cell.walkable = false;
                    
                    // 更新可视化
                    if (cell.visualObject != null)
                    {
                        Renderer renderer = cell.visualObject.GetComponent<Renderer>();
                        if (renderer != null)
                        {
                            Color color = unwalkableColor;
                            color.a = Alpha;
                            renderer.material.color = color;
                        }
                    }
                }
                
                // 统计
                if (!cell.walkable) unwalkableCells++;
                if (cell.occupyingObject != null && cell.occupyingObject.GetComponent<Shelf>() != null) shelfCells++;
            }
        }
        
        Debug.Log($"网格验证完成：共有 {unwalkableCells} 个不可行走单元格，{shelfCells} 个货架单元格");
    }
    void CreateGrid()
    {
        grid = new GridCell[gridSizeX, gridSizeZ];
        
        // 计算网格的世界坐标起点（左下角）
        Vector3 worldBottomLeft = transform.position - Vector3.right * gridWorldSize.x / 2 - Vector3.forward * gridWorldSize.y / 2;
        
        // 创建每个网格
        for (int x = 0; x < gridSizeX; x++)
        {
            for (int z = 0; z < gridSizeZ; z++)
            {
                // 计算网格的世界坐标
                Vector3 worldPoint = worldBottomLeft + Vector3.right * (x * cellSize + cellSize / 2) + Vector3.forward * (z * cellSize + cellSize / 2);
                
                // 检查是否可行走（通过射线检测）
                bool walkable = !Physics.CheckBox(worldPoint, new Vector3(cellSize / 2 - 0.1f, 0.1f, cellSize / 2 - 0.1f), Quaternion.identity, LayerMask.GetMask("Obstacle"));
                
                // 创建网格单元
                grid[x, z] = new GridCell(x, z, worldPoint, walkable);
                
                // 如果显示网格，则创建可视化物体
                if (displayGrid)
                {
                    CreateGridVisual(grid[x, z]);
                }
            }
        }
    }
    
    void CreateGridVisual(GridCell cell)
    {
        if (cellPrefab == null) return;
        
        // 实例化网格预制体
        GameObject cellObj = Instantiate(cellPrefab, cell.worldPosition, Quaternion.identity);
        cellObj.transform.localScale = new Vector3(cellSize * 0.9f, cellHeight, cellSize * 0.9f);
        cellObj.transform.SetParent(gridParent);
        
        // 设置颜色
        Renderer renderer = cellObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            // 设置颜色和透明度
            Color color = cell.walkable ? walkableColor : unwalkableColor;
            color.a = 0.5f; // 设置透明度
            renderer.material.color = color;
        }
        
        // 设置名称
        cellObj.name = "Cell_" + cell.gridX + "_" + cell.gridZ;
        
        // 存储可视化物体引用
        cell.visualObject = cellObj;
    }
    
    // 获取世界坐标对应的网格单元
    public GridCell GetCellFromWorldPoint(Vector3 worldPosition)
    {
        // 计算相对位置
        float percentX = Mathf.Clamp01((worldPosition.x - (transform.position.x - gridWorldSize.x / 2)) / gridWorldSize.x);
        float percentZ = Mathf.Clamp01((worldPosition.z - (transform.position.z - gridWorldSize.y / 2)) / gridWorldSize.y);
        
        // 计算网格坐标
        int x = Mathf.Clamp(Mathf.RoundToInt((gridSizeX - 1) * percentX), 0, gridSizeX - 1);
        int z = Mathf.Clamp(Mathf.RoundToInt((gridSizeZ - 1) * percentZ), 0, gridSizeZ - 1);
        
        return grid[x, z];
    }
    
    // 获取相邻的网格单元
    public List<GridCell> GetNeighbors(GridCell cell)
    {
        List<GridCell> neighbors = new List<GridCell>();
        
        // 检查八个方向的相邻格子
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                // 跳过自身
                if (x == 0 && z == 0) continue;
                
                // 计算相邻格子的坐标
                int checkX = cell.gridX + x;
                int checkZ = cell.gridZ + z;
                
                // 检查是否在网格范围内
                if (checkX >= 0 && checkX < gridSizeX && checkZ >= 0 && checkZ < gridSizeZ)
                {
                    neighbors.Add(grid[checkX, checkZ]);
                }
            }
        }
        
        return neighbors;
    }
    
    // 更新网格单元的状态
    public void UpdateCellWalkable(int x, int z, bool walkable)
    {
        if (x >= 0 && x < gridSizeX && z >= 0 && z < gridSizeZ)
        {
            grid[x, z].walkable = walkable;
            
            // 更新可视化
            if (grid[x, z].visualObject != null)
            {
                Renderer renderer = grid[x, z].visualObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Color color = walkable ? walkableColor : unwalkableColor;
                    color.a = Alpha;
                    renderer.material.color = color;
                }
            }
        }
    }
    
    public void MarkObjectPosition(GameObject obj, bool walkable = false)
    {
        if (obj == null) return;
        
        // 更新之前的位置
        if (objectToCellMap.ContainsKey(obj))
        {
            GridCell oldCell = objectToCellMap[obj];
            
            // 只有当这个Cell确实被该对象占用时才清除
            if (oldCell.occupyingObject == obj)
            {
                oldCell.occupyingObject = null;
                oldCell.walkable = true; // 释放旧位置
            }
            
            objectToCellMap.Remove(obj); // 从字典中移除旧映射
        }
        
        Vector3 position = obj.transform.position;
        GridCell cell = GetCellFromWorldPoint(position);
        
        if (cell != null)
        {
            // 设置新位置的占用
            cell.occupyingObject = walkable ? null : obj;
            cell.walkable = walkable;
            
            // 记录对象位置
            objectToCellMap[obj] = cell;
        }
    }
    
    // 显示/隐藏网格
    public void ToggleGridDisplay(bool show)
    {
        displayGrid = show;
        gridParent.gameObject.SetActive(show);
    }
    
    // // 获取起点到终点的路径

    // public List<GridCell> GetPath(Vector3 startPos, Vector3 endPos, GameObject requestingAgent = null)
    // {
    //     GridCell startCell = GetCellFromWorldPoint(startPos);
    //     GridCell targetCell = GetCellFromWorldPoint(endPos);
        
    //     Debug.Log($"开始寻路: 从 ({startCell.gridX},{startCell.gridZ}) 到 ({targetCell.gridX},{targetCell.gridZ})");
        
    //     // 记录起点的原始状态
    //     bool originalWalkable = startCell.walkable;
    //     GameObject originalObject = startCell.occupyingObject;
        
    //     // 临时标记起点为可行走
    //     startCell.walkable = true;
    //     startCell.occupyingObject = null;
        
    //     try
    //     {
    //         // 使用 D* Lite 寻路算法
    //         DStarLite dstar = new DStarLite(this, startCell, targetCell);
    //         List<GridCell> path = dstar.ComputeShortestPath();
            
    //         // 如果 D* Lite 返回空路径或仅有目标点，使用简单路径
    //         if (path == null || path.Count < 2)
    //         {
    //             Debug.LogWarning($"D* Lite 无法找到有效路径，使用简单路径替代");
    //             path = GenerateStraightLinePath(startCell, targetCell);
    //             Debug.Log($"生成简单路径，节点数: {path?.Count ?? 0}");
    //         }
            
    //         return path;
    //     }
    //     finally
    //     {
    //         // 恢复起点原始状态
    //         startCell.walkable = originalWalkable;
    //         startCell.occupyingObject = originalObject;
    //     }
    // }
    // 添加简单直线路径生成方法
    private List<GridCell> GenerateStraightLinePath(GridCell start, GridCell target)
    {
        List<GridCell> path = new List<GridCell>();
        path.Add(start);
        
        // 如果起点和终点相同，只返回起点
        if (start.gridX == target.gridX && start.gridZ == target.gridZ)
            return path;
        
        // 计算起点到终点的方向向量
        int dx = target.gridX - start.gridX;
        int dz = target.gridZ - start.gridZ;
        int steps = Math.Max(Math.Abs(dx), Math.Abs(dz));
        
        if (steps == 0)
        {
            path.Add(target);
            return path;
        }
        
        float stepX = (float)dx / steps;
        float stepZ = (float)dz / steps;
        
        // 生成中间点
        for (int i = 1; i < steps; i++)
        {
            int x = Mathf.RoundToInt(start.gridX + stepX * i);
            int z = Mathf.RoundToInt(start.gridZ + stepZ * i);
            
            if (x >= 0 && x < gridSizeX && z >= 0 && z < gridSizeZ)
            {
                path.Add(grid[x, z]);
            }
        }
        
        // 添加终点
        if (!path.Contains(target))
            path.Add(target);
        
        return path;
    }
    // 获取所有网格信息
    public GridCell[,] GetGrid()
    {
        return grid;
    }
    
    // 获取网格尺寸
    public Vector2Int GetGridSize()
    {
        return new Vector2Int(gridSizeX, gridSizeZ);
    }

    // 添加到GridManager类中

    private Dictionary<int, GridCell> objectCellCache = new Dictionary<int, GridCell>();

    public GridCell GetOrCreateCellForObject(GameObject obj)
    {
        int id = obj.GetInstanceID();
        if (!objectCellCache.ContainsKey(id))
        {
            objectCellCache[id] = GetCellFromWorldPoint(obj.transform.position);
        }
        return objectCellCache[id];
    }

    // 当物体移动时调用此方法更新缓存
    public void UpdateObjectCell(GameObject obj)
    {
        int id = obj.GetInstanceID();
        objectCellCache[id] = GetCellFromWorldPoint(obj.transform.position);
    }
    // 添加一个调试方法，可以在出现路径问题时调用
    public void CheckGridIntegrity()
    {
        int shelves = 0;
        int robots = 0;
        int unwalkable = 0;
        
        for (int x = 0; x < gridSizeX; x++)
        {
            for (int z = 0; z < gridSizeZ; z++)
            {
                GridCell cell = grid[x, z];
                
                // 检查货架
                if (cell.occupyingObject != null && cell.occupyingObject.GetComponent<Shelf>() != null)
                {
                    shelves++;
                    // 确保货架单元格标记为不可行走
                    if (cell.walkable)
                    {
                        Debug.LogWarning($"网格完整性警告：货架位置({x},{z})被错误标记为可行走");
                        cell.walkable = false;
                    }
                }
                
                // 检查机器人
                if (cell.occupyingObject != null && cell.occupyingObject.GetComponent<Robot>() != null)
                {
                    robots++;
                }
                
                // 统计不可行走单元格
                if (!cell.walkable)
                {
                    unwalkable++;
                }
            }
        }
        
        Debug.Log($"网格完整性检查：货架 {shelves}，机器人 {robots}，不可行走单元格 {unwalkable}");
    }

    public GridCell GetCellFromGrid(int x, int z)
    {
        // 检查坐标是否在有效范围内
        if (x >= 0 && x < gridSizeX && z >= 0 && z < gridSizeZ)
        {
            return grid[x, z];
        }
        return null; // 如果坐标超出范围，返回null
    }
}














[System.Serializable]
public class GridCell
{
    // 基础属性
    public int gridX; // 网格X坐标
    public int gridZ; // 网格Z坐标
    public Vector3 worldPosition; // 世界坐标
    public bool walkable; // 是否可行走
    public GameObject visualObject; // 可视化物体
    public GameObject occupyingObject; // 占用此网格的物体
    public bool isShelfAdjacent; // 标记是否为货架相邻网格

    // A*算法属性
    public float gCost; // 从起点到当前节点的实际代价
    public float hCost; // 从当前节点到目标的估计代价
    public float fCost => gCost + hCost; // A*的总代价
    public GridCell parent; // 父节点，用于重建路径

    // D*算法属性
    public int tag; // 节点标记（NEW, OPEN, CLOSED）
    public float h; // 启发式值
    public float k; // 节点优先级键值
    public GridCell backPointer; // 回溯指针

    // D* Lite算法属性
    public float g; // 实际代价
    public float rhs; // 右侧启发值
    public float[] key; // 优先队列键值

    public GridCell(int _gridX, int _gridZ, Vector3 _worldPos, bool _walkable)
    {
        gridX = _gridX;
        gridZ = _gridZ;
        worldPosition = _worldPos;
        walkable = _walkable;
        isShelfAdjacent = false;
        
        // 初始化寻路算法相关属性
        InitializePathfinding();
    }

    public void InitializePathfinding()
    {
        // A*初始化
        gCost = float.MaxValue;
        hCost = 0;
        parent = null;

        // D*初始化
        tag = 0;
        h = float.MaxValue;
        k = float.MaxValue;
        backPointer = null;

        // D* Lite初始化
        g = float.MaxValue;
        rhs = float.MaxValue;
        key = new float[2] { float.MaxValue, float.MaxValue };
    }

    // 重置节点状态
    public void Reset()
    {
        InitializePathfinding();
    }

    // 用于比较两个网格单元是否相等
    public override bool Equals(object obj)
    {
        if (obj == null || !(obj is GridCell))
            return false;

        GridCell other = (GridCell)obj;
        return gridX == other.gridX && gridZ == other.gridZ;
    }

    public override int GetHashCode()
    {
        return gridX.GetHashCode() ^ gridZ.GetHashCode();
    }
}
