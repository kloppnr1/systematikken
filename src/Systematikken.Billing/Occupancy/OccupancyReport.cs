namespace Systematikken.Billing.Occupancy;

/// <summary>An enrolled child, or a waiting-list applicant, as the occupancy report sees them.</summary>
public sealed record OccupancyPerson(Guid Id, string Name, DateOnly DateOfBirth, DateOnly Start, DateOnly? End);

public sealed record OccupancySettings(AgeCutover Cutover, int SchoolStartMonth, int NurseryCapacity, int KindergartenCapacity);

public sealed record OccupancyGroup(
    IReadOnlyList<OccupancyPerson> Present,
    IReadOnlyList<OccupancyPerson> Joining,
    IReadOnlyList<OccupancyPerson> Leaving,
    int WaitingList,
    int Capacity)
{
    public int Count => Present.Count;
    public int FreePlaces => Capacity - Count;
}

public sealed record OccupancyMonth(BillingMonth Month, OccupancyGroup Nursery, OccupancyGroup Kindergarten);

/// <summary>Rules O1–O5 in docs/occupancy-report.md. Pure.</summary>
public static class OccupancyReport
{
    public static IReadOnlyList<OccupancyMonth> Build(
        BillingMonth from,
        int months,
        OccupancySettings settings,
        IReadOnlyCollection<OccupancyPerson> children,
        IReadOnlyCollection<OccupancyPerson> applicants)
    {
        var result = new List<OccupancyMonth>();
        for (var i = 0; i < months; i++)
        {
            var month = from.AddMonths(i);
            var previous = month.AddMonths(-1);
            result.Add(new OccupancyMonth(
                month,
                Group(CareGroup.Nursery, settings.NurseryCapacity),
                Group(CareGroup.Kindergarten, settings.KindergartenCapacity)));

            OccupancyGroup Group(CareGroup group, int capacity)
            {
                var now = In(children, month, group, settings);
                var before = In(children, previous, group, settings);
                return new OccupancyGroup(
                    now,
                    now.Where(c => !before.Any(b => b.Id == c.Id)).ToList(),
                    before.Where(b => !now.Any(c => c.Id == b.Id)).ToList(),
                    In(applicants, month, group, settings).Count,
                    capacity);
            }
        }
        return result;
    }

    /// <summary>O2: the last day before the school-start month in the year the child turns 6.</summary>
    public static DateOnly ExpectedEnd(DateOnly dateOfBirth, int schoolStartMonth) =>
        new DateOnly(dateOfBirth.Year + 6, schoolStartMonth, 1).AddDays(-1);

    /// <summary>O1: there on at least one day of the month. A real end date wins over the expected one.</summary>
    public static bool IsPresent(OccupancyPerson p, BillingMonth month, int schoolStartMonth)
    {
        var end = p.End ?? ExpectedEnd(p.DateOfBirth, schoolStartMonth);
        return p.Start <= month.LastDay && end >= month.FirstDay;
    }

    private static List<OccupancyPerson> In(IEnumerable<OccupancyPerson> people, BillingMonth month, CareGroup group, OccupancySettings s) =>
        people.Where(p => IsPresent(p, month, s.SchoolStartMonth) && s.Cutover.GroupFor(p.DateOfBirth, month) == group)
            .OrderBy(p => p.Name)
            .ToList();
}
