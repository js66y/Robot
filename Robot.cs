using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Robot : MonoBehaviour
{
    public Station homeStation; // 所属的Station
    public GridCell currentCell; // 当前所在的网格
    public Color color; // 当前颜色
    public bool hasTask; // 是否接到任务
    public bool hasCargo; // 是否携带货物

    private GridManager gridManager;
    private Renderer robotRenderer;
    private Task currentTask;
    private List<GridCell> currentPath;

    private int currentPathIndex;
    // private Shelf targetShelf;
    private bool isReturning;
    private List<GridCell> changedCells = new List<GridCell>();
    // 在 Robot 类中添加
    public float moveSpeed = 5.0f; // 机器人移动速度
    public Shelf targetShelf; // 当前目标货架
    
    private PathVisualizer pathVisualizer;
    private float stuckTime = 0f;
    private Vector3 lastPosition;
    private Vector3 currentVelocity = Vector3.zero;
    private Vector3 targetPosition;
    private Coroutine pickupCoroutine; // 用于等待货架
    private const float STUCK_THRESHOLD = 3.0f; // 3秒无移动视为卡住

    private const float MIN_DISTANCE_TO_TARGET = 0.1f; // 认为到达目标的最小距离
    private PathfindingManager pathfindingManager;
    private bool isColliding = false;
    private float collisionTime = 0f;
    private Color originalRobotColor;
    private const float COLLISION_RECOVERY_TIME = 1.0f;
    // 处理被其他机器人通知的碰撞
    public void HandleCollision()
    {
        if (!isColliding) // 避免重复处理
        {
            isColliding = true;
            collisionTime = Time.time;
            originalRobotColor = robotRenderer.material.color;
            robotRenderer.material.color = Color.red;
            
            // 停止当前移动
            currentVelocity = Vector3.zero;
            
            // 延迟一秒后重新规划路径
            StartCoroutine(ReplanPathAfterCollision());
        }
    }
    // 碰撞后重新规划路径
    private IEnumerator ReplanPathAfterCollision()
    {
        // 等待一秒
        yield return new WaitForSeconds(COLLISION_RECOVERY_TIME);
        
        // 恢复原始颜色
        robotRenderer.material.color = originalRobotColor;
        isColliding = false;
        
        // 重新规划路径
        if (hasTask)
        {
            if (isReturning)
            {
                // 重新规划返回站点的路径
                CalculatePathTo(homeStation.transform.position);
            }
            else if (targetShelf != null)
            {
                // 重新规划前往货架的路径
                Vector3 accessPoint = GetAccessPointForShelf(targetShelf, true);
                CalculatePathTo(accessPoint);
            }
        }
    }
    // 添加碰撞检测方法
    private void OnCollisionEnter(Collision collision)
    {
        // 检查是否与其他机器人碰撞
        Robot otherRobot = collision.gameObject.GetComponent<Robot>();
        if (otherRobot != null)
        {
            Debug.LogWarning($"机器人 {gameObject.name} 与机器人 {otherRobot.name} 发生碰撞！");
            
            // 标记碰撞状态
            isColliding = true;
            collisionTime = Time.time;
            
            // 保存原始颜色并将机器人变成红色
            originalRobotColor = robotRenderer.material.color;
            robotRenderer.material.color = Color.red;
            
            // 停止当前移动
            currentVelocity = Vector3.zero;
            
            // 通知其他机器人也变红
            otherRobot.HandleCollision();
            
            // 延迟一秒后重新规划路径
            StartCoroutine(ReplanPathAfterCollision());
        }
    }
    private void Awake()
    {
        gridManager = FindObjectOfType<GridManager>();
        robotRenderer = GetComponentInChildren<Renderer>();
        pathfindingManager = FindObjectOfType<PathfindingManager>();
        
        // 获取或添加PathVisualizer组件
        pathVisualizer = GetComponentInChildren<PathVisualizer>();
        if (pathVisualizer == null)
        {
            GameObject visualizerObject = new GameObject("PathVisualizer");
            visualizerObject.transform.SetParent(transform);
            visualizerObject.transform.localPosition = Vector3.zero;
            pathVisualizer = visualizerObject.AddComponent<PathVisualizer>();
        }
    }


    // 初始化Robot状态
    public void Initialize(Station station, GridCell spawnCell)
    {
        homeStation = station;
        currentCell = spawnCell;
        transform.position = spawnCell.worldPosition;
        // 设置初始朝向为Y轴正方向（90度旋转）
        transform.rotation = Quaternion.Euler(0, -90, 0);
        // 设置Robot的图层
        gameObject.layer = LayerMask.NameToLayer("Robot");
        UpdateColor(homeStation.stationColor); // 初始颜色与Station一致
        gridManager.MarkObjectPosition(gameObject, false); // 标记当前位置不可行走
    }
    public void AssignTask(Task task)
    {
        // 接收新任务前，清除上一次任务的路径历史
        if (pathVisualizer != null)
        {
            pathVisualizer.ClearPath();
        }
        currentTask = task;
        hasTask = true;
        targetShelf = task.targetShelf;
        UpdateColor(homeStation.stationColor);
        
        Debug.Log($"机器人 {gameObject.name} 被分配任务: 前往货架 {targetShelf.name}");
        
        // 计算货架访问点而非直接前往货架位置
        Vector3 accessPoint = GetAccessPointForShelf(targetShelf);
        
        // 计算路径并检查
        CalculatePathTo(accessPoint);
        
        // 检查路径是否有效
        if (currentPath == null || currentPath.Count <= 1)
        {
            Debug.LogWarning($"警告: 机器人 {gameObject.name} 无法找到到达货架 {targetShelf.name} 的路径，等待1秒后重试");
            StartCoroutine(RetryPathfindingAfterDelay(1.0f));
        }
        else
        {
            Debug.Log($"机器人 {gameObject.name} 找到路径，长度为 {currentPath.Count}");
        }
    }

    // 添加一个新的协程方法，用于延迟重试路径规划
    private IEnumerator RetryPathfindingAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        Debug.Log($"机器人 {gameObject.name} 重新尝试规划到达货架 {targetShelf.name} 的路径");
        
        // 重新计算到货架的路径
        Vector3 accessPoint = GetAccessPointForShelf(targetShelf);
        CalculatePathTo(accessPoint);
        
        // 二次检查路径有效性
        if (currentPath == null || currentPath.Count <= 1)
        {
            Debug.LogWarning($"机器人 {gameObject.name} 第二次尝试规划路径仍然失败，尝试更换目标点");
            
            // 尝试寻找附近可行的目标点
            Vector3 alternativeTarget = GetAccessPointForShelf(targetShelf, true);
            if (alternativeTarget != accessPoint)
            {
                Debug.Log($"机器人 {gameObject.name} 尝试使用替代目标点");
                CalculatePathTo(alternativeTarget);
                
                if (currentPath == null || currentPath.Count <= 1)
                {
                    Debug.LogError($"机器人 {gameObject.name} 多次尝试规划路径均失败，暂时放弃此任务");
                    // 放弃当前任务，通知Station
                    if (homeStation != null)
                    {
                        homeStation.TaskFailed(currentTask);
                        hasTask = false;
                        targetShelf = null;
                        homeStation.AddRobotToIdleList(this);
                    }
                }
            }
        }
        else
        {
            Debug.Log($"机器人 {gameObject.name} 重试后找到路径，长度为 {currentPath.Count}");
        }
    }
    // 修改计算路径的方法，使用PathfindingManager
    private void CalculatePathTo(Vector3 targetPosition)
    {
        try
        {
            // 计算路径可视化的颜色
            Color pathColor = homeStation != null ? homeStation.stationColor : color;
            
            // 使用PathfindingManager计算路径
            if (pathfindingManager != null)
            {
                currentPath = pathfindingManager.FindPath(transform.position, targetPosition, gameObject);
                currentPathIndex = 0;
                
                // 更新路径可视化
                if (pathVisualizer != null && currentPath != null && currentPath.Count > 0)
                {
                    pathVisualizer.UpdatePathVisualization(currentPath, pathColor);
                }
                
                // 检查路径有效性
                if (currentPath == null || currentPath.Count <= 1)
                {
                    Debug.LogWarning($"机器人 {gameObject.name} 无法找到到达目标的路径，尝试附近点");
                    
                    // 尝试寻找附近可行的目标点
                    Vector3 alternativeTarget = FindNearbyAccessiblePoint(targetPosition);
                    if (alternativeTarget != targetPosition)
                    {
                        Debug.Log($"机器人 {gameObject.name} 使用替代目标点");
                        currentPath = pathfindingManager.FindPath(transform.position, alternativeTarget, gameObject);
                        currentPathIndex = 0;
                        
                        if (pathVisualizer != null && currentPath != null && currentPath.Count > 0)
                        {
                            pathVisualizer.UpdatePathVisualization(currentPath, pathColor);
                        }
                    }
                }
            }
            else
            {
                Debug.LogError("PathfindingManager未找到");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"计算路径时出错: {e.Message}\n{e.StackTrace}");
        }
    }
    // 辅助方法：寻找附近可行的点
    private Vector3 FindNearbyAccessiblePoint(Vector3 targetPosition)
    {
        // 尝试在目标周围找到可行走的点
        for (int radius = 1; radius <= 5; radius++)
        {
            GridCell accessibleCell = FindNearestWalkableCell(targetPosition, radius);
            if (accessibleCell != null)
            {
                return accessibleCell.worldPosition;
            }
        }
        return targetPosition; // 如果找不到，返回原始目标
    }
    // 修改环境变化处理方法
    public void NotifyEnvironmentChange(List<GridCell> changedCells)
    {
        try
        {
            // 通知PathfindingManager环境变化
            if (pathfindingManager != null)
            {
                pathfindingManager.NotifyChangedCells(changedCells);
                
                // 如果有当前任务，重新计算路径
                if (hasTask && (currentPath == null || CheckPathObstruction()))
                {
                    if (isReturning)
                    {
                        CalculatePathTo(homeStation.transform.position);
                    }
                    else if (targetShelf != null)
                    {
                        CalculatePathTo(GetAccessPointForShelf(targetShelf));
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"通知环境变化时出错: {e.Message}\n{e.StackTrace}");
        }
    }
    // 更新颜色
    public void UpdateColor(Color newColor)
    {
        color = newColor;
        if (robotRenderer != null)
        {
            robotRenderer.material.color = newColor;
        }
    }

    // Update方法改为平滑移动
    private void Update()
    {
        if (hasTask && currentPath != null && currentPathIndex < currentPath.Count)
        {
            // 卡住检测
            if (Vector3.Distance(transform.position, lastPosition) < 0.01f)
            {
                stuckTime += Time.deltaTime;
                if (stuckTime > STUCK_THRESHOLD)
                {
                    Debug.LogWarning($"机器人 {gameObject.name} 似乎卡住了，尝试重新规划路径");
                    stuckTime = 0f;
                    
                    // 强制重新规划路径
                    if (isReturning)
                        CalculatePathTo(homeStation.transform.position);
                    else if (targetShelf != null)
                        CalculatePathTo(GetAccessPointForShelf(targetShelf));
                }
            }
            else
            {
                stuckTime = 0f;
                lastPosition = transform.position;
            }
            // 处理碰撞恢复
            if (isColliding && Time.time - collisionTime > COLLISION_RECOVERY_TIME)
            {
                // 时间到，恢复原始颜色
                robotRenderer.material.color = originalRobotColor;
                isColliding = false;
            }
            
            // 如果正在碰撞状态，不执行移动
            if (isColliding)
                return;
            // 平滑移动到下一个路径点
            SmoothMoveAlongPath();
        }
        
        // 确保位置对齐当前单元格
        if (currentCell != null)
        {
            // 如果移动，不需要强制对齐，只标记所在单元格
            GridCell newCell = gridManager.GetCellFromWorldPoint(transform.position);
            if (newCell != currentCell)
            {
                gridManager.MarkObjectPosition(gameObject, true); // 释放当前格
                currentCell = newCell;
                gridManager.MarkObjectPosition(gameObject, false); // 占用新格
            }
        }
    }
    private void SmoothMoveAlongPath()
    {
        if (currentPath == null || currentPathIndex >= currentPath.Count)
            return;
        
        // 获取当前目标点
        Vector3 targetPosition = currentPath[currentPathIndex].worldPosition;
        
        // 计算到目标点的距离
        float distanceToTarget = Vector3.Distance(transform.position, targetPosition);
        
        // 如果足够接近目标点，移动到下一个点
        if (distanceToTarget < MIN_DISTANCE_TO_TARGET)
        {
            currentPathIndex++;
            
            // 更新路径可视化器中的已完成路径段
            if (pathVisualizer != null)
            {
                pathVisualizer.MarkCompletedPathSegment(currentPathIndex);
            }
            
            // 如果到达了路径终点
            if (currentPathIndex >= currentPath.Count)
            {
                OnPathCompleted();
                return;
            }
            
            // 更新目标位置
            targetPosition = currentPath[currentPathIndex].worldPosition;
        }
        
        // 计算移动方向
        Vector3 direction = (targetPosition - transform.position).normalized;
        
        // 平滑移动
        float speed = moveSpeed * Time.deltaTime;
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, 0.2f, speed);
        
        // 计算旋转方向 - 确保始终以Y轴-90度向前
        if (direction != Vector3.zero)
        {
            // 计算目标旋转
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            
            // 调整为Y轴-90度朝向
            targetRotation *= Quaternion.Euler(0, -90, 0);
            
            // 平滑旋转
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
        }
    }
    // 路径完成处理
    private void OnPathCompleted()
    {
        Debug.Log($"机器人 {gameObject.name} 到达路径终点");
        
        if (!isReturning)
        {
            // 确认是否已到达货架附近
            float distToShelf = Vector3.Distance(transform.position, targetShelf.transform.position);
            Debug.Log($"机器人 {gameObject.name} 距离货架 {distToShelf} 单位");
            
            if (distToShelf <= 2.0f) // 如果在货架附近
            {
                // 标记货架并等待1秒后取货
                targetShelf.SetHighlight(true);
                pickupCoroutine = StartCoroutine(PickUpCargoAfterDelay(1.0f));
            }
            else
            {
                Debug.LogWarning($"机器人 {gameObject.name} 未能达到货架附近，重新规划路径");
                CalculatePathTo(GetAccessPointForShelf(targetShelf));
            }
        }
        else
        {
            DeliverCargo();
        }
    }
    // 延迟取货的协程
    private IEnumerator PickUpCargoAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // 取货
        hasCargo = true;
        UpdateColor(Color.red);
        targetShelf.SetHighlight(false);
        
        // 计算返回路径
        CalculatePathTo(homeStation.transform.position);
        isReturning = true;
    }
    private void MoveAlongPath()
    {
        if (currentPath == null || currentPathIndex >= currentPath.Count)
        {
            Debug.LogWarning($"机器人 {gameObject.name} 的路径为空或已到达终点");
            return;
        }

        GridCell nextCell = currentPath[currentPathIndex];
        Debug.Log($"机器人 {gameObject.name} 尝试移动到 ({nextCell.gridX},{nextCell.gridZ})，可行走: {nextCell.walkable}，占用物体: {(nextCell.occupyingObject ? nextCell.occupyingObject.name : "无")}");
        
        // 使用新的可访问性检查
        bool canMove = nextCell.IsAccessibleFor(gameObject);
        
        if (canMove)
        {
            Debug.Log($"机器人 {gameObject.name} 成功移动到新位置");
            
            // 先释放当前位置
            if (currentCell != null)
            {
                gridManager.MarkObjectPosition(gameObject, true);
            }
            
            // 移动到新位置
            currentCell = nextCell;
            transform.position = currentCell.worldPosition;
            
            // 标记新位置为不可行走
            gridManager.MarkObjectPosition(gameObject, false);
            
            currentPathIndex++;

            // 到达目标
            if (currentPathIndex >= currentPath.Count)
            {
                Debug.Log($"机器人 {gameObject.name} 到达路径终点");
                if (!isReturning)
                {
                    // 确认是否已到达货架附近
                    float distToShelf = Vector3.Distance(transform.position, targetShelf.transform.position);
                    Debug.Log($"机器人 {gameObject.name} 距离货架 {distToShelf} 单位");
                    
                    if (distToShelf <= 2.0f) // 如果在货架附近
                    {
                        PickUpCargo();
                        CalculatePathTo(homeStation.transform.position);
                        isReturning = true;
                    }
                    else
                    {
                        Debug.LogWarning($"机器人 {gameObject.name} 未能达到货架附近，重新规划路径");
                        CalculatePathTo(GetAccessPointForShelf(targetShelf));
                    }
                }
                else
                {
                    DeliverCargo();
                }
            }
        }
        else
        {
            Debug.LogWarning($"机器人 {gameObject.name} 前方格子不可行走，重新规划路径");
            // 路径被阻塞，重新计算
            if (isReturning)
            {
                CalculatePathTo(homeStation.transform.position);
            }
            else if (targetShelf != null)
            {
                CalculatePathTo(GetAccessPointForShelf(targetShelf));
            }
        }
        
        // 检测到路径变化时
        if (CheckPathObstruction())
        {
            List<GridCell> changed = DetectChangedCells();
            // if (pathPlanner != null)
            // {
            //     pathPlanner.HandleChangedCells(changed);
            //     currentPath = pathPlanner.ComputeShortestPath();
            //     currentPathIndex = 0;
            //     // 更新路径可视化
            //     if (pathVisualizer != null)
            //     {
            //         Color visualColor = homeStation != null ? homeStation.stationColor : color;
            //         pathVisualizer.UpdatePathVisualization(currentPath, visualColor);
            //     }
            // }
            // else
            // {
            //     // 重新计算路径
            //     if (isReturning)
            //     {
            //         CalculatePathTo(homeStation.transform.position);
            //     }
            //     else if (targetShelf != null)
            //     {
            //         CalculatePathTo(GetAccessPointForShelf(targetShelf));
            //     }
            // }
        }
    }
    private void DeliverCargo()
    {
        hasCargo = false;
        hasTask = false;
        isReturning = false;
        UpdateColor(Color.white);
        homeStation.AddRobotToIdleList(this);
        currentTask.station.TaskCompleted(); // 通知任务完成
        currentTask.station.GenerateNewTask(targetShelf); // 生成新任务
    }
    // 修改检测路径阻塞的方法
    private bool CheckPathObstruction()
    {
        if (currentPath == null) return false;
        foreach (var cell in currentPath)
        {
            // 使用新的可访问性检查
            if (!cell.IsAccessibleFor(gameObject))
                return true;
        }
        return false;
    }
    private void PickUpCargo()
    {
        hasCargo = true;
        UpdateColor(Color.red);
        targetShelf.SetHighlight(false);
    }
    private List<GridCell> DetectChangedCells()
    {
        // 实现变化检测逻辑（示例检测周围3x3区域）
        List<GridCell> changed = new List<GridCell>();
        foreach (var cell in gridManager.GetNeighbors(currentCell))
        {
            if (cell.occupyingObject != null || !cell.walkable)
                changed.Add(cell);
        }
        return changed;
    }

    private Vector3 GetAccessPointForShelf(Shelf shelf, bool findAlternative = false)
    {
        if (shelf == null)
            return Vector3.zero;
        
        // 获取货架所在的网格
        GridCell shelfCell = gridManager.GetCellFromWorldPoint(shelf.transform.position);
        if (shelfCell == null)
            return shelf.transform.position;
        
        // 获取货架的碰撞体尺寸来估计货架占用的网格数量
        Collider shelfCollider = shelf.GetComponent<Collider>();
        int shelfSizeX = 1; // 默认值
        
        if (shelfCollider != null)
        {
            shelfSizeX = Mathf.CeilToInt(shelfCollider.bounds.size.x / gridManager.cellSize);
        }
        
        // 根据货架名称决定访问方向
        string shelfName = shelf.name.ToLower();
        int xOffset = 0;
        
        // Shelf0/2/3在X负方向侧访问，Shelf1/4/5在X正方向侧访问
        if (shelfName.Contains("shelf0") || shelfName.Contains("shelf2") || shelfName.Contains("shelf3"))
        {
            xOffset = -(shelfSizeX + 1); // X负方向，额外偏移1格确保不会碰到货架
        }
        else // Shelf1/4/5
        {
            xOffset = (shelfSizeX + 1); // X正方向，额外偏移1格确保不会碰到货架
        }
        
        // 获取网格尺寸以确保位置有效
        Vector2Int gridSize = gridManager.GetGridSize();
        
        // 检查访问点是否有效
        if (IsValidAccessPoint(shelfCell.gridX + xOffset, shelfCell.gridZ, gridSize))
        {
            GridCell accessCell = gridManager.grid[shelfCell.gridX + xOffset, shelfCell.gridZ];
            return accessCell.worldPosition;
        }
        
        // 如果请求寻找替代点，寻找附近可行走的点
        if (findAlternative)
        {
            // 在周围5格范围内寻找可行走点
            for (int radius = 1; radius <= 5; radius++)
            {
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    for (int offsetZ = -radius; offsetZ <= radius; offsetZ++)
                    {
                        // 跳过中心点
                        if (offsetX == 0 && offsetZ == 0) continue;
                        
                        int testX = shelfCell.gridX + offsetX;
                        int testZ = shelfCell.gridZ + offsetZ;
                        
                        if (IsValidAccessPoint(testX, testZ, gridSize))
                        {
                            GridCell alternativeCell = gridManager.grid[testX, testZ];
                            if (alternativeCell.walkable && !alternativeCell.isShelfAdjacent)
                            {
                                return alternativeCell.worldPosition;
                            }
                        }
                    }
                }
            }
        }
        
        // 如果所有尝试都失败，返回货架位置
        return shelf.transform.position;
    }

    // 辅助方法：检查指定网格坐标是否是有效的访问点
    private bool IsValidAccessPoint(int x, int z, Vector2Int gridSize)
    {
        if (x < 0 || x >= gridSize.x || z < 0 || z >= gridSize.y)
            return false;
        
        GridCell cell = gridManager.grid[x, z];
        return cell.walkable && !cell.isShelfAdjacent && cell.occupyingObject == null;
    }
    // 添加辅助方法，用于寻找最近的可行走单元格
    private GridCell FindNearestWalkableCell(Vector3 position, int searchRadius)
    {
        GridCell centerCell = gridManager.GetCellFromWorldPoint(position);
        
        // 按距离排序的单元格列表
        List<GridCell> cellsByDistance = new List<GridCell>();
        
        // 在指定半径内搜索
        for (int x = -searchRadius; x <= searchRadius; x++)
        {
            for (int z = -searchRadius; z <= searchRadius; z++)
            {
                int checkX = centerCell.gridX + x;
                int checkZ = centerCell.gridZ + z;
                
                Vector2Int gridSize = gridManager.GetGridSize();
                if (checkX >= 0 && checkX < gridSize.x && checkZ >= 0 && checkZ < gridSize.y)
                {
                    GridCell cell = gridManager.GetGrid()[checkX, checkZ];
                    if (cell.walkable && cell.occupyingObject == null)
                    {
                        cellsByDistance.Add(cell);
                    }
                }
            }
        }
        
        // 按与中心点的距离排序
        cellsByDistance.Sort((a, b) => 
            Vector3.Distance(a.worldPosition, position).CompareTo(
            Vector3.Distance(b.worldPosition, position)));
        
        // 返回最近的可行走单元格，如果没有则返回中心单元格
        return cellsByDistance.Count > 0 ? cellsByDistance[0] : centerCell;
    }
}


