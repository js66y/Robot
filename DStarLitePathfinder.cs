using System;
using System.Collections.Generic;
using UnityEngine;

public class DStarLitePathfinder : IPathfinder
{
    private const int MOVE_STRAIGHT_COST = 10;
    private GridManager gridManager;
    
    private class State
    {
        public int g; // 实际代价
        public int rhs; // 局部一致性值
        public int key1; // 优先级第一关键字
        public int key2; // 优先级第二关键字
    }
    
    private Dictionary<GridCell, State> _stateMap;
    private PriorityQueue<GridCell> _openList;
    private GridCell _start;
    private GridCell _goal;
    private int _km; // 累计启发式修正值
    private List<GridCell> _lastPath;
    
    public DStarLitePathfinder()
    {
        _stateMap = new Dictionary<GridCell, State>();
        _openList = new PriorityQueue<GridCell>();
        _km = 0;
        _lastPath = null;
    }
    
    public List<GridCell> FindPath(GridCell start, GridCell goal, GameObject requestingAgent = null)
    {
        if (start == null || goal == null)
            return null;
        
        bool needsInitialization = _start == null || _goal == null || _start != start || _goal != goal;
        
        if (needsInitialization)
        {
            Initialize(start, goal);
        }
        
        // 处理占用和障碍物
        HandleOccupiedCells(requestingAgent);
        
        // 执行主算法
        ComputeShortestPath();
        
        // 提取路径
        _lastPath = ExtractPath();
        return _lastPath;
    }
    public void Initialize(GridManager gridManager)
    {
        this.gridManager = gridManager;
        _stateMap.Clear();
        _openList.Clear();
        _km = 0;
        _lastPath = null;
        Debug.Log("D* Lite 寻路算法已初始化");
    }

    private GridCell GetCellAt(int x, int y)
    {
        return gridManager?.GetCellFromGrid(x, y);
    }
    private void Initialize(GridCell start, GridCell goal)
    {
        _start = start;
        _goal = goal;
        _km = 0;
        
        _stateMap.Clear();
        _openList.Clear();
        
        // 初始化目标
        State goalState = GetState(goal);
        goalState.rhs = 0;
        
        // 计算键值并加入OPEN列表
        UpdateKeys(goal);
        _openList.Enqueue(goal, new Tuple<int, int>(goalState.key1, goalState.key2));
    }
    
    private void HandleOccupiedCells(GameObject requestingAgent)
    {
        // 检查占用的单元格
        foreach (var entry in new Dictionary<GridCell, State>(_stateMap))
        {
            GridCell cell = entry.Key;
            if (!cell.isWalkable || (cell.occupiedBy != null && cell.occupiedBy != requestingAgent))
            {
                // 更新占用状态
                UpdateVertex(cell);
            }
        }
    }
    
    private void ComputeShortestPath()
    {
        while (_openList.Count > 0 && 
              (CompareTuples(_openList.TopPriority(), CalculateKeys(_start)) < 0 || 
               GetState(_start).rhs != GetState(_start).g))
        {
            var u = _openList.Dequeue();
            State uState = GetState(u);
            
            if (uState.g > uState.rhs)
            {
                // 更新节点值
                uState.g = uState.rhs;
                
                // 更新所有前驱
                foreach (var s in GetNeighbors(u))
                {
                    UpdateVertex(s);
                }
            }
            else
            {
                // 标记为不一致
                uState.g = int.MaxValue;
                
                // 更新节点本身和其前驱
                UpdateVertex(u);
                foreach (var s in GetNeighbors(u))
                {
                    UpdateVertex(s);
                }
            }
        }
    }
    
    private State GetState(GridCell cell)
    {
        if (!_stateMap.ContainsKey(cell))
        {
            _stateMap[cell] = new State { g = int.MaxValue, rhs = int.MaxValue };
        }
        
        return _stateMap[cell];
    }
    
