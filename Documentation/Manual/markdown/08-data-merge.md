# Data merge (variable data)

**Data merge** prints a batch of labels from a spreadsheet — one label per row — filling in the parts
that change (a name, a serial, a price, a date) from your data while the rest of the design stays put.
It's mail-merge for labels.

The flow is: **prepare a CSV**, **put tokens where the data should go**, then **preview and print**.

## Preparing your data (CSV)

Thermalith reads a **CSV** file. The simplest way to make one is to save (or export) a sheet from Excel,
Google Sheets, LibreOffice, or a database as *CSV*.

The shape Thermalith expects:

- **The first row is a header row** — its cells are the **column names** you'll bind to.
- **Each following row is one label**, printed in order.
- **Every value is treated as text.** Thermalith doesn't reformat numbers or dates — they print exactly
  as they appear in the cell, so format them the way you want them to read before you export.
- **An empty cell prints blank** for that label.

If you're exporting from a spreadsheet, flatten it first: no merged cells, no multi-row headers, no
subtotal or grouping rows, and export the **values** (not formulas).

A column can also be referred to by its **position** instead of its name (see tokens below). If a header
cell is left **blank**, or two columns share the same header, that column can only be used by position.

## Loading a data file

Open the **Data Merge** menu and choose **Select Data File…**, then pick your CSV. Thermalith shows a
short summary (file name, row count, column count), and the **Columns** and **Print Merge** items become
available. **Clear Data Source** unloads it again.

## Binding fields to columns (tokens)

You bind a field to a column by putting a **token** in its value. Any text, barcode, or QR value can
hold tokens, mixed in with fixed text:

- `{"Column name"}` — bind by **column name** (use this form when the name has spaces).
- `{3}` — bind by **column position**, 1 for the first column, 2 for the second, and so on.

For example, a text element reading `Part {"Part No"} — {"Description"}` prints *Part A-100 — Hex bolt*
for a row whose **Part No** is `A-100` and **Description** is `Hex bolt`.

You don't have to type tokens by hand. Click the field you want to fill, then open **Data Merge →
Columns** and click a column — its token is inserted at the cursor. Each entry shows the exact token it
will insert.

> A token that doesn't match any column is left visible as-is (e.g. `{"Prise"}`), so a typo is easy to
> spot. To print a literal brace, double it: `{{` prints `{`.

Auto-size text grows to fit each row's value, so a long entry isn't cut off (see **Auto-size** in
*[Element types](05-element-types.md)*).

## Previewing the rows

Turn on **Data Merge → Preview Merge Data** to see real labels on the canvas. A small bar appears with
**◀ / Row *n* of *m* / ▶** so you can step through the rows and check each one; **Close** (or turning
the menu item off) returns to the normal design view, where tokens show as `{…}` placeholders again.

## Printing the batch

With a printer connected (see *[Printing](07-printing.md)*), choose **Data Merge → Print Merge…**.

- Thermalith first tells you **how many labels the run will print** (rows × the **Copies** setting) and
  asks you to confirm — so a large batch never starts by surprise.
- On printers with **RFID-tagged rolls** (such as the B1) it also checks the roll: if the run needs more
  labels than the roll holds, it prints **as many whole rows as fit**, then asks you to change the roll
  and print the rest as a fresh run. It never auto-continues across a roll change, so you get the chance
  to load different stock or adjust the design first.
- While printing, a progress dialog shows **Printing *k* / *m*** with a **Cancel** button. Cancelling
  stops after the current label finishes feeding.

<table width="100%"><tr>
<td width="50%" align="left" valign="bottom"><img src="assets/brand/evilgenius.png" alt="EvilGenius" height="56"></td>
<td width="50%" align="right" valign="bottom"><img src="assets/brand/evilgeniuslabsca.png" alt="EvilGeniusLabs.ca" height="26"></td>
</tr></table>
