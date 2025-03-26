using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shelf : MonoBehaviour
{
    private Color originalColor;
    private new Renderer renderer;
    private GridManager gridManager;
    private Coroutine highlightCoroutine;

    // 存储所有被该货架占用的单元格
    private List<GridCell> occupiedCells = new List<GridCell>();

    private void Start()
    {
        renderer = GetComponent<Renderer>();
        originalColor = renderer.material.color;
        
        // 通过GridManager获取对应的GridCell
        gridManager = FindObjectOfType<GridManager>();
        if (gridManager != null)
        {
            MarkOccupiedCells();
        }
    }
    
    // 标记货架占据的所有网格单元格
    private void MarkOccupiedCells()
    {
        // 获取货架的碰撞体
        Collider shelfCollider = GetComponent<Collider>();
        if (shelfCollider == null)
        {
            Debug.LogWarning($"货架 {gameObject.name} 没有碰撞体组件，无法确定占用范围");
            return;
        }
        
        // 获取碰撞体的边界框
        Bounds bounds = shelfCollider.bounds;
        
        // 计算边界框在网格坐标系中的最小和最大点
        Vector3 minPoint = bounds.min;
        Vector3 maxPoint = bounds.max;
        
        // 获取对应网格单元格
        GridCell minCell = gridManager.GetCellFromWorldPoint(minPoint);
        GridCell maxCell = gridManager.GetCellFromWorldPoint(maxPoint);
        
        if (minCell == null || maxCell == null)
        {
            Debug.LogWarning($"货架 {gameObject.name} 位置超出网格范围");
            return;
        }
        
        // 遍历并标记所有被货架覆盖的单元格
        for (int x = minCell.gridX; x <= maxCell.gridX; x++)
        {
            for (int z = minCell.gridZ; z <= maxCell.gridZ; z++)
            {
                GridCell cell = gridManager.GetCellFromGrid(x, z);
                if (cell != null)
                {
                    cell.walkable = false;
                    cell.occupyingObject = gameObject;
                    occupiedCells.Add(cell);
                    
                    // 更新单元格的视觉表示
                    if (cell.visualObject != null)
                    {
                        Renderer cellRenderer = cell.visualObject.GetComponent<Renderer>();
                        if (cellRenderer != null)
                        {
                            Color unwalkableColor = gridManager.unwalkableColor;
                            unwalkableColor.a = gridManager.Alpha;
                            cellRenderer.material.color = unwalkableColor;
                        }
                    }
                }
            }
        }
        
        Debug.Log($"货架 {gameObject.name} 占据了 {occupiedCells.Count} 个网格单元格");
    }

    // 添加平滑高亮过渡动画
    public void SetHighlight(bool highlight)
    {
        // 停止现有协程
        if (highlightCoroutine != null)
            StopCoroutine(highlightCoroutine);
            
        // 启动新协程
        highlightCoroutine = StartCoroutine(SmoothHighlight(highlight ? Color.red : originalColor, 0.5f));
    }
    
    private IEnumerator SmoothHighlight(Color targetColor, float duration)
    {
        Color startColor = renderer.material.color;
        float time = 0;
        
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration; // 归一化时间
            renderer.material.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }
        
        renderer.material.color = targetColor;
    }
    
    // 获取任意GameObject对应的GridCell的便捷方法
    public static GridCell GetCellForObject(GameObject obj)
    {
        GridManager gridManager = FindObjectOfType<GridManager>();
        if (gridManager != null)
        {
            return gridManager.GetCellFromWorldPoint(obj.transform.position);
        }
        return null;
    }
}