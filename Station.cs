using System.Collections;
using System.Collections.Generic;
using Object = UnityEngine.Object;  // 添加这一行
using UnityEngine;
using System;

// 新增Task类用于表示任务
public class Task
{
    public Shelf targetShelf;
    public Station station;
    public int priority = 1; // 优先级（值越大优先级越高）
    public float assignTime; // 任务分配时间
    public float estimatedCompletionTime; // 预计完成时间
}

// 任务分配策略接口
public interface ITaskAllocationStrategy
{
    Robot AllocateTask(Task task, List<Robot> availableRobots, Station station);
}

// 基于最短距离的任务分配策略
public class NearestRobotStrategy : ITaskAllocationStrategy
{
    public Robot AllocateTask(Task task, List<Robot> availableRobots, Station station)
    {
        if (availableRobots.Count == 0) return null;
        
        Robot bestRobot = null;
        float minCost = float.MaxValue;
        
        foreach (Robot robot in availableRobots)
        {
            float cost = Vector3.Distance(robot.transform.position, task.targetShelf.transform.position);
            if (cost < minCost)
            {
                minCost = cost;
                bestRobot = robot;
            }
        }
        
        return bestRobot;
    }
}

// 基于负载均衡的任务分配策略
public class LoadBalancingStrategy : ITaskAllocationStrategy
{
    public Robot AllocateTask(Task task, List<Robot> availableRobots, Station station)
    {
        if (availableRobots.Count == 0) return null;
        
        // 创建一个包含每个机器人负载的字典（以homeStation为键）
        Dictionary<Station, int> stationLoadCount = new Dictionary<Station, int>();
        foreach (Robot robot in UnityEngine.Object.FindObjectsOfType<Robot>())
        {
            if (robot.homeStation != null && robot.hasTask)
            {
                if (!stationLoadCount.ContainsKey(robot.homeStation))
                {
                    stationLoadCount[robot.homeStation] = 0;
                }
                stationLoadCount[robot.homeStation]++;
            }
        }
        
        // 根据所属站点的负载和距离计算成本
        Robot bestRobot = null;
        float minCost = float.MaxValue;
        
        foreach (Robot robot in availableRobots)
        {
            float distance = Vector3.Distance(robot.transform.position, task.targetShelf.transform.position);
            int stationLoad = stationLoadCount.ContainsKey(robot.homeStation) ? stationLoadCount[robot.homeStation] : 0;
            
            // 计算综合成本（距离 + 站点负载的加权值）
            float cost = distance + stationLoad * 5f;
            
            if (cost < minCost)
            {
                minCost = cost;
                bestRobot = robot;
            }
        }
        
        return bestRobot;
    }
}

// 综合策略（结合距离、负载和电量）
public class HybridStrategy : ITaskAllocationStrategy
{
    public Robot AllocateTask(Task task, List<Robot> availableRobots, Station station)
    {
        if (availableRobots.Count == 0) return null;
        
        Dictionary<Station, int> stationLoadCount = new Dictionary<Station, int>();
        foreach (Robot robot in UnityEngine.Object.FindObjectsOfType<Robot>())
        {
            if (robot.homeStation != null && robot.hasTask)
            {
                if (!stationLoadCount.ContainsKey(robot.homeStation))
                {
                    stationLoadCount[robot.homeStation] = 0;
                }
                stationLoadCount[robot.homeStation]++;
            }
        }
        
        Robot bestRobot = null;
        float minCost = float.MaxValue;
        
        foreach (Robot robot in availableRobots)
        {
            float distance = Vector3.Distance(robot.transform.position, task.targetShelf.transform.position);
            int stationLoad = stationLoadCount.ContainsKey(robot.homeStation) ? stationLoadCount[robot.homeStation] : 0;
            
            // 模拟电量因素（这里用距离站点的远近作为电量的近似）
            float batteryLevel = 1.0f - Vector3.Distance(robot.transform.position, robot.homeStation.transform.position) / 50f;
            batteryLevel = Mathf.Clamp01(batteryLevel);
            
            // 计算综合成本
            float distanceWeight = 1.0f;
            float loadWeight = 3.0f;
            float batteryWeight = 2.0f;
            
            float cost = distanceWeight * distance + 
                        loadWeight * stationLoad + 
                        batteryWeight * (1.0f - batteryLevel);
            
            if (cost < minCost)
            {
                minCost = cost;
                bestRobot = robot;
            }
        }
        
        return bestRobot;
    }
}

