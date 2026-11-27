# Betalingsservice files

Two files: the collection file the manager uploads each month (delivery 0601), and the payment file the manager downloads from the bank afterwards (delivery 0602).

## Where the layouts come from

From the files the old system sent to and read from Betalingsservice for ten years. Those were accepted in production, so they are the best evidence there is. The official record layout ([Betalingsservice Guidelines for Data Suppliers](https://www.betalingsservice.dk/media/exxlsui1/bs_creditor_recordlayout.pdf)) could not be read here: the PDF has no extractable text without a PDF tool. **Before the first real upload, the layouts below should be checked against that guide.**

General: records are 128 characters, CRLF line endings, ISO-8859-1. Amounts are in øre, zero-filled, without sign. Numbers are right-aligned and zero-filled; text is left-aligned and space-filled.

## Collection file (0601), section 0112

Order: BS002, BS012, then per collection four BS022, one BS042 and its BS052 lines, then BS092, BS992.

| Record | Content |
|---|---|
| BS002 delivery start | CVR (8), `BS1`, `0601`, serial number (10) |
| BS012 section start | PBS number (8), `0112`, debtor group (5), data supplier identification (15), creation date `ddMMyyyy`, main text (60) |
| BS022 `0240` | Record 1 payer name, 2 "(child's first name)", 3 address, each 35 characters; record 9 postcode (4) |
| BS042 `0280` | Customer number = the child's payment number (15), collection date `ddMMyyyy`, sign code `1`, amount (13) |
| BS052 `0241` | One text line (60): first an empty line, then one per charge line, text left and amount right, Danish number format (`4.233,50`) |
| BS092 section end | Number of BS042, total amount (15), number of BS052, number of BS022 |
| BS992 delivery end | Number of sections (1), the same counts and total |

- Only charges above 0 are in the file. Payers without an agreement get a payment slip from Betalingsservice.
- Collection date = the 1st of the month. When the 1st is not a banking day, Betalingsservice moves the payment to the next banking day (its default; a creditor can choose the day before instead). The app shows that day.
- **Delivery deadline:** the file must reach Betalingsservice by the 6th-last banking day of the month before (Vejledning for kreditorer, Nov 2026). The app shows this date. Banking days exclude weekends, public holidays, the Friday after Ascension, 5 June, 24 and 31 December.
- **Delivery:** the institution sends the file straight to Betalingsservice, not through its bank.
- **Serial number:** the old system always wrote `0000000016`. The new one counts up by one for each file the institution makes. Whether Betalingsservice checks it is not confirmed.

## Payment file (0602), section 0211

| Record | Content |
|---|---|
| BS002 | Delivery type `0602` at position 17–20, creation date `ddMMyy` at 50–55 |
| BS012 | PBS number at 6–13 |
| BS042 `0236` | Automatic payment completed: customer number 26–40, collection date 50–55, amount 57–69, actual payment date 104–109, bookkeeping date 110–115, amount paid 116–128 |
| BS992 | Delivery end; a file without it is refused as incomplete |

- Lines can arrive with trailing spaces cut (the real file has 55-character lines), so they are padded back to 128.
- **Only `0236` is booked automatically.** Every other BS042 transaction code (rejected, cancelled, charged back, or a payment by payment slip) is listed for the manager with its customer number, and the rest of the file is still read. The old system tried to read code `0297` with different positions; those positions are not confirmed, so the new system does not guess.
- The same file is never imported twice (checked by content).

## Verified

- All tests build the records from the layout above and check length, positions, counts and totals.
- The real payment file from the old system (`D0933901-602.BS1`, 2018, two `0236` payments) reads correctly with the new reader. It was read locally only. It may hold real customer data and is not in this repo.
