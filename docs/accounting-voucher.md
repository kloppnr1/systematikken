# Bookkeeping voucher ("Bogføringsbilag")

The bookkeeper books each month's billing from one voucher. It has the same content as the old system's "Bogføringsbilag" button on the monthly billing screen. The bookkeeper enters it by hand in e-conomic or Dinero, so no import format is needed.

## Content

One PDF per month, available from the monthly billing screen once the month exists.

- Heading: the month, in Danish ("december 2026").
- One table with three columns and one row of amounts:

| Opkrævningssum | Opkrævninger | Tilskudssum |
|---|---|---|
| amount | amount | amount |

Amounts have two decimals and a decimal comma (3598,37), with no thousands separator, as in the old file.

## How each amount is worked out

All from the month's charge lines (see [fee rules](fee-rules.md)):

- **Opkrævninger** = the sum of price lines: nursery and kindergarten (full and half month), meal scheme and manual charges. Deposit and sign-up fee are *not* included.
- **Tilskudssum** = the sum of free-place subsidy and sibling discount.
- **Opkrævningssum** = everything charged this month minus subsidies: Opkrævninger + deposit + sign-up fee − Tilskudssum.

Never included: amounts carried over from earlier months (unpaid or overpaid) and payments. They were booked in the month they came from.

## Test case

One month with:
- nursery child, full month 4,233.50, free place 1,100.25, sibling discount 1,550.50, plus 1.00 unpaid from last month
- kindergarten child starting the 16th, half month 616.62, meal scheme 300.00
- deposit 1,099.00 for a child starting later
- payments received 4,000.00

| Opkrævningssum | Opkrævninger | Tilskudssum |
|---|---|---|
| 3598,37 | 5150,12 | 2650,75 |

The 1.00 carried over and the 4,000.00 in payments do not show.

## Changes from the old system

- The old button sent the three amounts through the web address, so whatever the screen showed went into the PDF. The new system works them out from the stored lines when the PDF is made.
- The old screen also showed payments received, the Betalingsservice total and the child count. They stay on the screen, not on the voucher, as before.
