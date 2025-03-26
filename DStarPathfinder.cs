using System;
using System.Collections.Generic;
using UnityEngine;

public class DStarPathfinder : IPathfinder
{
    private const int MOVE_STRAIGHT_COST = 10;
    private const int K_OLD = 0;
    private const int K_NEW = 1;
    private const int K_OPEN = 2;
    private const int K_CLOSED = 3;
    private GridManager gridManager;
    
    private class State
    {
        public int t; // 状态类型：NEW, OPEN, CLOSED
        public int h; // 估计代价
        public int k; // 最小代价
        public GridCell backPointer; // 指向父节点
    }
    
    private Dictionary<GridCell, State> _states;
    private PriorityQueue<GridCell> _openList;
    private GridCell _goal;
    
    public DStarPathfinder()
    {
        _states = new Dictionary<GridCell, State>();
        _openList = new PriorityQueue<GridCell>();
    }
    
    public List<GridCell> FindPath(GridCell start, GridCell goal, GameObject requestingAgent = null)
    {
        if (start == null || goal == null)
            return null;
        
        // 如果目标改变，重新初始化算法
        if (_goal != goal)
        {
            _goal = goal;
            InitializeSearch();
        }
        
        // 处理占用和新障碍物
        HandleOccupiedCells(requestingAgent);
        
        // 执行主算法
        ComputeShortestPath();
        
        // 根据状态生成路径
        List<GridCell> path = ExtractPath(start);
        return path;
    }
    public void Initialize(GridManager gridManager)
    {
        this.gridManager = gridManager;
        _states.Clear();
        _openList.Clear();
        Debug.Log("D* 寻路算法已初始化");
    }

    private GridCell GetCellAt(int x, int y)
    {
        return gridManager?.GetCellFromGrid(x, y);
    }
    private void InitializeSearch()
    {
        _states.Clear();
        _openList.Clear();
        
        // 初始化目标状态
        State goalState = new State
        {
            t = K_NEW,
            h = 0,
            k = 0,
            backPointer = null
        };
        
        _states[_goal] = goalState;
        _openList.Enqueue(_goal, 0);
        
        // 设置目标状态为OPEN
        _states[_goal].t = K_OPEN;
    }
    
    private void HandleOccupiedCells(GameObject requestingAgent)
    {
        // 检查和处理被占用的单元格
        foreach (var entry in _states)
        {
            GridCell cell = entry.Key;
            if (!cell.isWalkable || (cell.occupiedBy != null && cell.occupiedBy != requestingAgent))
            {
                // 标记为障碍物
                State state = entry.Value;
                if (state.h != int.MaxValue)
                {
                    state.h = int.MaxValue;
                    UpdateVertex(cell);
                }
            }
        }
    }
    
    private void ComputeShortestPath()
    {
        while (_openList.Count > 0)
        {
            int kOld = _openList.TopPriority();
            GridCell x = _openList.Dequeue();
            
            if (!_states.ContainsKey(x))
                continue;
            
            State state = _states[x];
            
            if (state.t == K_CLOSED)
                continue;
            
            if (kOld < state.h)
            {
                // 扩展状态
                foreach (GridCell y in GetNeighbors(x))
                {
                    if (!_states.ContainsKey(y))
                    {
                        _states[y] = new State { t = K_NEW, h = int.MaxValue, k = int.MaxValue, backPointer = null };
                    }
                    
                    if (_states[y].t == K_NEW)
                    {
                        _states[y].k = GetKMin(y);
                        _states[y].t = K_OPEN;
                        _openList.Enqueue(y, _states[y].k);
                    }
                    else
                    {
                        if (_states[y].h > _states[x].h + CostBetween(y, x))
                        {
                            _states[y].backPointer = x;
                            UpdateVertex(y);
                        }
                    }
                }
            }
            
            if (kOld == state.h)
            {
                state.t = K_CLOSED;
                
                foreach (GridCell y in GetNeighbors(x))
                {
                    if (!_states.ContainsKey(y))
                    {
                        _states[y] = new State { t = K_NEW, h = int.MaxValue, k = int.MaxValue, backPointer = null };
                    }
                    
                    if (_states[y].t == K_NEW ||
                        (_states[y].backPointer == x && _states[y].h != _states[x].h + CostBetween(y, x)) ||
                        (_states[y].backPointer != x && _states[y].h > _states[x].h + CostBetween(y, x)))
                    {
                        _states[y].backPointer = x;
                        UpdateVertex(y);
                    }
                }
            }
            else
            {
                // 重新入队
                _openList.Enqueue(x, GetKMin(x));
            }
        }
    }
    