public class PriorityQueue<T>
{
    private List<T> elements = new List<T>();
    private Func<T, float[]> keySelector;
    private Comparison<float[]> comparer;
    private Dictionary<T, int> elementIndices = new Dictionary<T, int>(); // 跟踪元素位置
    
    public int Count => elements.Count;
    
    public PriorityQueue(Func<T, float[]> keySelector, Comparison<float[]> comparer)
    {
        this.keySelector = keySelector;
        this.comparer = comparer;
    }

    public T Peek()
    {
        if (elements.Count == 0)
            throw new InvalidOperationException("Queue is empty.");
        return elements[0];
    }

    public bool Contains(T item)
    {
        return elementIndices.ContainsKey(item);
    }

    public void Remove(T item)
    {
        // 使用字典直接查找元素索引，无需使用IndexOf
        if (!elementIndices.TryGetValue(item, out int index))
            return; // 元素不在队列中
        
        try
        {
            // 特殊情况：如果只剩一个元素
            if (elements.Count == 1)
            {
                elements.Clear();
                elementIndices.Clear();
                return;
            }
            
            // 将最后一个元素移到被删除元素的位置
            T lastElement = elements[elements.Count - 1];
            elements[index] = lastElement;
            elementIndices[lastElement] = index;
            
            // 移除最后一个元素
            elements.RemoveAt(elements.Count - 1);
            elementIndices.Remove(item);
            
            // 如果删除后队列为空，直接返回
            if (elements.Count == 0)
                return;
            
            // 调整堆结构
            HeapifyDown(index);
        }
        catch (Exception e)
        {
            Debug.LogError($"PriorityQueue.Remove 错误: {e.Message}\n索引: {index}, 数量: {elements.Count}");
            
            // 如果出现任何问题，重建整个队列
            RebuildQueue(item);
        }
    }

