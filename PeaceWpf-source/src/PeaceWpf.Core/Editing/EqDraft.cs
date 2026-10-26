using PeaceWpf.Core.Model;

namespace PeaceWpf.Core.Editing;

/// <summary>一次编辑会话里的完整状态：点列表 + 风味。</summary>
/// <param name="Points">这条曲线上的点。</param>
/// <param name="Flavors">叠加层风味；低音层不用，一直是默认值。</param>
public sealed record EditSnapshot(EqPointList Points, FlavorSet Flavors);

/// <summary>
/// 编辑会话里的草稿：一份“已保存”状态，一份“正在改”的状态。
///
/// 状态是「点列表 + 风味」两件一起：撤回、脏标记、取消都要覆盖风味，
/// 否则“只调了风味”会被当成没改，保存/取消按钮不出现、Ctrl+Z 也退不回去。
///
/// 只有 <see cref="Begin"/> 之后才允许改草稿；保存成功才把草稿升成已保存状态；
/// 取消就是把草稿丢回已保存状态。整个类不认识后端，不认识 WPF —— 便于单测。
/// </summary>
public sealed class EqDraft
{
    private readonly UndoStack<EditSnapshot> _history;
    private EditSnapshot _saved;
    private EditSnapshot _current;

    public EqDraft(EqPointList? saved = null, FlavorSet? flavors = null, int undoSteps = 50)
    {
        _saved = new EditSnapshot(saved ?? EqPointList.CreateDefault(), flavors ?? FlavorSet.CreateDefault());
        _current = _saved;
        _history = new UndoStack<EditSnapshot>(undoSteps);
        _history.Reset(_current);
    }

    /// <summary>最近一次保存（或进入编辑时）的曲线。</summary>
    public EqPointList Saved => _saved.Points;

    /// <summary>界面上现在显示的曲线。</summary>
    public EqPointList Current => _current.Points;

    /// <summary>最近一次保存（或进入编辑时）的风味。</summary>
    public FlavorSet SavedFlavors => _saved.Flavors;

    /// <summary>界面上现在显示的风味。</summary>
    public FlavorSet CurrentFlavors => _current.Flavors;

    /// <summary>是否在一次编辑会话里。</summary>
    public bool IsEditing { get; private set; }

    /// <summary>有改动还没保存（曲线或风味任一变了）。保存/取消之后自然变回 false。</summary>
    public bool IsDirty => !_current.Equals(_saved);

    public bool CanUndo => IsEditing && _history.CanUndo;

    public bool CanRedo => IsEditing && _history.CanRedo;

    /// <summary>草稿、脏标记、撤回可用性变了。</summary>
    public event EventHandler? Changed;

    /// <summary>进入编辑会话：草稿回到“已保存”，历史清空。</summary>
    public void Begin()
    {
        if (IsEditing)
        {
            return;
        }

        IsEditing = true;
        _current = _saved;
        _history.Reset(_current);
        Raise();
    }

    /// <summary>
    /// 退出编辑会话。调用方负责先处理脏标记（保存或放弃），
    /// 这里只把状态收回去，不会替你保存。
    /// </summary>
    public void End()
    {
        if (!IsEditing)
        {
            return;
        }

        IsEditing = false;
        _current = _saved;
        _history.Reset(_current);
        Raise();
    }

    /// <summary>
    /// 拖动过程中的临时改动：只改显示，不进撤回栈。
    /// 一次拖动结束再 <see cref="Commit"/>，于是整段拖动只算一步。
    /// </summary>
    public void Preview(EqPointList next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (!IsEditing || next.Equals(Current))
        {
            return;
        }

        _current = _current with { Points = next };
        Raise();
    }

    /// <summary>拖动风味时的临时改动：同样只改显示，松手再 <see cref="Commit"/>。</summary>
    public void PreviewFlavors(FlavorSet next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (!IsEditing || next.Equals(CurrentFlavors))
        {
            return;
        }

        _current = _current with { Flavors = next };
        Raise();
    }

    /// <summary>把当前草稿压进撤回栈。没有变化时返回 false。</summary>
    public bool Commit()
    {
        if (!IsEditing)
        {
            return false;
        }

        var pushed = _history.Push(_current);
        Raise();
        return pushed;
    }

    /// <summary>撤回一步：点和风味一起退。</summary>
    public bool Undo()
    {
        if (!IsEditing)
        {
            return false;
        }

        var state = _history.Undo();
        if (state is null)
        {
            return false;
        }

        _current = state;
        Raise();
        return true;
    }

    /// <summary>重做一步。</summary>
    public bool Redo()
    {
        if (!IsEditing)
        {
            return false;
        }

        var state = _history.Redo();
        if (state is null)
        {
            return false;
        }

        _current = state;
        Raise();
        return true;
    }

    /// <summary>取消：草稿丢回已保存状态（曲线和风味一起），历史清空，不发任何后端消息。</summary>
    public void Cancel()
    {
        if (!IsEditing)
        {
            return;
        }

        _current = _saved;
        _history.Reset(_current);
        Raise();
    }

    /// <summary>保存成功之后调用：曲线升成新的已保存状态，风味保持当前这套。</summary>
    public void AcceptSaved(EqPointList saved) => AcceptSaved(saved, LatestFlavors());

    /// <summary>保存成功之后调用：曲线和风味都升成新的已保存状态。</summary>
    public void AcceptSaved(EqPointList saved, FlavorSet flavors)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(flavors);
        _saved = new EditSnapshot(saved, flavors);
        _current = _saved;
        _history.Reset(_current);
        Raise();
    }

    /// <summary>
    /// 外部刷新读回了新状态。不在编辑会话里就顺手更新草稿；
    /// 正在编辑时宁可保留用户没保存的改动，也不拿后端的旧值盖掉。
    /// 风味不跟着后端走，沿用当前这一套（调用方要换风味请用两参数版本）。
    /// </summary>
    public bool RefreshSaved(EqPointList saved) => RefreshSaved(saved, LatestFlavors());

    /// <summary>两参数版本：曲线和风味一起换。</summary>
    public bool RefreshSaved(EqPointList saved, FlavorSet flavors)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(flavors);
        if (IsEditing)
        {
            return false;
        }

        _saved = new EditSnapshot(saved, flavors);
        _current = _saved;
        _history.Reset(_current);
        Raise();
        return true;
    }

    /// <summary>
    /// 换风味时用哪一份当“最新”：编辑中就是当前草稿，否则就是已保存那份。
    /// 这样“只换曲线”的旧调用不会把正在改的风味冲掉。
    /// </summary>
    private FlavorSet LatestFlavors() => IsEditing ? _current.Flavors : _saved.Flavors;

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}
