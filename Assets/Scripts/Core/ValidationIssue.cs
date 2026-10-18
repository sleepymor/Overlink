namespace Overlink.Core
{
    public class ValidationIssue
    {
        public Severity Severity { get; }
        public string Field { get; }
        public string Message { get; }

        public ValidationIssue(Severity severity, string field, string message)
        {
            Severity = severity;
            Field = field;
            Message = message;
        }

        public override string ToString() => $"[{Severity}] {Field}: {Message}";
    }
}
