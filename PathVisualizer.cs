using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathVisualizer : MonoBehaviour
{
    [Header("可视化设置")]
    public float lineWidth = 0.1f;
    public float lineHeight = 0.05f; // 路径高于地面的高度
    public bool fadePathEnd = true;  // 路径末端是否渐变透明
    public bool showCompletedPath = true; // 显示已完成任务的路径
    
    private LineRenderer lineRenderer;
    private Color pathColor;
    
    private void Awake()
    {
        // 获取或添加LineRenderer组件
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            SetupLineRenderer();
        }
    }
    
    private void SetupLineRenderer()
    {
        // 设置LineRenderer的基本属性
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth * 0.7f; // 路径尾部稍细，增强视觉效果
        lineRenderer.positionCount = 0;
        
        // 使用Unity默认的线材质
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.useWorldSpace = true;
    }
    
    // 更新路径显示
    public void UpdatePathVisualization(List<GridCell> path, Color color)
    {
        if (path == null || path.Count <= 1)
        {
            return; // 保留当前路径，不清除
        }
        
        pathColor = color;
        lineRenderer.startColor = pathColor;
        
        // 如果启用路径末端渐变
        if (fadePathEnd)
        {
            Color endColor = pathColor;
            endColor.a = 0.2f; // 设置透明度
            lineRenderer.endColor = endColor;
        }
        else
        {
            lineRenderer.endColor = pathColor;
        }
        
        // 设置路径点
        lineRenderer.positionCount = path.Count;
        for (int i = 0; i < path.Count; i++)
        {
            // 确保路径点高于地面，避免与地面重叠
            Vector3 pointPosition = path[i].worldPosition + Vector3.up * lineHeight;
            lineRenderer.SetPosition(i, pointPosition);
        }
    }
    
    // 清除路径 - 只在新任务开始时调用
    public void ClearPath()
    {
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
        }
    }
    
    // 标记已完成的路径段
    public void MarkCompletedPathSegment(int upToIndex)
    {
        if (lineRenderer == null || lineRenderer.positionCount <= 1 || upToIndex <= 0)
            return;
            
        // 创建渐变颜色，已完成的段落更透明
        Gradient gradient = new Gradient();
        
        // 设置两个关键点 - 已完成/未完成分界点颜色变化
        float transitionPoint = Mathf.Clamp01((float)upToIndex / (lineRenderer.positionCount - 1));
        
        GradientColorKey[] colorKeys = new GradientColorKey[2];
        colorKeys[0].color = pathColor;
        colorKeys[0].time = 0f;
        colorKeys[1].color = pathColor;
        colorKeys[1].time = 1f;
        
        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[3];
        // alphaKeys[0].alpha = 0f; // 已完成路径透明度
        // alphaKeys[0].time = 0f;
        // alphaKeys[1].alpha = 0f; // 过渡点最不透明
        // alphaKeys[1].time = transitionPoint;
        // alphaKeys[2].alpha = 0f; // 未完成路径半透明
        // alphaKeys[2].time = 0f;

        alphaKeys[0].alpha = 0.3f; // 已完成路径透明度
        alphaKeys[0].time = 0f;
        alphaKeys[1].alpha = 1.0f; // 过渡点最不透明
        alphaKeys[1].time = transitionPoint;
        alphaKeys[2].alpha = 0.7f; // 未完成路径半透明
        alphaKeys[2].time = 1f;
        
        gradient.SetKeys(colorKeys, alphaKeys);
        lineRenderer.colorGradient = gradient;
    }
}