    private void UpdateVertex(GridCell u)
    {
        if (u == _goal)
            return;
        
        State uState = GetState(u);
        
        // 移除旧键值
        if (_openList.Contains(u))
        {
            _openList.Remove(u);
        }
        
        // 计算新的rhs值
        if (IsObstacle(u))
        {
            uState.rhs = int.MaxValue;
        }
        else
        {
            // 找到最小后继
            int minRhs = int.MaxValue;
            foreach (var s in GetNeighbors(u))
            {
                int cost = CostBetween(u, s);
                if (cost < int.MaxValue)
                {
                    int val = GetState(s).g + cost;
                    minRhs = Math.Min(minRhs, val);
                }
            }
            uState.rhs = minRhs;
        }
        
        // 检查是否需要加入OPEN列表
        if (uState.g != uState.rhs)
        {
            UpdateKeys(u);
            _openList.Enqueue(u, new Tuple<int, int>(uState.key1, uState.key2));
        }
    }
    
    private void UpdateKeys(GridCell u)
    {
        State uState = GetState(u);
        int minVal = Math.Min(uState.g, uState.rhs);
        
        // 计算新的键值
        uState.key1 = minVal + Heuristic(u, _start) + _km;
        uState.key2 = minVal;
    }
    
    private Tuple<int, int> CalculateKeys(GridCell u)
    {
        State uState = GetState(u);
        int minVal = Math.Min(uState.g, uState.rhs);
        
        return new Tuple<int, int>(
            minVal + Heuristic(u, _start) + _km,
            minVal
        );
    }
    
