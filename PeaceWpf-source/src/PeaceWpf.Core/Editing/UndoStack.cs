using PeaceWpf.Core.Model;

namespace PeaceWpf.Core.Editing;

/// <summary>
/// 撤回栈：只存整个状态的快照，不记录“这一步改了什么”。
///
/// 这样拖动、建点、删点、改 Q、改风味都走同一条路，也不用为每种操作写逆操作。
/// 上限按“步”算：50 步意味着最多 51 份状态（初始状态 + 50 次改动）。
///
/// 泛型是为了让同一条栈同时装“点列表”和“点列表 + 风味”两种状态：
/// 低音层只有点，左右层还要带上风味，两者互不干扰。
/// </summary>
public sealed class UndoStack<T>
    where T : class
{
    private readonly int _maxSteps;
    private readonly List<T> _states = new();
    private int _index = -1;

    public UndoStack(int maxSteps = 50)
    {
        if (maxSteps < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSteps), "撤回步数至少是 1。");
        }

        _maxSteps = maxSteps;
    }

    public bool CanUndo => _index > 0;

    public bool CanRedo => _index >= 0 && _index < _states.Count - 1;

    /// <summary>当前状态在栈里的位置，测试用。</summary>
    public int Position => _index;

    public int Count => _states.Count;

    /// <summary>丢掉历史，把给定状态当作新的起点。进入编辑会话和保存之后都调它。</summary>
    public void Reset(T state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _states.Clear();
        _states.Add(state);
        _index = 0;
    }

    /// <summary>
    /// 压入一个新状态。和当前状态完全一样时什么都不做；
    /// 撤回之后再做新改动会丢弃“未来”，这是标准行为。
    /// </summary>
    public bool Push(T state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (_index >= 0 && state.Equals(_states[_index]))
        {
            return false;
        }

        if (_index >= 0 && _index < _states.Count - 1)
        {
            _states.RemoveRange(_index + 1, _states.Count - _index - 1);
        }

        _states.Add(state);
        _index = _states.Count - 1;

        while (_states.Count > _maxSteps + 1)
        {
            _states.RemoveAt(0);
            _index--;
        }

        return true;
    }

    /// <summary>退回一步，返回要恢复的状态；已经到头则返回 null。</summary>
    public T? Undo()
    {
        if (!CanUndo)
        {
            return null;
        }

        _index--;
        return _states[_index];
    }

    /// <summary>重做一步，返回要恢复的状态；已经到头则返回 null。</summary>
    public T? Redo()
    {
        if (!CanRedo)
        {
            return null;
        }

        _index++;
        return _states[_index];
    }
}
