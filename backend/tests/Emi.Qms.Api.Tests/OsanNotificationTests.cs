using Emi.Qms.Api.Notifications;
using System.Text.Json;
using Xunit;

namespace Emi.Qms.Api.Tests;

public sealed class OsanNotificationTests
{
    private static OsanNotificationSnapshot Snapshot(OsanNotificationKind kind) => new(kind,
        "장비 <script>", "WO-1", "Rack", "고객사", 2, new DateOnly(2026, 9, 30), "담당자",
        new DateTimeOffset(2026, 9, 11, 18, 0, 0, TimeSpan.Zero), "배선검사", ["패널 01", "패널 02"],
        "검사 완료 <img onerror=alert(1)>", 2);

    [Theory]
    [InlineData(OsanNotificationKind.ProjectCreated, "신규 프로젝트 등록")]
    [InlineData(OsanNotificationKind.StepCompleted, "배선검사 Gate 완료")]
    [InlineData(OsanNotificationKind.StepRejected, "배선검사 반려")]
    [InlineData(OsanNotificationKind.StepEdited, "배선검사 수정 완료")]
    [InlineData(OsanNotificationKind.StepIssueRegistered, "배선검사 공정 이상 발생")]
    [InlineData(OsanNotificationKind.StepIssueResolved, "배선검사 조치 완료")]
    [InlineData(OsanNotificationKind.StepWorkRequested, "배선검사 공정 진행 요청")]
    [InlineData(OsanNotificationKind.ProjectCompleted, "프로젝트 완료")]
    public void Templates_preserve_event_snapshot_and_encode_untrusted_text(OsanNotificationKind kind, string subject)
    {
        var result = OsanNotificationTemplates.Render(Snapshot(kind), "https://pms.example/progress?projectId=1&targetId=2");
        Assert.StartsWith("[EMI PMS · 오산] " + subject, result.Subject);
        Assert.Contains("2026-09-12 03:00:00", result.HtmlBody);
        Assert.DoesNotContain("<script>", result.HtmlBody);
        Assert.DoesNotContain("<img onerror", result.HtmlBody);
        Assert.Contains("&lt;script&gt;", result.HtmlBody);
        Assert.Contains("targetId=2", result.HtmlBody);
        Assert.Contains("로그인", result.HtmlBody);
        if (kind is OsanNotificationKind.StepCompleted or OsanNotificationKind.StepEdited or OsanNotificationKind.StepRejected
            or OsanNotificationKind.StepIssueRegistered or OsanNotificationKind.StepIssueResolved
            or OsanNotificationKind.StepWorkRequested)
        {
            Assert.Contains("패널 01 외 1개", result.Subject);
            Assert.Contains("패널 01, 패널 02", result.HtmlBody);
        }
    }

    [Fact]
    public void Issue_templates_include_required_action_copy_and_buttons()
    {
        var registered = OsanNotificationTemplates.Render(Snapshot(OsanNotificationKind.StepIssueRegistered));
        Assert.Equal("배선검사 공정 이상 발생", registered.Title);
        Assert.Equal("공정 이상 내용 확인하기", registered.ButtonText);
        Assert.Contains("앞 6단계 완료와 모든 이상 조치 전에는 포장할 수 없습니다", registered.HtmlBody);
        Assert.Contains("검사 완료", registered.HtmlBody);

        var resolved = OsanNotificationTemplates.Render(Snapshot(OsanNotificationKind.StepIssueResolved));
        Assert.Equal("배선검사 조치 완료", resolved.Title);
        Assert.Equal("조치 완료 기록 보기", resolved.ButtonText);
        Assert.Contains("이상 조치가 완료되었으며 해당 Gate도 완료", resolved.HtmlBody);
        Assert.Contains("검사 완료", resolved.HtmlBody);
    }

    [Fact]
    public void Work_request_template_includes_work_order_part_and_request_action()
    {
        var requested = OsanNotificationTemplates.Render(
            Snapshot(OsanNotificationKind.StepWorkRequested) with { WorkOrderNumber = "WORK-42" });
        Assert.Equal("배선검사 공정 진행 요청", requested.Title);
        Assert.Equal("요청 공정 보기", requested.ButtonText);
        Assert.Contains("WORK-42", requested.Message);
        Assert.Contains("W/O", requested.HtmlBody);
        Assert.Contains("Rack", requested.HtmlBody);
        Assert.Contains("공정 진행을 요청했습니다", requested.Message);
        Assert.DoesNotContain("등록 사진", requested.HtmlBody);
    }

    [Fact]
    public void Legacy_numeric_project_completion_snapshot_preserves_its_enum_identity()
    {
        var json = JsonSerializer.Serialize(Snapshot(OsanNotificationKind.ProjectCompleted));
        Assert.Contains("\"Kind\":4", json);
        var restored = JsonSerializer.Deserialize<OsanNotificationSnapshot>(json)!;
        Assert.Equal(OsanNotificationKind.ProjectCompleted, restored.Kind);
        Assert.Equal("프로젝트가 완료되었습니다", OsanNotificationTemplates.Render(restored).Title);
    }

    [Fact]
    public void Completion_key_is_lifetime_scoped_but_other_events_are_operation_scoped()
    {
        var project = Guid.NewGuid(); var first = Guid.NewGuid(); var next = Guid.NewGuid();
        Assert.Equal(OsanNotificationWriter.IdempotencyKey(project, first, OsanNotificationKind.ProjectCompleted),
            OsanNotificationWriter.IdempotencyKey(project, next, OsanNotificationKind.ProjectCompleted));
        Assert.NotEqual(OsanNotificationWriter.IdempotencyKey(project, first, OsanNotificationKind.StepCompleted),
            OsanNotificationWriter.IdempotencyKey(project, next, OsanNotificationKind.StepCompleted));
    }

    [Fact]
    public void Empty_comment_and_unsafe_link_are_handled_without_html_injection()
    {
        var result = OsanNotificationTemplates.Render(Snapshot(OsanNotificationKind.StepCompleted) with { Comment = null }, "javascript:alert(1)");
        Assert.Contains("등록된 코멘트가 없습니다.", result.HtmlBody);
        Assert.DoesNotContain("href=", result.HtmlBody);
    }

    [Fact]
    public void Graph_html_is_explicit_opt_in_and_existing_mail_remains_text()
    {
        var plain = new MailDeliveryPayload("test@example.com", "제목", "본문", null, null);
        Assert.Equal("Text", GraphSendMailRequest.FromPayload(plain).Message.Body.ContentType);
        Assert.Equal("HTML", GraphSendMailRequest.FromPayload(plain with { IsHtml = true }).Message.Body.ContentType);
    }
}