    private int Heuristic(GridCell a, GridCell b)
    {
        // 使用曼哈顿距离
        return MOVE_STRAIGHT_COST * (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y));
    }
    
    private int CostBetween(GridCell a, GridCell b)
    {
        // 检查目标单元格是否可通行
        if (IsObstacle(b))
            return int.MaxValue;
        
        // 直线移动成本
        return MOVE_STRAIGHT_COST;
    }
    
    private bool IsObstacle(GridCell cell)
    {
        return !cell.isWalkable || cell.occupiedBy != null;
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
        GridCell neighbor = GetCellAt(x, y);
        if (neighbor != null)
            neighbors.Add(neighbor);
    }
    
    // private GridCell GetCellAt(int x, int y)
    // {
    //     // 示例实现，实际应从GridManager获取
    //     return GridManager.Instance?.GetCellAt(x, y);
    // }
    
    private int CompareTuples(Tuple<int, int> a, Tuple<int, int> b)
    {
        if (a.Item1 != b.Item1)
            return a.Item1 - b.Item1;
        return a.Item2 - b.Item2;
    }
    
    private List<GridCell> ExtractPath()
    {
        List<GridCell> path = new List<GridCell>();
        GridCell current = _start;
        
        if (GetState(current).rhs == int.MaxValue)
            return null; // 没有路径
        
        path.Add(current);
        
        while (current != _goal)
        {
            // 找到下一个最佳节点
            int minCost = int.MaxValue;
            GridCell next = null;
            
            foreach (var s in GetNeighbors(current))
            {
                int cost = CostBetween(current, s);
                if (cost < int.MaxValue)
                {
                    int val = cost + GetState(s).g;
                    if (val < minCost)
                    {
                        minCost = val;
                        next = s;
                    }
                }
            }
            
            if (next == null)
                break; // 无法继续
            
            current = next;
            path.Add(current);
            
            // 防止可能的循环
            if (path.Count > 1000)
                break;
        }
        
        return path;
    }
    
    public void HandleChangedCells(List<GridCell> changedCells)
    {
        if (changedCells == null || changedCells.Count == 0 || _start == null || _goal == null)
            return;
        
        // 累加启发式修正值
        _km += Heuristic(_lastPath[0], _start);
        
        // 更新起点
        _start = _lastPath[0];
        
        // 处理变化的单元格
        foreach (GridCell cell in changedCells)
        {
            UpdateVertex(cell);
            
            // 更新邻居节点
            foreach (var neighbor in GetNeighbors(cell))
            {
                UpdateVertex(neighbor);
            }
        }
        
        // 重新计算路径
        ComputeShortestPath();
    }
    
    public string GetAlgorithmName()
    {
        return "D* Lite Pathfinding";
    }
    
    // 优先队列实现
    private class PriorityQueue<T>
    {
        private List<KeyValuePair<T, Tuple<int, int>>> _elements = new List<KeyValuePair<T, Tuple<int, int>>>();
        private Dictionary<T, int> _lookup = new Dictionary<T, int>();
        
        public int Count { get { return _elements.Count; } }
        
        public void Clear()
        {
            _elements.Clear();
            _lookup.Clear();
        }
        public void Enqueue(T item, Tuple<int, int> priority)
        {
            if (_lookup.ContainsKey(item))
            {
                // 如果元素已经存在，更新其优先级
                int index = _lookup[item];
                _elements[index] = new KeyValuePair<T, Tuple<int, int>>(item, priority);
                BubbleUp(index);
            }
            else
            {
                // 添加新元素
                _elements.Add(new KeyValuePair<T, Tuple<int, int>>(item, priority));
                _lookup[item] = _elements.Count - 1;
                BubbleUp(_elements.Count - 1);
            }
        }
        
        public T Dequeue()
        {
            int lastIndex = _elements.Count - 1;
            T frontItem = _elements[0].Key;
            
            // 移除查找项
            _lookup.Remove(frontItem);
            
            // 将最后一个元素放到前面，并重新调整
            _elements[0] = _elements[lastIndex];
            _elements.RemoveAt(lastIndex);
            
            if (_elements.Count > 0)
            {
                _lookup[_elements[0].Key] = 0;
                BubbleDown(0);
            }
            
            return frontItem;
        }
        
        public bool Contains(T item)
        {
            return _lookup.ContainsKey(item);
        }
        
        public void Remove(T item)
        {
            if (!_lookup.ContainsKey(item))
                return;
            
            int index = _lookup[item];
            _lookup.Remove(item);
            
            // 如果是最后一项，直接移除
            if (index == _elements.Count - 1)
            {
                _elements.RemoveAt(index);
                return;
            }
            
            // 将最后一项移到当前位置
            _elements[index] = _elements[_elements.Count - 1];
            _elements.RemoveAt(_elements.Count - 1);
            
            if (_elements.Count > index)
            {
                _lookup[_elements[index].Key] = index;
                BubbleDown(index);
            }
        }
        
        public Tuple<int, int> TopPriority()
        {
            return _elements[0].Value;
        }
        
        private void BubbleUp(int index)
        {
            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;
                
                if (Compare(_elements[parentIndex].Value, _elements[index].Value) <= 0)
                    break;
                
                // 交换
                Swap(index, parentIndex);
                index = parentIndex;
            }
        }
        
        private void BubbleDown(int index)
        {
            int lastIndex = _elements.Count - 1;
            
            while (true)
            {
                int leftChildIndex = 2 * index + 1;
                int rightChildIndex = 2 * index + 2;
                int smallestIndex = index;
                
                // 找到最小的子节点
                if (leftChildIndex <= lastIndex && Compare(_elements[leftChildIndex].Value, _elements[smallestIndex].Value) < 0)
                    smallestIndex = leftChildIndex;
                
                if (rightChildIndex <= lastIndex && Compare(_elements[rightChildIndex].Value, _elements[smallestIndex].Value) < 0)
                    smallestIndex = rightChildIndex;
                
                if (smallestIndex == index)
                    break;
                
                // 交换
                Swap(index, smallestIndex);
                index = smallestIndex;
            }
        }
        
        private void Swap(int index1, int index2)
        {
            var temp = _elements[index1];
            _elements[index1] = _elements[index2];
            _elements[index2] = temp;
            
            _lookup[_elements[index1].Key] = index1;
            _lookup[_elements[index2].Key] = index2;
        }
        
        private int Compare(Tuple<int, int> a, Tuple<int, int> b)
        {
            if (a.Item1 != b.Item1)
                return a.Item1 - b.Item1;
            return a.Item2 - b.Item2;
        }
    }
}