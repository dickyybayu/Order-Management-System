namespace OMS.API.Models;

public sealed class BackgroundJobExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string JobName { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; }

    public DateTime? FinishedAtUtc { get; set; }

    public BackgroundJobExecutionStatus Status { get; set; } = BackgroundJobExecutionStatus.Running;

    public string? Message { get; set; }

    public void NormalizeForStorage()
    {
        JobName = JobName.Trim();
        Message = string.IsNullOrWhiteSpace(Message) ? null : Message.Trim();
    }
}
