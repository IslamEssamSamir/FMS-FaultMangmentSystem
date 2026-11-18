namespace IncidentManagementSystem.ViewModels;

public sealed class IncidentAttachmentDownloadViewModel
{
    public Stream Content { get; set; } = Stream.Null;
    public string ContentType { get; set; } = "application/pdf";
    public string Extension { get; set; } = ".pdf";
}
