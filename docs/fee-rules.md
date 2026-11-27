# Fee rules

How Systematikken works out what each payer owes each month. Taken from how the old system behaved for ten years. This is the spec: the fee engine is written from scratch against it, and every rule has test cases.

The engine is one pure function: it takes a month, the institution's settings, the children, their subsidies and their earlier postings, and returns the lines for each charge. It does no database or UI work, so every rule can be tested on its own.

## Institution settings

| Setting | Example |
|---|---|
| Nursery price per month | 4,233.50 kr. |
| Kindergarten price per month | 1,233.25 kr. |
| Part-time prices (optional) | 2,550.00 / 800.50 kr. |
| Age at which a child moves to kindergarten price | 2 years 11 months (set per municipality) |
| Month with no charge (optional) | July |
| Deposit (optional), and how many months before start it is charged | 1,099 kr., 2 months |
| Sign-up fee (optional) | |
| Meal scheme per month (optional) | |
| Half-month day | 16 (general rule; the institution can change it) |

## Rules

**R1. One charge list per month.** It covers the 1st to the last day of the month. A month marked "no charge" is skipped entirely, and the next list is for the month after.

**R2. Who is charged.** A child is charged for a month if they have a start date on or before the last day of the month, and no end date or an end date on or after the 1st.
- No start date, or start after the month → not charged ("not started").
- End before the month → not charged ("has left").

**R3. Nursery or kindergarten.** Cut-over date = date of birth + the municipality's years and months. The child pays kindergarten price from the first month that starts *after* the cut-over date.
- Born 1 Jan 2015, cut-over 2 y 11 m → 1 Dec 2017. December 2017 is nursery. January 2018 is kindergarten.
- Born 15 Jan 2015, cut-over 2 y 9 m → 15 Oct 2017. October is nursery, November is kindergarten.

**R4. Price.** Nursery or kindergarten price from R3. If the child is part-time and a part-time price is set, use that instead.

**R5. Half month.** The month's price is halved if:
- the child starts in the month on the half-month day or later, or
- the child ends in the month before the half-month day.

The half-month day is the 16th by general rule, and the institution can change it. With the 16th: starting on the 2nd–15th, or ending on the 16th or later, is a full month.

**R6. First invoice catches up.** If a child has never been invoiced and started before this month, each missed month from the start month is added as its own line, with its own R3 group, R5 half-month rule, R8 meal scheme and R9 subsidy. The month without charge is skipped. Then this month is added.
- Started 1 Feb, first invoiced in March → February + March.
- Started 16 Feb, first invoiced in March → half February + March.

**R7. One-off fees.** Charged once, on the child's first invoice:
- the deposit, if the institution uses it. With "N months ahead" it is charged, on its own charge, in the month that lies N months before the start month: start in December, 2 months ahead → charged in October. If the child is enrolled after that month's list was approved, the deposit goes on the next list instead. It is never charged twice.
- the sign-up fee, if the institution uses it.

**R8. Meal scheme.** A fixed amount every charged month, if the institution uses it. Always the full amount, also in a half month.

**R9. Subsidy.** Free-place subsidy and sibling discount are amounts per child per month, taken from the municipality. They are subtracted as their own lines. The engine never works them out itself. A charged child who had a subsidy last month and has none this month is flagged for the manager to check: usually the municipality renewed it and nobody entered it yet. The manager enters the new amount, or 0.

**R10. Carry-over.** All postings before this month are summed for the child.
- Unpaid balance → added to this month, one line per month it comes from: "Opkrævning af restgæld fra 11-2016".
- Overpaid balance → subtracted from this month, one line per month: "For stor betaling overført fra 11-2016".

**R11. Payer.** The child's chosen payer. If none is chosen, the system asks; it does not guess (the old system picked the first parent, which could be wrong).

**R12. Manual lines.** The manager can add a manual charge or credit with a text to a month that is not yet approved.

**R13. Amount to pay.** The sum of all lines. Each line is rounded to whole øre when it is made, with half an øre going to the even øre, as in the old system (616.625 → 616.62, 616.635 → 616.64). The stored amount is then the same as the amount collected, and the lines on the payer's overview add up to the total.

## Betalingsservice file (collection)

- One collection per charge where the amount to pay is above 0.
- Customer number = the child's payment number. Siblings have one collection each.
- Collection date = the 1st of the month.
- Text lines to the payer: one per line on the charge (price, half month, subsidy, sibling discount, meal scheme, carried-over amounts, manual lines), each with its amount right-aligned.
- Payer name, child's first name, address and postcode.

## Invoice

Betalingsservice is the invoice for every payer. All charges go in the collection file, whether or not the payer has signed up yet:
- A payer with a Betalingsservice agreement sees the amount and text lines on their payment overview.
- A payer without one gets a payment slip from Betalingsservice (Mastercard), with an offer to sign up.

