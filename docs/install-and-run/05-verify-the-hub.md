# Module 5: Verify the Hub

[⬅ Back to overview](README.md) · [⬅ Module 4](04-tokens.md)

Where you are: the Hub is configured. About 3 minutes.

Two quick checks prove the Hub is alive: the ping route and the Swagger page.

## What you'll do

- Call the ping route
- Open the Swagger page

## Steps

1. Call the ping route from a terminal:

   ```text
   curl http://localhost:9944/api/v4/g4/ping
   ```

   A running Hub answers with status 200. The release workflow uses the same route to wait for the Hub to be ready.

2. Open `http://localhost:9944/swagger` in a browser. The page lists the Hub API. The Swagger documents are served at `swagger/{documentName}/docs.json`.

   ![The Swagger page of the Hub](images/05-swagger.png)

   TODO(screenshot): images/05-swagger.png, the Swagger page at http://localhost:9944/swagger.

3. If your port is not 9944, replace it in both addresses.

## Check your work

- `ping` returns status 200.
- The Swagger page loads and lists controllers.

## Where the facts come from

| Fact | Source |
| --- | --- |
| Route `/api/v4/g4/[controller]` with a GET action | `src\G4.Services.Hub\Controllers\PingController.cs:11-15` |
| Readiness probe uses the ping route | `.github\workflows\release-pipline.yml` (step "Verify Service Readiness") |
| Swagger UI prefix `swagger` | `src\G4.Services.Hub\Program.cs:185-192` (`UseSwaggerUI`, `RoutePrefix = "swagger"`) |

Next: [Module 6: Logging](06-logging.md)
