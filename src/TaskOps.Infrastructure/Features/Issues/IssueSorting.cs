using TaskOps.Application.Features.Issues;
using TaskOps.Domain.Entities;

namespace TaskOps.Infrastructure.Features.Issues;

internal static class IssueSorting
{
    public static bool TryParse(string? value, out IssueSort sort)
    {
        sort = IssueSort.CreatedAtDescending;
        var normalized = IssueValidation.NormalizeOptional(value);
        if (normalized is null)
        {
            return true;
        }

        sort = normalized switch
        {
            "createdAt" or "created" => IssueSort.CreatedAtAscending,
            "-createdAt" or "-created" => IssueSort.CreatedAtDescending,
            "dueDate" or "due" => IssueSort.DueDateAscending,
            "-dueDate" or "-due" => IssueSort.DueDateDescending,
            "priority" => IssueSort.PriorityAscending,
            "-priority" => IssueSort.PriorityDescending,
            "status" => IssueSort.StatusAscending,
            "-status" => IssueSort.StatusDescending,
            "title" => IssueSort.TitleAscending,
            "-title" => IssueSort.TitleDescending,
            "number" => IssueSort.NumberAscending,
            "-number" => IssueSort.NumberDescending,
            _ => IssueSort.Invalid
        };

        return sort != IssueSort.Invalid;
    }

    public static IOrderedQueryable<Issue> Apply(IQueryable<Issue> query, IssueSort sort)
    {
        return sort switch
        {
            IssueSort.CreatedAtAscending => query.OrderBy(issue => issue.CreatedAt).ThenBy(issue => issue.Id),
            IssueSort.DueDateAscending => query
                .OrderBy(issue => issue.DueDate == null)
                .ThenBy(issue => issue.DueDate)
                .ThenBy(issue => issue.Project.Key)
                .ThenBy(issue => issue.Number),
            IssueSort.DueDateDescending => query
                .OrderBy(issue => issue.DueDate == null)
                .ThenByDescending(issue => issue.DueDate)
                .ThenBy(issue => issue.Project.Key)
                .ThenBy(issue => issue.Number),
            IssueSort.PriorityAscending => query
                .OrderBy(issue =>
                    issue.Priority == IssuePriority.Low ? 1 :
                    issue.Priority == IssuePriority.Medium ? 2 :
                    issue.Priority == IssuePriority.High ? 3 :
                    4)
                .ThenBy(issue => issue.Project.Key)
                .ThenBy(issue => issue.Number),
            IssueSort.PriorityDescending => query
                .OrderByDescending(issue =>
                    issue.Priority == IssuePriority.Low ? 1 :
                    issue.Priority == IssuePriority.Medium ? 2 :
                    issue.Priority == IssuePriority.High ? 3 :
                    4)
                .ThenBy(issue => issue.Project.Key)
                .ThenBy(issue => issue.Number),
            IssueSort.StatusAscending => query
                .OrderBy(issue =>
                    issue.Status == IssueStatus.Todo ? 1 :
                    issue.Status == IssueStatus.InProgress ? 2 :
                    issue.Status == IssueStatus.InReview ? 3 :
                    4)
                .ThenBy(issue => issue.Project.Key)
                .ThenBy(issue => issue.Number),
            IssueSort.StatusDescending => query
                .OrderByDescending(issue =>
                    issue.Status == IssueStatus.Todo ? 1 :
                    issue.Status == IssueStatus.InProgress ? 2 :
                    issue.Status == IssueStatus.InReview ? 3 :
                    4)
                .ThenBy(issue => issue.Project.Key)
                .ThenBy(issue => issue.Number),
            IssueSort.TitleAscending => query.OrderBy(issue => issue.Title).ThenBy(issue => issue.Id),
            IssueSort.TitleDescending => query.OrderByDescending(issue => issue.Title).ThenBy(issue => issue.Id),
            IssueSort.NumberAscending => query.OrderBy(issue => issue.Project.Key).ThenBy(issue => issue.Number),
            IssueSort.NumberDescending => query.OrderByDescending(issue => issue.Project.Key).ThenByDescending(issue => issue.Number),
            _ => query.OrderByDescending(issue => issue.CreatedAt).ThenByDescending(issue => issue.Id)
        };
    }
}

internal enum IssueSort
{
    Invalid,
    CreatedAtAscending,
    CreatedAtDescending,
    DueDateAscending,
    DueDateDescending,
    PriorityAscending,
    PriorityDescending,
    StatusAscending,
    StatusDescending,
    TitleAscending,
    TitleDescending,
    NumberAscending,
    NumberDescending
}
