# Occupancy report ("Pladser")

Shows the manager, month by month, how many nursery and kindergarten children the institution has against its capacity, so they can plan intake from the waiting list.

Like the fee engine, the counting is one pure function with its own tests: it takes the months, the institution's settings, the children and the waiting list, and returns the counts.

## Settings

| Setting | Example |
|---|---|
| Nursery capacity | 24 |
| Kindergarten capacity | 40 |
| Age at which a child moves to kindergarten | 2 years 11 months (same setting as the fees) |
| School-start month | August (set per municipality, not per institution) |

## What the screen shows

- One row per month: the next 12 months from this month, or a chosen calendar year.
- For each month: nursery count, kindergarten count, capacity for each, and free places (capacity minus count, can be negative).
- A stacked column chart, one stack for nursery and one for kindergarten, with capacity drawn as a line.
- Waiting-list children as an extra layer on each stack, off by default.
- Tap a month to list the children who are there, who joins that month and who leaves.

## Rules

**O1. Counted in a month.** A child counts if they are there on at least one day of it: start date on or before the last day, and end date on or after the 1st.

**O2. Expected end.** A child with no end date is expected to leave at school start: the last day before the school-start month in the year they turn 6.
- Born 10 Mar 2021, school start August → expected end 31 Jul 2027.

**O3. Nursery or kindergarten.** Same rule as fee rule R3. The child counts as kindergarten from the first month that starts after the cut-over date. The report and billing always agree.

**O4. Waiting list.** An applicant who is waiting or has been offered a place counts in the waiting-list layer from their desired start month until their O2 expected end. They are split into nursery or kindergarten by O3. Declined and removed applicants never count.

**O5. Joining and leaving.** Joining = counted this month but not last month. Leaving = counted last month but not this month. A child who moves from nursery to kindergarten shows as leaving nursery and joining kindergarten.

## Test cases

Cut-over 2 y 11 m, school start August, capacity 24 / 40.

| # | Case | Month | Expected |
|---|---|---|---|
| 1 | Starts 1 Mar 2026, born 1 May 2024 | Mar 2026 | nursery 1 |
| 2 | Same child | Feb 2026 | not counted |
| 3 | Ends 31 Jan 2026 | Jan / Feb 2026 | counted / not counted |
| 4 | Ends 1 Feb 2026 | Feb 2026 | counted (one day is enough) |
| 5 | Born 1 Jan 2023 (cut-over 1 Dec 2025) | Dec 2025 / Jan 2026 | nursery / kindergarten |
| 6 | Born 10 Mar 2021, no end date | Jul 2027 / Aug 2027 | counted / not counted |
| 7 | Same child, end date 31 Mar 2027 | Apr 2027 | not counted (a real end date wins) |
| 8 | Applicant waiting, desired start 1 Aug 2026, born 1 Jan 2025 | Jul / Aug 2026 | waiting layer 0 / nursery 1 |
| 9 | Applicant declined | any | never counted |
| 10 | 25 nursery children | any | free nursery places −1 |
| 11 | Case 5 child | Jan 2026 | leaving nursery and joining kindergarten |

## Decided

- **School start** is set per municipality. In the old data most used August and one used April.
