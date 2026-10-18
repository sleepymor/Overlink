using System;
using System.Collections.Generic;
using System.Linq;

namespace Overlink.Core
{
    public class ValidationResult
    {
        public IReadOnlyList<ValidationIssue> Issues { get; }
        public bool HasErrors => Issues != null && Issues.Any(i => i.Severity == Severity.Error);
        public bool HasWarnings => Issues != null && Issues.Any(i => i.Severity == Severity.Warning);
        public bool HasInfo => Issues != null && Issues.Any(i => i.Severity == Severity.Info);

        public ValidationResult(IReadOnlyList<ValidationIssue> issues)
        {
            Issues = issues ?? Array.Empty<ValidationIssue>();
        }
    }
}
