using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObstacleDetector : MonoBehaviour
{
    private GridManager gridManager;
    
    private void OnTriggerEnter(Collider other)
    {
        GridCell cell = gridManager.GetCellFromWorldPoint(transform.position);
        cell.walkable = false;
        
        // 通知相关机器人更新路径
        Robot[] robots = FindObjectsOfType<Robot>();
        foreach (var robot in robots)
        {
            robot.NotifyEnvironmentChange(new List<GridCell>{cell});
        }
    }
}