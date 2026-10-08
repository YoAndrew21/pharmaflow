# Canonical fictional demo data

`catalog.json` is the single source of demo records for the frontend and future ASP.NET Core/PostgreSQL seed. It contains nine independently authored illustrative presentations, three fictional branches, and a fixed snapshot. Generic ingredient names are used for recognition; presentation combinations, prescription flags, stock, prices, branches, schedules, and timestamps are demonstration values, not real pharmacy facts or medical guidance.

All prices use fictional USD reference values in integer minor units for the stated package. Missing observations are intentionally omitted rather than encoded as unavailable. Null prices mean no reference price was provided. Preserve IDs, slugs, relationships, and timestamps when migrating; never replace snapshot times with the current clock. Do not add copies of this dataset inside the frontend or future backend.
