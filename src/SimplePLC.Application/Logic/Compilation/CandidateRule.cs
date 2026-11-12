namespace SimplePLC.Application.Logic.Compilation;

using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Models;

/// <summary>
/// Đại diện cho một ứng viên luật (candidate rule) được sinh ra từ quá trình duyệt đồ thị
/// hoặc mở rộng macro trước khi qua phân tích truy cập và sắp xếp thứ tự thực thi.
/// </summary>
/// <param name="ActionNode">Node hành vi đại diện trong đồ thị FBD.</param>
/// <param name="Trigger">Mô hình kích hoạt (Trigger Model).</param>
/// <param name="Action">Mô hình hành động (Action Model).</param>
/// <param name="Guard">Mô hình điều kiện khóa (Guard Model).</param>
/// <param name="RuleName">Tên định danh luật thực thi.</param>
/// <param name="Origin">Nguồn gốc node macro đã sinh ra luật này (nếu có).</param>
public sealed record CandidateRule(
    LogicNode ActionNode,
    TriggerModel Trigger,
    ActionModel Action,
    GuardModel Guard,
    string RuleName,
    GeneratedRuleOrigin? Origin = null);