public class Station : MonoBehaviour
{
    [Header("基本设置")]
    public Color stationColor; // Station的颜色
    public int robotCount = 3; // 初始化的Robot数量
    public GameObject robotPrefab; // AGV预制体
    
    [Header("任务配置")]
    public float taskGenerationInterval = 10f; // 自动生成任务的间隔时间
    public bool autoGenerateTasks = true; // 是否自动生成任务
    public int maxQueuedTasks = 10; // 最大队列任务数
    
    [Header("任务分配策略")]
    public TaskAllocationStrategyType taskAllocationStrategy = TaskAllocationStrategyType.Nearest;
    
    // 不同的任务分配策略枚举
    public enum TaskAllocationStrategyType
    {
        Nearest,
        LoadBalancing,
        Hybrid
    }
    
    private GridManager gridManager;
    private UIManager uiManager;
    private ITaskAllocationStrategy currentStrategy;
    
    // 公开属性以便UI访问
    public List<Task> taskQueue = new List<Task>();
    private List<Robot> idleRobots = new List<Robot>();
    public int completedTaskCount = 0;
    
    private float lastTaskGenerationTime;
    private Dictionary<TaskAllocationStrategyType, ITaskAllocationStrategy> strategies = 
        new Dictionary<TaskAllocationStrategyType, ITaskAllocationStrategy>();

    private void Start()
    {
        gridManager = FindObjectOfType<GridManager>();
        uiManager = FindObjectOfType<UIManager>();
        
        // 初始化所有策略
        strategies[TaskAllocationStrategyType.Nearest] = new NearestRobotStrategy();
        strategies[TaskAllocationStrategyType.LoadBalancing] = new LoadBalancingStrategy();
        strategies[TaskAllocationStrategyType.Hybrid] = new HybridStrategy();
        
        // 设置当前策略
        SetTaskAllocationStrategy(taskAllocationStrategy);
        
        // 初始化机器人
        InitializeRobots();
        
        lastTaskGenerationTime = Time.time;
    }
    // 处理任务失败的情况
    public void TaskFailed(Task task)
    {
        if (task != null)
        {
            // 重新将任务加入队列，优先级提高
            task.priority += 1; // 提高优先级
            taskQueue.Add(task);
            Debug.Log($"站点 {name} 收到任务失败通知，任务重新入队，优先级提高为 {task.priority}");
            
            // 根据情况可能需要重新排序队列
            taskQueue.Sort((t1, t2) => t2.priority.CompareTo(t1.priority));
        }
    }
    // 添加强制分配任务的方法
    public void ForceAssignTasks()
    {
        Debug.Log($"Station {name} 强制分配任务，队列中任务: {taskQueue.Count}, 空闲机器人: {idleRobots.Count}");
        AssignTasks();
    }
    private void Update()
    {
        // 分配任务
        if (taskQueue.Count > 0 && idleRobots.Count > 0)
        {
            Debug.Log($"Station {name}: 尝试分配任务. 队列中任务: {taskQueue.Count}, 空闲机器人: {idleRobots.Count}");
        }
        // 分配任务
        AssignTasks();
        
        // 如果启用了自动生成任务
        if (autoGenerateTasks && Time.time - lastTaskGenerationTime > taskGenerationInterval)
        {
            Debug.Log($"Station {name}: 尝试自动生成任务");
            AutoGenerateTask();
            lastTaskGenerationTime = Time.time;
        }
    }
    
    // 设置任务分配策略
    public void SetTaskAllocationStrategy(TaskAllocationStrategyType strategyType)
    {
        taskAllocationStrategy = strategyType;
        if (strategies.ContainsKey(strategyType))
        {
            currentStrategy = strategies[strategyType];
            Debug.Log($"Station {name} switched to {strategyType} task allocation strategy");
        }
        else
        {
            Debug.LogError($"Strategy {strategyType} not implemented!");
            currentStrategy = strategies[TaskAllocationStrategyType.Nearest]; // 默认使用最近策略
        }
    }

    // 任务完成回调
    public void TaskCompleted()
    {
        completedTaskCount++;
        if (uiManager != null)
        {
            uiManager.IncrementTaskCompleted();
        }
    }