    private int GetKMin(GridCell x)
    {
        if (!_states.ContainsKey(x))
            return int.MaxValue;
        
        return Math.Min(_states[x].h, _states[x].k);
    }
    
    private void UpdateVertex(GridCell x)
    {
        if (!_states.ContainsKey(x))
            return;
        
        State state = _states[x];
        
        // 如果在OPEN列表中，移除
        if (state.t == K_OPEN)
        {
            _openList.Remove(x);
        }
        
        // 如果不是CLOSED状态
        if (state.t != K_CLOSED)
        {
            if (x != _goal)
            {
                // 计算h值
                int minH = int.MaxValue;
                GridCell bestY = null;
                
                foreach (GridCell y in GetNeighbors(x))
                {
                    if (!_states.ContainsKey(y))
                        continue;
                    
                    int h = _states[y].h + CostBetween(x, y);
                    if (h < minH)
                    {
                        minH = h;
                        bestY = y;
                    }
                }
                
                if (bestY != null)
                {
                    state.h = minH;
                    state.backPointer = bestY;
                }
            }
        }
        
        // 放回OPEN列表
        state.k = state.h;
        state.t = K_OPEN;
        _openList.Enqueue(x, state.k);
    }
    
    private int CostBetween(GridCell a, GridCell b)
    {
        // 检查是否可通行
        if (!b.isWalkable || b.occupiedBy != null)
            return int.MaxValue;
        
        // 上下左右移动成本相同
        return MOVE_STRAIGHT_COST;
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
    
    private List<GridCell> ExtractPath(GridCell start)
    {
        List<GridCell> path = new List<GridCell>();
        GridCell current = start;
        
        if (!_states.ContainsKey(current))
            return null;
        
        // 沿着back pointer构建路径
        path.Add(current);
        
        while (current != _goal)
        {
            if (!_states.ContainsKey(current) || _states[current].backPointer == null)
                break;
            
            current = _states[current].backPointer;
            path.Add(current);
            
            // 防止可能出现的循环
            if (path.Count > 1000)
                break;
        }
        
        return path;
    }
    
    public void HandleChangedCells(List<GridCell> changedCells)
    {
        if (changedCells == null || changedCells.Count == 0)
            return;
        
        // 更新变化的单元格
        foreach (GridCell cell in changedCells)
        {
            if (_states.ContainsKey(cell))
            {
                if (!cell.isWalkable || cell.occupiedBy != null)
                {
                    // 标记为障碍物
                    _states[cell].h = int.MaxValue;
                    UpdateVertex(cell);
                }
                else if (cell.isWalkable && _states[cell].h == int.MaxValue)
                {
                    // 恢复为可通行
                    UpdateVertex(cell);
                }
            }
        }
        
        // 重新计算路径
        ComputeShortestPath();
    }
    
    public string GetAlgorithmName()
    {
        return "D* Pathfinding";
    }
    
    // 优先队列实现
    private class PriorityQueue<T>
    {
        private List<KeyValuePair<T, int>> _elements = new List<KeyValuePair<T, int>>();
        private Dictionary<T, int> _lookup = new Dictionary<T, int>();
        
        public int Count { get { return _elements.Count; } }
        public void Clear()
        {
            _elements.Clear();
            _lookup.Clear();
        }
        public void Enqueue(T item, int priority)
        {
            if (_lookup.ContainsKey(item))
            {
                // 如果元素已经存在，更新其优先级
                int index = _lookup[item];
                _elements[index] = new KeyValuePair<T, int>(item, priority);
                BubbleUp(index);
            }
            else
            {
                // 添加新元素
                _elements.Add(new KeyValuePair<T, int>(item, priority));
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
        
        public T Peek()
        {
            return _elements[0].Key;
        }
        
        public int TopPriority()
        {
            return _elements[0].Value;
        }
        
        private void BubbleUp(int index)
        {
            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;
                
                if (_elements[parentIndex].Value <= _elements[index].Value)
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
                if (leftChildIndex <= lastIndex && _elements[leftChildIndex].Value < _elements[smallestIndex].Value)
                    smallestIndex = leftChildIndex;
                
                if (rightChildIndex <= lastIndex && _elements[rightChildIndex].Value < _elements[smallestIndex].Value)
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
    }
}