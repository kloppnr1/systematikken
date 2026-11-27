using Systematikken.Billing.Occupancy;

namespace Systematikken.Billing.Tests;

/// <summary>The test cases in docs/occupancy-report.md.</summary>
public class OccupancyReportTests
{
    private static readonly OccupancySettings Settings = new(new AgeCutover(2, 11), SchoolStartMonth: 8, NurseryCapacity: 24, KindergartenCapacity: 40);

    private static OccupancyPerson P(string born, string start, string? end = null) =>
        new(Guid.NewGuid(), "Barn", DateOnly.Parse(born), DateOnly.Parse(start), end is null ? null : DateOnly.Parse(end));

    private static OccupancyMonth Month(int year, int month, IReadOnlyCollection<OccupancyPerson>? children = null, IReadOnlyCollection<OccupancyPerson>? applicants = null) =>
        OccupancyReport.Build(new BillingMonth(year, month), 1, Settings, children ?? [], applicants ?? []).Single();

    [Fact]
    public void O01_CountedFromStartMonth()
    {
        var child = P("2024-05-01", "2026-03-01");
        Assert.Equal(1, Month(2026, 3, [child]).Nursery.Count);
        Assert.Equal(0, Month(2026, 2, [child]).Nursery.Count);
    }

    [Fact]
    public void O03_EndDate()
    {
        var child = P("2022-06-01", "2025-01-01", "2026-01-31");
        Assert.Equal(1, Month(2026, 1, [child]).Kindergarten.Count);
        Assert.Equal(0, Month(2026, 2, [child]).Kindergarten.Count);
    }

    [Fact]
    public void O04_OneDayIsEnough() =>
        Assert.Equal(1, Month(2026, 2, [P("2022-06-01", "2025-01-01", "2026-02-01")]).Kindergarten.Count);

    [Fact]
    public void O05_MovesToKindergartenAfterCutover()
    {
        var child = P("2023-01-01", "2024-01-01");
        Assert.Equal(1, Month(2025, 12, [child]).Nursery.Count);
        Assert.Equal(1, Month(2026, 1, [child]).Kindergarten.Count);
    }

    [Fact]
    public void O06_ExpectedEndAtSchoolStart()
    {
        var child = P("2021-03-10", "2023-01-01");
        Assert.Equal(new DateOnly(2027, 7, 31), OccupancyReport.ExpectedEnd(child.DateOfBirth, 8));
        Assert.Equal(1, Month(2027, 7, [child]).Kindergarten.Count);
        Assert.Equal(0, Month(2027, 8, [child]).Kindergarten.Count);
    }

    [Fact]
    public void O07_RealEndDateWins() =>
        Assert.Equal(0, Month(2027, 4, [P("2021-03-10", "2023-01-01", "2027-03-31")]).Kindergarten.Count);

    [Fact]
    public void O08_WaitingListFromDesiredStart()
    {
        var applicant = P("2025-01-01", "2026-08-01");
        Assert.Equal(0, Month(2026, 7, applicants: [applicant]).Nursery.WaitingList);
        Assert.Equal(1, Month(2026, 8, applicants: [applicant]).Nursery.WaitingList);
    }

    [Fact]
    public void O10_FreePlacesCanBeNegative()
    {
        var children = Enumerable.Range(0, 25).Select(_ => P("2025-01-01", "2026-01-01")).ToList();
        Assert.Equal(-1, Month(2026, 3, children).Nursery.FreePlaces);
    }

    [Fact]
    public void O11_GroupChangeIsLeavingAndJoining()
    {
        var child = P("2023-01-01", "2024-01-01");
        var january = Month(2026, 1, [child]);
        Assert.Single(january.Nursery.Leaving);
        Assert.Single(january.Kindergarten.Joining);
    }
}