    private void RebuildQueue(T itemToRemove)
    {
        // 备份当前元素
        List<T> backup = new List<T>(elements);
        elements.Clear();
        elementIndices.Clear();
        
        // 重新入队所有元素（除了要移除的元素）
        foreach (T element in backup)
        {
            if (!element.Equals(itemToRemove))
                Enqueue(element);
        }
    }

    public void Enqueue(T item)
    {
        elements.Add(item);
        int index = elements.Count - 1;
        elementIndices[item] = index;
        
        HeapifyUp(index);
    }

    public T Dequeue()
    {
        if (elements.Count == 0)
            throw new InvalidOperationException("队列为空");
            
        T result = elements[0];
        
        // 如果只有一个元素，直接清空
        if (elements.Count == 1)
        {
            elements.Clear();
            elementIndices.Clear();
            return result;
        }
        
        try
        {
            // 将最后一个元素移到第一个位置
            T lastElement = elements[elements.Count - 1];
            elements[0] = lastElement;
            elementIndices[lastElement] = 0;
            
            // 移除最后一个元素和要出队的元素
            elements.RemoveAt(elements.Count - 1);
            elementIndices.Remove(result);
            
            // 调整堆
            HeapifyDown(0);
        }
        catch (Exception e)
        {
            Debug.LogError($"PriorityQueue.Dequeue 错误: {e.Message}");
            
            // 如果出现任何问题，重建队列
            RebuildQueue(result);
        }
        
        return result;
    }