The system itself sends no invoices.

## Payments file (Betalingsservice payment information)

- The same file is never imported twice (checked by content).
- Each completed payment is matched by customer number and collection date to the charge.
- Status after import: paid, partly paid, or overpaid.
- **New:** a payment that matches no charge is shown to the manager and does not stop the rest of the file. The old system stopped the whole import.

## Things the old system did that I'd change

1. **Subsidy in catch-up months.** R6 adds earlier months but no subsidy for them. A family that had free place in February paid full price for February when first invoiced in March. → Use the subsidy entered for each month.
2. **Meal scheme in catch-up months** was dated to the current month instead of the month it belongs to. → Date it correctly.
3. **Charges at or below 0** (a credit) were left out of the Betalingsservice file and nothing else happened. → Show them to the manager as "to be paid back or carried over".
4. **Payer guessed** (see R11).
5. **Half-øre remainders.** The old system rounded only the collected total, but kept the unrounded amount on the charge. A half month of 1,233.25 was stored as 616.625 and collected as 616.62, which left 0.005 kr. as "partly paid" and carried it into the next month. The payer's text lines were also rounded a different way from the total, so they didn't always add up. → Round each line when it is made (R13).

## Test cases

Prices in all cases: nursery 4,233.50, kindergarten 1,233.25, part-time 2,550.00 / 800.50, cut-over 2 y 11 m unless stated.

| # | Case | Month | Expected to pay |
|---|---|---|---|
| 1 | Nursery, starts 1 Jan | Jan 2017 | 4,233.50 |
| 2 | Same, with sibling 1,550.50 and free place 1,100.25 | Jan 2017 | 1,582.75 |
| 3 | Left 31 Dec 2016 | Jan 2017 | not charged |
| 4 | Starts 1 Feb 2017 | Jan 2017 | not charged |
| 5 | Kindergarten, starts 16 Jan | Jan 2017 | 616.62 (half of 1,233.25, rounded to even) |
| 6 | Born 1 Jan 2015 | Dec 2017 / Jan 2018 | 4,233.50 / 1,233.25 |
| 7 | Born 15 Jan 2015, cut-over 2 y 9 m | Sep 2017 / Nov 2017 | 4,233.50 / 1,233.25 |
| 8 | Kindergarten, started 1 Feb, never invoiced | Mar 2017 | 2,466.50 (Feb + Mar) |
| 9 | Kindergarten, started 16 Feb, never invoiced | Mar 2017 | 1,849.87 (616.62 + 1,233.25) |
| 10 | Nov 2016: charged 1,200, paid 1,199 | Jan 2017 | 4,234.50 (adds 1.00 debt) |
| 11 | Nov 2016: charged 1,200, paid 1,201 | Jan 2017 | 4,232.50 (subtracts 1.00) |
| 12 | Oct: 1,200 charged, 1,000 paid. Nov: 1,200 charged, 1,399.26 paid | Jan 2017 | 4,234.24 (adds 0.74) |
| 13 | Part-time, born 1 Jan 2015 | Dec 2017 / Jan 2018 | 2,550.00 / 800.50 |
| 14 | Part-time kindergarten, starts 16 Jan | Jan 2017 | 400.25 |
| 15 | Deposit 1,099, charged 2 months ahead, starts 1 Dec 2018 | Oct 2018 | 1,099.00 deposit only |
| 15b | Same, but starts 1 Jan 2019 | Oct 2018 / Nov 2018 | nothing / 1,099.00 deposit |
| 16 | Month with no charge = July | after June | next list is August |
| 17 | Ends 15 Mar | Mar | half price |
| 18 | Ends 16 Mar | Mar | full price |
| 19 | Half-month day set to 10, kindergarten starts 10 Jan | Jan 2017 | 616.62 (half) |
| 20 | Half-month day set to 10, kindergarten starts 9 Jan | Jan 2017 | 1,233.25 (full) |
| 21 | Meal scheme 300, kindergarten starts 16 Jan | Jan 2017 | 916.62 (half price + full meal) |

Cases 1–15 match the old system's tests (numbers rewritten, not copied). Cases 5, 9, 19 and 21 show rounding. Cases 16–21 are new.

## Open questions

1. ~~**Half month.**~~ Decided: the 16th is the general rule, and each institution can change the day.
2. ~~**Meal scheme in half months.**~~ Decided: always the full amount.
3. ~~**Rounding.**~~ Decided: as in the old system, half an øre goes to the even øre. Done per line (see R13).
4. ~~**Deposit timing.**~~ Decided: "2 months ahead" means a December start pays in October. The old system also charged January starts in October (3 months ahead); that is fixed.