    // 任务分配方法
    private void AssignTasks()
    {
        // 按优先级排序任务队列
        taskQueue.Sort((t1, t2) => t2.priority.CompareTo(t1.priority));
        
        while (taskQueue.Count > 0 && idleRobots.Count > 0)
        {
            Task task = taskQueue[0];
            
            // 使用当前策略分配任务
            Robot bestRobot = currentStrategy.AllocateTask(task, idleRobots, this);
            
            if (bestRobot != null)
            {
                // 记录分配时间和预计完成时间
                task.assignTime = Time.time;
                float estimatedDistance = Vector3.Distance(bestRobot.transform.position, task.targetShelf.transform.position) +
                                         Vector3.Distance(task.targetShelf.transform.position, transform.position);
                task.estimatedCompletionTime = task.assignTime + estimatedDistance / bestRobot.moveSpeed;
                
                // 分配任务给机器人
                bestRobot.AssignTask(task);
                idleRobots.Remove(bestRobot);
                taskQueue.RemoveAt(0);
                
                Debug.Log($"任务分配给机器人 {bestRobot.name}，预计完成时间: {task.estimatedCompletionTime-task.assignTime:F2}秒");
            }
            else
            {
                // 如果无法分配任务，退出循环
                break;
            }
        }
    }

    // 将机器人添加到空闲列表
    public void AddRobotToIdleList(Robot robot)
    {
        if (!idleRobots.Contains(robot))
            idleRobots.Add(robot);
    }

    // 生成新任务
    public void GenerateNewTask(Shelf shelf, int priority = 1)
    {
        // 检查任务队列是否已满
        if (taskQueue.Count >= maxQueuedTasks)
        {
            Debug.LogWarning($"Station {name} 任务队列已满!");
            return;
        }
        
        Task newTask = new Task 
        { 
            targetShelf = shelf,
            station = this,
            priority = priority
        };
        
        taskQueue.Add(newTask);
        
        // 通知 UIManager 新增任务
        if (uiManager != null)
        {
            uiManager.AddNewTask();
        }
        
        Debug.Log($"Station {name} 创建了新任务，目标货架: {shelf.name}，优先级: {priority}");
    }
    
    // 自动生成任务
    private void AutoGenerateTask()
    {
        // 找到一个随机的货架
        Shelf[] allShelves = FindObjectsOfType<Shelf>();
        if (allShelves.Length > 0)
        {
            Shelf randomShelf = allShelves[UnityEngine.Random.Range(0, allShelves.Length)];
            int priority = UnityEngine.Random.Range(1, 4); // 1-3的随机优先级
            GenerateNewTask(randomShelf, priority);
        }
    }

    // 初始化Robot
    private void InitializeRobots()
    {
        GridCell stationCell = gridManager.GetCellFromWorldPoint(transform.position);

        // 在Station周围生成Robot
        for (int i = 0; i < robotCount; i++)
        {
            GridCell spawnCell = FindWalkableNeighbor(stationCell);
            if (spawnCell != null)
            {
                GameObject robotObj = Instantiate(robotPrefab, spawnCell.worldPosition, Quaternion.identity);
                Robot robot = robotObj.GetComponent<Robot>();
                if (robot == null)
                {
                    Debug.LogError("Robot预制体必须包含Robot组件!");
                }
                else
                {
                    robot.name = $"Robot_{name}_{i}";
                    robot.Initialize(this, spawnCell);
                    
                    // 添加到空闲列表
                    idleRobots.Add(robot);
                }
            }
        }
    }

    // 查找附近可行走的网格
    private GridCell FindWalkableNeighbor(GridCell centerCell)
    {
        List<GridCell> neighbors = gridManager.GetNeighbors(centerCell);
        // 随机打乱顺序以获得更好的分布
        ShuffleList(neighbors);
        
        foreach (GridCell neighbor in neighbors)
        {
            if (neighbor.walkable && neighbor.occupyingObject == null)
            {
                return neighbor;
            }
        }
        
        // 如果一环没有找到，扩大搜索范围
        List<GridCell> extendedNeighbors = new List<GridCell>();
        foreach (GridCell neighbor in neighbors)
        {
            extendedNeighbors.AddRange(gridManager.GetNeighbors(neighbor));
        }
        
        ShuffleList(extendedNeighbors);
        foreach (GridCell neighbor in extendedNeighbors)
        {
            if (neighbor.walkable && neighbor.occupyingObject == null)
            {
                return neighbor;
            }
        }
        
        return null;
    }
    
    // 辅助方法：打乱列表
    private void ShuffleList<T>(List<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = UnityEngine.Random.Range(0, n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
}