    private void HeapifyUp(int index)
    {
        while (index > 0)
        {
            int parent = (index - 1) / 2;
            
            // 如果当前元素比父元素小，交换位置
            if (comparer(keySelector(elements[index]), keySelector(elements[parent])) < 0)
            {
                Swap(index, parent);
                index = parent;
            }
            else
                break;
        }
    }

    private void HeapifyDown(int index)
    {
        int lastIndex = elements.Count - 1;
        
        while (true)
        {
            int left = 2 * index + 1;
            int right = 2 * index + 2;
            int smallest = index;
            
            // 查找最小子节点
            if (left <= lastIndex && 
                comparer(keySelector(elements[left]), keySelector(elements[smallest])) < 0)
                smallest = left;
            
            if (right <= lastIndex && 
                comparer(keySelector(elements[right]), keySelector(elements[smallest])) < 0)
                smallest = right;
            
            // 如果当前节点最小，结束
            if (smallest == index)
                break;
            
            Swap(index, smallest);
            index = smallest;
        }
    }

    private void Swap(int i, int j)
    {
        // 交换元素
        T temp = elements[i];
        elements[i] = elements[j];
        elements[j] = temp;
        
        // 更新索引字典
        elementIndices[elements[i]] = i;
        elementIndices[elements[j]] = j;
    }
}
