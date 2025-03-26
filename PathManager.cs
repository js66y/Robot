using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathManager : MonoBehaviour
{
    [Header("全局路径设置")]
    public bool showAllPaths = true;
    public float defaultLineWidth = 0.1f;
    public float refreshInterval = 1f; // 路径刷新间隔，减少性能开销
    
    private List<PathVisualizer> allVisualizers = new List<PathVisualizer>();
    private float lastRefreshTime;
    
    private void Start()
    {
        lastRefreshTime = Time.time;
        RefreshVisualizersList();
    }
    
    private void Update()
    {
        // 定期刷新可视化器列表，查找新创建的机器人
        if (Time.time - lastRefreshTime > refreshInterval)
        {
            RefreshVisualizersList();
            lastRefreshTime = Time.time;
        }
    }
    
    // 刷新可视化器列表
    private void RefreshVisualizersList()
    {
        allVisualizers.Clear();
        PathVisualizer[] visualizers = FindObjectsOfType<PathVisualizer>();
        allVisualizers.AddRange(visualizers);
        
        // 应用全局设置
        foreach (var visualizer in allVisualizers)
        {
            visualizer.gameObject.SetActive(showAllPaths);
            visualizer.lineWidth = defaultLineWidth;
        }
    }
    
    // 清除所有路径历史
    public void ClearAllPaths()
    {
        foreach (var visualizer in allVisualizers)
        {
            visualizer.ClearPath();
        }
    }
    
    // 设置所有路径的可见性
    public void SetAllPathsVisibility(bool visible)
    {
        showAllPaths = visible;
        foreach (var visualizer in allVisualizers)
        {
            visualizer.gameObject.SetActive(visible);
        }
    }
    
    // 在UI中添加切换按钮
    public void ToggleAllPaths()
    {
        SetAllPathsVisibility(!showAllPaths);
    }
    // 仅清除指定机器人的路径
    public void ClearRobotPath(Robot robot)
    {
        PathVisualizer visualizer = robot.GetComponentInChildren<PathVisualizer>();
        if (visualizer != null)
        {
            visualizer.ClearPath();
        }
    }
    // 清除所有非活动任务的路径
    public void ClearInactiveRobotPaths()
    {
        Robot[] robots = FindObjectsOfType<Robot>();
        foreach (Robot robot in robots)
        {
            if (!robot.hasTask)
            {
                PathVisualizer visualizer = robot.GetComponentInChildren<PathVisualizer>();
                if (visualizer != null)
                {
                    visualizer.ClearPath();
                }
            }
        }
    }
}