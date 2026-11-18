namespace IncidentManagementSystem.Models.Enums;

public enum NotificationMethod
{
    OfficialLetter = 1,
    Phone = 2,
    Other = 3
}

public static class NotificationMethodDisplay
{
    public static string GetLabel(NotificationMethod method) => method switch
    {
        NotificationMethod.OfficialLetter => "جواب رسمي",
        NotificationMethod.Phone => "إخطار هاتفي",
        NotificationMethod.Other => "أخرى",
        _ => "غير معروف"
    };
}
