# Why These Entities Exist

The entities were designed to answer one question: **how do you store a CSV file
that has unknown columns, unknown row count, and needs AI analysis on specific
cells — without hardcoding anything?**

Here's the reasoning behind each one.

---

## `Survey`

Someone uploads a CSV. You need a record that says *"this upload exists, who
submitted it, where the file is stored, and what state processing is in."*
Every other entity traces back to this one. It's the top of the chain.

Without it you'd have no way to group anything together or track whether
processing is done.

---

## `SurveyColumn`

Every CSV from a different event has different column names. One might have
`"How was the food?"`, another might have `"Rate your experience"`. You can't
hardcode column names into the schema.

So instead of columns being fixed in the database, you store the column
*definitions* as rows in their own table — one row per column per survey. The
`AnalyzeSentiment` flag on this entity is also critical: it marks which columns
contain free text that should be sent to the RoBERTa model.

---

## `SurveyResponse`

One record per row in the CSV. Its only job is to act as a **grouping parent**
for all the cell values in that row, and to remember the original row order via
`RowIndex`.

Without this, there'd be no way to say "these 10 cells all belong to the same
respondent."

---

## `ResponseValue`

This is where the actual data lives — one record per cell (row × column
intersection). It stores the raw string from the CSV.

This is the hardest one to justify at first because it feels like a lot of rows.
But it's the only way to handle variable schemas. If every CSV had the same
columns, you'd just add those columns directly to `SurveyResponse`. Since they
don't, you normalize the cells into their own table and reference the column
definition by ID.

---

## `SentimentResult`

The RoBERTa model returns a label (`positive`, `neutral`, `negative`) and three
probability scores for a given piece of text. That output needs to live somewhere
and be tied back to the exact cell it came from.

It's one-to-one with `ResponseValue` — not every cell gets a sentiment result,
only the ones whose parent column has `AnalyzeSentiment = true`. Keeping it as a
separate table means you can query sentiment data independently without touching
the raw CSV data at all.

---

## `KpiAggregate`

This one is purely a **performance decision**, not a data modeling decision.

The dashboard needs to show things like "60% positive responses for the 'overall
experience' column across 10,000 submissions." Without this entity, every
dashboard load would run `AVG()` and `COUNT()` across potentially millions of
`SentimentResult` rows in real time. With thousands of users viewing dashboards
simultaneously, that query would be brutal.

So instead, the NLP worker computes these aggregates once when a survey finishes
and writes a single summary row per column. The dashboard reads one flat row
instead of scanning millions.

---

## The Chain in One Sentence Per Level

```
Survey          → "this CSV was uploaded"
SurveyColumn    → "this CSV had these columns"
SurveyResponse  → "this is one row from that CSV"
ResponseValue   → "this is one cell from that row"
SentimentResult → "this is what the AI said about that cell"
KpiAggregate    → "here is the pre-computed summary for the dashboard"
```

Each level exists because the level above it couldn't store that information
without either hardcoding assumptions or duplicating data.
