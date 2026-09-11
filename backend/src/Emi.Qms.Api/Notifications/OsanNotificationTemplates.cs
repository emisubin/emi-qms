using System.Net;
using System.Text;

namespace Emi.Qms.Api.Notifications;

public sealed record OsanNotificationContent(string Title, string Message, string Subject, string ButtonText, string HtmlBody);

public static class OsanNotificationTemplates
{
    public static OsanNotificationContent Render(OsanNotificationSnapshot item, string? linkUrl = null)
    {
        var targets = item.Targets.Length switch { 0 => "", 1 => item.Targets[0], _ => $"{item.Targets[0]} 외 {item.Targets.Length - 1}개" };
        var isProject = item.Kind is OsanNotificationKind.ProjectCreated or OsanNotificationKind.ProjectCompleted;
        var action = item.Kind switch
        {
            OsanNotificationKind.ProjectCreated => "신규 프로젝트 등록",
            OsanNotificationKind.StepCompleted => $"{item.StepName} 완료",
            OsanNotificationKind.StepRejected => $"{item.StepName} 반려",
            OsanNotificationKind.StepEdited => $"{item.StepName} 수정 완료",
            OsanNotificationKind.ProjectCompleted => "프로젝트 완료",
            _ => throw new ArgumentOutOfRangeException(nameof(item))
        };
        var title = item.Kind switch
        {
            OsanNotificationKind.ProjectCreated => "새 프로젝트가 등록되었습니다",
            OsanNotificationKind.ProjectCompleted => "프로젝트가 완료되었습니다",
            _ => action
        };
        var intro = item.Kind switch
        {
            OsanNotificationKind.ProjectCreated => "새 프로젝트가 등록되었습니다.",
            OsanNotificationKind.StepCompleted => "진행단계가 완료되었습니다.",
            OsanNotificationKind.StepRejected => "등록된 진행단계가 반려되었습니다. 아래 사유를 확인하고 내용을 보완해 주세요.",
            OsanNotificationKind.StepEdited => "진행단계의 사진·코멘트가 수정 저장되었습니다.",
            _ => "모든 진행 대상의 7단계가 완료되어 프로젝트가 완료 처리되었습니다."
        };
        var message = item.Kind switch
        {
            OsanNotificationKind.ProjectCreated => $"{item.ActorName}님이 {item.ProjectName} 프로젝트를 등록했습니다.\nCode: {item.ProjectCode}",
            OsanNotificationKind.ProjectCompleted => $"{item.ProjectName}의 모든 진행 대상이 7단계를 완료했습니다.\nCode: {item.ProjectCode}",
            _ => $"{item.ProjectName} · {targets}\nCode: {item.ProjectCode}\n{item.ActorName}님이 " +
                (item.Kind == OsanNotificationKind.StepRejected ? $"단계를 반려했습니다.\n사유: {item.Comment}" :
                 item.Kind == OsanNotificationKind.StepEdited ? "사진·코멘트를 수정 저장했습니다." : "단계를 완료했습니다.") +
                $"\n진행 대상: {string.Join(", ", item.Targets)}"
        };
        var timestamp = item.OccurredAt.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd HH:mm:ss") + " (한국 시간)";
        message += $"\n{timestamp}";
        var button = item.Kind switch
        {
            OsanNotificationKind.StepCompleted => "완료 기록 보기",
            OsanNotificationKind.StepRejected => "반려 내용 확인하기",
            OsanNotificationKind.StepEdited => "수정 기록 보기",
            _ => "프로젝트 상세 보기"
        };
        var subject = $"[EMI PMS · 오산] {action} · {item.ProjectName}" + (isProject ? "" : $" · {targets}");
        var html = new StringBuilder("<html lang=\"ko\"><body style=\"font-family:Arial,sans-serif;color:#17212b;line-height:1.6\"><h2>EMI PMS · 오산</h2>");
        html.Append("<p>").Append(E(intro)).Append("</p><dl>");
        Field("장비명", item.ProjectName); Field("프로젝트 코드", item.ProjectCode);
        if (isProject)
        {
            Field("Part 분류", item.PartCategory); Field("고객사", item.CustomerName);
            Field(item.Kind == OsanNotificationKind.ProjectCompleted ? "완료 수량" : "수량", item.Quantity.ToString());
            Field("납기일", item.DueDate?.ToString("yyyy-MM-dd") ?? "미지정");
        }
        else { Field("진행 대상", string.Join(", ", item.Targets)); Field("진행 단계", item.StepName ?? ""); }
        if (item.Kind != OsanNotificationKind.ProjectCompleted)
            Field(item.Kind == OsanNotificationKind.StepRejected ? "반려 처리자" : item.Kind == OsanNotificationKind.StepEdited ? "최종 등록자" : "등록자", item.ActorName);
        Field(item.Kind switch {
            OsanNotificationKind.ProjectCreated => "등록 일시", OsanNotificationKind.StepRejected => "반려 일시",
            OsanNotificationKind.StepEdited => "수정 일시", _ => "완료 일시" }, timestamp);
        if (!isProject && item.Kind != OsanNotificationKind.StepRejected) Field("등록 사진", $"{item.PhotoCount}장");
        html.Append("</dl>");
        if (!isProject)
        {
            html.Append("<h3>").Append(item.Kind == OsanNotificationKind.StepRejected ? "반려 사유" : item.Kind == OsanNotificationKind.StepEdited ? "최종 코멘트" : "코멘트")
                .Append("</h3><p style=\"white-space:pre-wrap\">").Append(E(string.IsNullOrWhiteSpace(item.Comment) ? "등록된 코멘트가 없습니다." : item.Comment)).Append("</p>");
        }
        if (item.Kind == OsanNotificationKind.StepRejected)
            html.Append("<p>해당 프로젝트의 진행 등록 권한이 있는 사용자가 수정할 수 있습니다. 한 사람이 수정 내용을 저장하면 다시 잠깁니다.</p>");
        if (item.Kind == OsanNotificationKind.StepEdited)
            html.Append("<p>이전 사진과 코멘트는 해당 단계의 이력 보기에서 확인할 수 있습니다.</p>");
        if (item.Kind == OsanNotificationKind.ProjectCompleted)
            html.Append("<p>진행 대상별 사진·코멘트와 처리 이력은 프로젝트 상세에서 확인할 수 있습니다.</p>");
        if (Uri.TryCreate(linkUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            html.Append("<p><a style=\"display:inline-block;background:#156f89;color:white;padding:12px 20px;text-decoration:none\" href=\"").Append(E(uri.AbsoluteUri)).Append("\">").Append(button).Append("</a></p>");
        html.Append("<hr><p>이 메일은 EMI PMS에서 자동 발송되었습니다.<br>상세 내용은 PMS에 로그인한 후 권한 범위 내에서 확인할 수 있습니다.</p></body></html>");
        return new(title, message, subject, button, html.ToString());
        void Field(string label, string value) => html.Append("<dt><b>").Append(E(label)).Append("</b></dt><dd>").Append(E(value)).Append("</dd>");
    }
    private static string E(string value) => WebUtility.HtmlEncode(value);
}
