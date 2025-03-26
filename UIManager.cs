using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class UIManager : MonoBehaviour
{
    [Header("UI 设置")]
    public bool showDebugInfo = true;
    public GUISkin customSkin;
    public float windowWidth = 250f;
    public float robotStatusHeight = 35f;
    public float stationStatusHeight = 40f;
    
    [Header("参考")]
    public GridManager gridManager;
    [Header("路径控制")]
    public PathManager pathManager;
    [Header("寻路设置")]
    public PathfindingManager pathfindingManager;
    private float simulationTime = 0f;
    private int totalTasks = 0;
    private int completedTasks = 0;
    private float lastUpdateTime;
    private Robot[] robots;
    private Station[] stations;
    private Dictionary<Station, int> assignedTasksPerStation = new Dictionary<Station, int>();


    private void Start()
    {
        // 如果没有指定gridManager，尝试查找
        if (gridManager == null)
            gridManager = FindObjectOfType<GridManager>();
        // 如果没有指定pathManager，尝试查找
        if (pathManager == null)
            pathManager = FindObjectOfType<PathManager>();
        // 获取所有机器人和站点
        UpdateEntities();
        
        lastUpdateTime = Time.time;
        // 如果没有指定pathfindingManager，尝试查找
        if (pathfindingManager == null)
            pathfindingManager = FindObjectOfType<PathfindingManager>();
    }
    
    private void Update()
    {
        // 更新模拟时间
        simulationTime += Time.deltaTime;
        
        // 每秒更新一次实体列表
        if (Time.time - lastUpdateTime > 1f)
        {
            UpdateEntities();
            lastUpdateTime = Time.time;
        }
    }
    
    private void UpdateEntities()
    {
        robots = FindObjectsOfType<Robot>();
        stations = FindObjectsOfType<Station>();
        
        // 更新每个站点的已分配任务计数
        UpdateAssignedTasksCount();
    }
    
    // 更新每个站点已分配的任务数
    private void UpdateAssignedTasksCount()
    {
        assignedTasksPerStation.Clear();
        
        foreach (Station station in stations)
        {
            assignedTasksPerStation[station] = 0;
        }
        
        foreach (Robot robot in robots)
        {
            if (robot.hasTask && robot.homeStation != null)
            {
                if (assignedTasksPerStation.ContainsKey(robot.homeStation))
                {
                    assignedTasksPerStation[robot.homeStation]++;
                }
            }
        }
    }
    
    public void IncrementTaskCompleted()
    {
        completedTasks++;
        totalTasks = Math.Max(totalTasks, completedTasks);
    }
    
    public void AddNewTask()
    {
        totalTasks++;
    }
    
    private void OnGUI()
    {
        if (customSkin != null)
            GUI.skin = customSkin;
            
        if (showDebugInfo)
            DrawDebugUI();
    }
    
    private void DrawDebugUI()
    {
        // 主信息窗口
        GUILayout.BeginArea(new Rect(10, 10, windowWidth, 120));
        GUILayout.BeginVertical("仓库机器人系统", "window");
        
        // 模拟时间
        TimeSpan timeSpan = TimeSpan.FromSeconds(simulationTime);
        GUILayout.Label($"模拟时间: {timeSpan.Hours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}");
        
        // 任务进度
        float progressValue = totalTasks > 0 ? (float)completedTasks / totalTasks : 0f;
        GUILayout.Label($"任务完成: {completedTasks}/{totalTasks} ({progressValue:P0})");
        
        // 进度条
        Rect progressRect = GUILayoutUtility.GetRect(windowWidth - 20, 20);
        GUI.Box(progressRect, "");
        
        
        // Rect filledRect = new Rect(progressRect.x, progressRect.y, progressRect.width * progressValue, progressRect.height);
        // GUI.Box(filledRect, "", "ProgressBarFill");
        // 使用颜色而非样式
        Color originalColor = GUI.color;
        GUI.color = new Color(0.2f, 0.7f, 0.2f); // 绿色填充
        Rect filledRect = new Rect(progressRect.x, progressRect.y, progressRect.width * progressValue, progressRect.height);
        GUI.Box(filledRect, "");
        GUI.color = originalColor;
        
        // 显示/隐藏网格按钮
        if (GUILayout.Button(gridManager.displayGrid ? "隐藏网格" : "显示网格"))
        {
            gridManager.ToggleGridDisplay(!gridManager.displayGrid);
        }
        // 添加显示/隐藏路径按钮
        if (pathManager != null)
        {
            if (GUILayout.Button(pathManager.showAllPaths ? "隐藏路径" : "显示路径"))
            {
                pathManager.ToggleAllPaths();
            }
            
            // 添加清除路径历史按钮
            if (GUILayout.Button("清除路径历史"))
            {
                pathManager.ClearAllPaths();
            }
            if (pathfindingManager != null)
            {
                // 显示当前算法
                GUILayout.Label($"当前寻路算法: {pathfindingManager.GetCurrentAlgorithmName()}");
                
                // 算法切换按钮
                GUILayout.BeginHorizontal();
                
                if (GUILayout.Button("A*算法"))
                {
                    pathfindingManager.SetAlgorithm(PathfindingManager.PathfindingAlgorithm.AStar);
                    gridManager.RefreshAllObjectPositions(); // 刷新所有物体位置
                }
                
                if (GUILayout.Button("D*算法"))
                {
                    pathfindingManager.SetAlgorithm(PathfindingManager.PathfindingAlgorithm.DStar);
                    gridManager.RefreshAllObjectPositions();
                }
                
                if (GUILayout.Button("D* Lite算法"))
                {
                    pathfindingManager.SetAlgorithm(PathfindingManager.PathfindingAlgorithm.DStarLite);
                    gridManager.RefreshAllObjectPositions();
                }
                
                GUILayout.EndHorizontal();
                
                // 添加手动刷新网格位置按钮
                if (GUILayout.Button("刷新网格位置"))
                {
                    gridManager.RefreshAllObjectPositions();
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        
        // 站点任务分配窗口
        if (stations != null && stations.Length > 0)
        {
            float stationWindowHeight = stations.Length * stationStatusHeight + 30;
            GUILayout.BeginArea(new Rect(10, 140, windowWidth, stationWindowHeight));
            GUILayout.BeginVertical("站点任务分配", "window");
            
            foreach (Station station in stations)
            {
                GUILayout.BeginHorizontal();
                
                // 显示站点颜色样本
                Rect colorRect = GUILayoutUtility.GetRect(20, 20);
                EditorGUI.DrawColorSwatch(colorRect, station.stationColor);
                
                // 站点任务信息
                int queuedTasks = station.taskQueue.Count;
                int assignedTasks = assignedTasksPerStation.ContainsKey(station) ? assignedTasksPerStation[station] : 0;
                
                GUILayout.Label($"站点 | 队列: {queuedTasks} | 已分配: {assignedTasks} | 已完成: {station.completedTaskCount}");

                GUILayout.EndHorizontal();
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        // 机器人状态窗口
        if (robots != null && robots.Length > 0)
        {
            float yOffset = stations != null ? 150 + stations.Length * stationStatusHeight + 30 : 140;
            float robotWindowHeight = robots.Length * robotStatusHeight + 30;
            GUILayout.BeginArea(new Rect(10, yOffset, windowWidth, robotWindowHeight));
            GUILayout.BeginVertical("机器人状态", "window");
            
            foreach (Robot robot in robots)
            {
                GUILayout.BeginHorizontal();
                
                // 显示机器人颜色样本
                Rect colorRect = GUILayoutUtility.GetRect(20, 20);
                EditorGUI.DrawColorSwatch(colorRect, robot.color);
                
                // 机器人状态
                string status = robot.hasTask ? (robot.hasCargo ? "运送货物中" : "前往取货中") : "空闲";
                GUILayout.Label($"机器人 | 状态: {status}");
                
                GUILayout.EndHorizontal();
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
    
    // 绘制颜色方块的辅助方法
    private static class EditorGUI
    {
        public static void DrawColorSwatch(Rect position, Color color)
        {
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.Box(position, "");
            GUI.color = oldColor;
        }
    }
}