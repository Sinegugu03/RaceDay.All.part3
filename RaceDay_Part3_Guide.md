# PROG6212 POE – Part 3 (MVC, Azure Blob Storage, Docker): Complete Guide

> **Read first.** I wrote this without being able to compile it here (no .NET SDK or NuGet access). Your first job is to **build and run the tests** (Step 3). If anything fails, copy the exact error text and send it to me and I'll fix it. The video also makes you explain the MVC structure, Blob Storage and Docker, so read through the code once.

---

## 1. What Part 3 wants (in plain words)

| # | Requirement | Where it's done |
|---|---|---|
| 1 | **MVC app** that gets *all* data from the Part 2 API. No database code and no copied business rules in the MVC project. | `Part3-MVC/RaceDay.Mvc` – every controller calls `IApiClient` |
| 2 | **Register and log in** through MVC (via the API). **Session** keeps the user and role on every page. Different menu and pages per role. Unauthenticated users blocked. Log out and redirect. | `AccountController`, `UserSession`, `[RequireLogin("Organiser")]`, role-based `_Layout` menu |
| 3 | **Organiser interface**: dashboard (events, enrolments per event, upcoming dates), events page (create/edit/delete with name, description, date, location, distance, type), banner upload, categories page, enrolments page, results entry page | `OrganiserController` + `Views/Organiser/*` |
| 4 | **Participant interface**: home with filters, event details + categories, enrol, my enrolments with status, **My Results** (e.g. "47th out of 312"), profile + profile picture | `HomeController`, `EventsController`, `ParticipantController`, `ProfileController` |
| 5 | **Azure Blob Storage inside the Part 2 API.** MVC must never talk to Azure. Organisers upload banners; images shown on the event detail page | `BlobStorageService`, `ImagesController` (API); MVC `ImagesController` just asks the API |
| 6 | **Docker**: MVC runs in a container, good Dockerfile, run command in the README, behaves like local | `Part3-MVC/Dockerfile`, `docker-compose.yml` (bonus: whole stack) |
| 7 | **UI design**: RaceDay sporting theme, Bootstrap, role-appropriate nav, colour-coded statuses (green Confirmed, amber Pending), graceful errors, consistent styling | `wwwroot/css/site.css`, `_Layout`, `_StatusBadge`, friendly error pages |
| 8 | **GitHub + CI/CD**: Part 3 in a branch or subfolder, **20+ commits**, workflow also builds the MVC project | `Part3-MVC` folder, updated `part2-build-test.yml` |

**Submission:** GitHub link on ARC. The repo must include all source code, a **Dockerfile**, a **complete README with Docker run instructions** and the **YouTube link**. Unlisted YouTube video: the full running application, the MVC structure, the Blob Storage integration and the Docker setup.

---

## 2. What I changed in your Part 2 API (Part 3 needs these)

| Change | Why |
|---|---|
| `Azure.Storage.Blobs` package, `IBlobStorageService` + `AzureBlobStorageService` | Image storage in Azure Blob Storage (private container) |
| `POST /api/events/{id}/banner`, `GET /api/events/{id}/banner` | Organiser banner upload; image served through the API |
| `POST /api/users/me/picture`, `GET /api/users/{id}/picture` | Profile pictures |
| `GET /api/events/mine` | Organiser dashboard (events with enrolment counts) |
| `GET /api/events/{id}/enrolments` | Organiser enrolments page (category + status + result) |
| `PUT /api/enrolments/{id}/status` | Organiser confirms (green) or cancels enrolments |
| `PUT /api/enrolments/{id}/result` | Correct a result after capturing it |
| `GET /api/users/me/results` | "My Results" with position **and total finishers** |
| New enrolments start as **Pending** | So amber Pending and green Confirmed both appear (this was the Part 1 plan's default) |
| 2 nullable columns: `Events.BannerBlobName`, `Users.ProfilePictureBlobName` | Remember which blob belongs to what (needs a new migration) |
| DB start-up retry, `Dockerfile` | So the API can start in Docker while SQL Server is still booting |
| 26 new tests (77 total), fake blob store in tests | Tests need no Azure |

**Image rules:** JPG/PNG/WEBP only, max 5 MB, the first bytes of the file are checked so a renamed text file is rejected. Blob storage not configured gives a friendly 503.

---

## 3. Step-by-step

### Step 1 – Put the files in your repo
1. Unzip `RaceDay_Part3.zip` into your repo folder (the one that already contains `Part2-API`). Choose **Replace** when Windows asks. Do **not** delete your `Part2-API/RaceDay.Api/Migrations` folder (the zip doesn't contain one).
2. You should now have, at the repo root: `Part2-API/`, `Part3-MVC/`, `RaceDay.All.sln`, `docker-compose.yml`, `.github/workflows/part2-build-test.yml` (replaced).
3. Don't commit yet. You'll commit in pieces in Step 7.

> **Which repo?** Use the repo that has Part 2. The brief wants Part 3 in a clearly named branch or subfolder of your repo. `Part3-MVC` is the subfolder. If your repo layout is different (for example the solution isn't inside `Part2-API`), tell me and I'll adjust the paths.

### Step 2 – Create the Azure Blob Storage (or use the emulator)
**Option A – the emulator (no Azure account):**
```
docker run -d -p 10000:10000 mcr.microsoft.com/azure-storage/azurite azurite-blob --blobHost 0.0.0.0 --skipApiVersionCheck
```
The default setting `UseDevelopmentStorage=true` already works with it.

**Option B – a real Azure Storage account (better for the video):**
1. Azure Portal → **Create a resource → Storage account**. Pick a name, region and *Standard / LRS* → Create.
2. Open the account → **Security + networking → Access keys → Show** → copy **Connection string (key1)**.
3. Save it as a *user secret* so it is never committed:
   ```
   dotnet user-secrets init --project Part2-API/RaceDay.Api
   dotnet user-secrets set "BlobStorage:ConnectionString" "PASTE_CONNECTION_STRING" --project Part2-API/RaceDay.Api
   ```
4. You don't need to create a container. The API creates the private `raceday-images` container the first time you upload.

### Step 3 – Build, add the migration, run the tests
1. Open **`RaceDay.All.sln`** in Visual Studio. **Build → Rebuild Solution.** Send me any errors.
2. **Add the migration for the two new columns** (Package Manager Console, *Default project* = `RaceDay.Api`; or the terminal commands in brackets):
   ```
   Add-Migration AddImageSupport
   Update-Database
   ```
   (`dotnet ef migrations add AddImageSupport --project Part2-API/RaceDay.Api` then `dotnet ef database update --project Part2-API/RaceDay.Api`)
   If you get "There is already an object named…", the database has tables without migration history: `dotnet ef database drop --project Part2-API/RaceDay.Api --force`, then `dotnet ef database update --project Part2-API/RaceDay.Api`.
3. **Test → Run All Tests.** Expect **77 passed**.

### Step 4 – Run the system locally
1. Make sure SQL Server is running and (Option A) the Azurite container is up.
2. Right-click the solution → **Configure Startup Projects → Multiple startup projects** → set **RaceDay.Api** and **RaceDay.Mvc** to *Start*. Use the **http** launch profiles. Press **F5**.
3. API: http://localhost:5080/swagger. Website: **http://localhost:5090**.
4. Try it (demo accounts, password `Password123!`):
   - `participant@raceday.test`: find a race, open it, **Enrol** (status shows amber **Pending**), see **My enrolments**, edit **Profile** and upload a picture.
   - `organiser@raceday.test`: **Dashboard**, **Create event** with a banner image, add **Categories**, open **Enrolments** and set the participant to **Confirmed** (turns green), open **Results**, enter a finish time and position, **Save & publish**.
   - Log back in as the participant: **My results** shows e.g. "1st out of 1 finishers".
5. Open the event page again and check the banner shows. It comes from Blob Storage through the API.

### Step 5 – Docker
**Whole system, one command** (from the repo root, with Docker Desktop running):
```
docker compose up --build
```
Website http://localhost:8081, API http://localhost:5080/swagger. The first build takes a few minutes. It needs about 3 GB of free memory because of SQL Server. Stop with `Ctrl+C`, then `docker compose down`.

**Only the MVC container** (this is the "Docker run command" the brief wants in the README; the API must be running on your PC):
```
docker build -t raceday-mvc ./Part3-MVC
docker run --rm -p 8081:8080 -e ApiSettings__BaseUrl=http://host.docker.internal:5080 raceday-mvc
```
Start the API so the container can reach it: `dotnet run --project Part2-API/RaceDay.Api --urls http://0.0.0.0:5080`.

Check the site in the container works exactly like it did locally (log in, enrol, banner image).

### Step 6 – README
Replace your repo's `README.md` with the `README.md` I gave you. Then:
1. Replace `PASTE YOUR YOUTUBE LINK HERE` with your video link.
2. Edit the README on GitHub and drag your green-build screenshot onto the placeholder line. GitHub inserts a working image link.

### Step 7 – Commits (20 or more)
Make real, separate commits as you add the pieces. Suggested sequence (27 commits). Run from the repo root:
```bash
git add Part2-API/RaceDay.Api/RaceDay.Api.csproj Part2-API/RaceDay.Api/Models Part2-API/RaceDay.Api/Data/RaceDayDbContext.cs
git commit -m "Part 3: add Blob Storage package and image columns"

git add Part2-API/RaceDay.Api/Migrations
git commit -m "Part 3: add AddImageSupport migration"

git add Part2-API/RaceDay.Api/Services/BlobStorageService.cs
git commit -m "Part 3: add Azure Blob Storage service"

git add Part2-API/RaceDay.Api/Controllers/ImagesController.cs
git commit -m "Part 3: add banner and profile picture endpoints"

git add Part2-API/RaceDay.Api/Dtos Part2-API/RaceDay.Api/Controllers/EventsController.cs
git commit -m "Part 3: add organiser dashboard endpoint"

git add Part2-API/RaceDay.Api/Controllers/EnrolmentsController.cs
git commit -m "Part 3: add event enrolments and status endpoints"

git add Part2-API/RaceDay.Api/Controllers/ResultsController.cs
git commit -m "Part 3: add my-results and result correction endpoints"

git add Part2-API/RaceDay.Api/Program.cs Part2-API/RaceDay.Api/appsettings.json
git commit -m "Part 3: register blob storage and add start-up DB retry"

git add Part2-API/RaceDay.Tests/FakeBlobStorage.cs Part2-API/RaceDay.Tests/RaceDayFactory.cs
git commit -m "Part 3: add fake blob storage for tests"

git add Part2-API/RaceDay.Tests/ImageTests.cs
git commit -m "Part 3: add image upload tests"

git add Part2-API/RaceDay.Tests/WorkflowTests.cs
git commit -m "Part 3: add tests for new endpoints"

git add Part3-MVC/RaceDay.Mvc/RaceDay.Mvc.csproj Part3-MVC/RaceDay.Mvc/Program.cs Part3-MVC/RaceDay.Mvc/appsettings.json Part3-MVC/RaceDay.Mvc/appsettings.Development.json Part3-MVC/RaceDay.Mvc/Properties Part3-MVC/.gitignore
git commit -m "Part 3: scaffold MVC project"

git add Part3-MVC/RaceDay.Mvc/Services/ApiClient.cs
git commit -m "Part 3: add API client that forwards the API session"

git add Part3-MVC/RaceDay.Mvc/Services/UserSession.cs Part3-MVC/RaceDay.Mvc/Filters
git commit -m "Part 3: add session helpers and role-based access filter"

git add Part3-MVC/RaceDay.Mvc/Models Part3-MVC/RaceDay.Mvc/Helpers
git commit -m "Part 3: add models, view models and helpers"

git add Part3-MVC/RaceDay.Mvc/Controllers/AppController.cs Part3-MVC/RaceDay.Mvc/Controllers/AccountController.cs Part3-MVC/RaceDay.Mvc/Views/Account
git commit -m "Part 3: add register, login and logout"

git add Part3-MVC/RaceDay.Mvc/Views/_ViewImports.cshtml Part3-MVC/RaceDay.Mvc/Views/_ViewStart.cshtml Part3-MVC/RaceDay.Mvc/Views/Shared Part3-MVC/RaceDay.Mvc/wwwroot
git commit -m "Part 3: add layout, role-based navigation and RaceDay theme"

git add Part3-MVC/RaceDay.Mvc/Controllers/HomeController.cs Part3-MVC/RaceDay.Mvc/Views/Home
git commit -m "Part 3: add home page with event filters"

git add Part3-MVC/RaceDay.Mvc/Controllers/EventsController.cs Part3-MVC/RaceDay.Mvc/Views/Events
git commit -m "Part 3: add event details and enrolment"

git add Part3-MVC/RaceDay.Mvc/Controllers/ParticipantController.cs Part3-MVC/RaceDay.Mvc/Views/Participant
git commit -m "Part 3: add my enrolments and my results pages"

git add Part3-MVC/RaceDay.Mvc/Controllers/ProfileController.cs Part3-MVC/RaceDay.Mvc/Views/Profile
git commit -m "Part 3: add profile page with picture upload"

git add Part3-MVC/RaceDay.Mvc/Controllers/OrganiserController.cs Part3-MVC/RaceDay.Mvc/Views/Organiser
git commit -m "Part 3: add organiser dashboard and management pages"

git add Part3-MVC/RaceDay.Mvc/Controllers/ImagesController.cs
git commit -m "Part 3: serve images through the API"

git add Part3-MVC/Dockerfile Part3-MVC/.dockerignore
git commit -m "Part 3: add MVC Dockerfile"

git add Part2-API/Dockerfile Part2-API/.dockerignore docker-compose.yml
git commit -m "Part 3: add API Dockerfile and docker-compose"

git add RaceDay.All.sln .github/workflows/part2-build-test.yml
git commit -m "Part 3: update GitHub Actions to build the MVC project"

git add README.md
git commit -m "Part 3: add README with Docker instructions"

git push origin main     # use your branch name if you aren't on main
```
Later: a commit for the CI screenshot and one for the YouTube link. Bug fixes add more. (If `git add` complains about a path, that file didn't change; skip that line.)

### Step 8 – Green CI and screenshot
1. GitHub → **Actions** → open the newest "Build and Test (API + MVC)" run.
2. Wait for **both jobs green**: *Build API, MVC and run unit tests* and *Build Docker images*.
3. Expand the steps (Restore, Build API tests and MVC project, Run unit tests), screenshot with `Win + Shift + S`.
4. Drag it into the README placeholder (Step 6). **Use a real screenshot of a real run only.** If the run is red, open the failed step, send me the error and fix it first.

---

## 4. Screenshots worth taking (for the README, document or video)
1. Green GitHub Actions run (required).
2. Participant **My enrolments** showing both amber **Pending** and green **Confirmed**.
3. **My results** showing a position such as "1st out of 2 finishers".
4. An event page showing the banner image.
5. The blob inside your Azure Storage account (Storage browser → `raceday-images` → `events/…`) or Azurite.
6. `docker compose up` running, and the site open on port 8081.

## 5. Video script (about 10–12 minutes, unlisted on YouTube)
1. **Intro (30 s):** "I'm Sinegugu Sibanyoni, ST10479060. This is Part 3 of PROG6212: the RaceDay MVC website, Azure Blob Storage and Docker."
2. **Architecture (1½ min):** Show the diagram in the README. "The MVC site has no database and no business rules. Everything goes through the Part 2 API. Images go from MVC to the API to Azure Blob Storage."
3. **MVC structure (2½ min):** In Solution Explorer: `Controllers` (Account, Home, Events, Participant, Organiser, Profile, Images), `Views`, `Models` (API models and view models), `Services/ApiClient.cs` (one place that calls the API and forwards the session cookie), `Filters/RequireLoginAttribute.cs` (blocks pages by login and role), `wwwroot/css/site.css` (theme).
4. **Authentication and roles (2 min):** Register a new participant, log in. Show the menu. Open an organiser URL by typing it → "access denied". Log out → try a protected page → redirected to login. Log in as the organiser and show the different menu and dashboard.
5. **Full demo (3 min):** Organiser creates an event with a banner, adds categories. Participant filters events, enrols (amber Pending). Organiser confirms (green). Organiser enters a result. Participant opens **My results**. Show the profile picture upload.
6. **Blob Storage (1½ min):** Show `BlobStorageService.cs` and `ImagesController` (API). Explain: private container, validation (type, size, file signature), MVC fetches images from the API. Show the blob in the Azure portal (or Azurite).
7. **Docker (1½ min):** Show the Dockerfile (multi-stage build, non-root user, port 8080). Run `docker build` and the `docker run` command (or `docker compose up --build`). Open the site on 8081 and log in to show it behaves like the local one.
8. **CI and close (45 s):** Show the green Actions run (it builds API, tests, MVC and the Docker images) and your commit history. "Thanks for watching."

## 6. What to submit
- **GitHub repository link on ARC.**
- The repo must contain: all source code, the **Dockerfile** (`Part3-MVC/Dockerfile`), the **README with Docker run instructions**, and the **YouTube link**.
- **Unlisted YouTube video** (full running app, MVC structure, Blob Storage integration, Docker setup).
- 20+ Part 3 commits; updated workflow building the MVC project; green-build screenshot in the README.

**Final checklist**
- [ ] `dotnet test` → 77 passed locally
- [ ] Both CI jobs green on the latest commit; screenshot in README renders
- [ ] README has the Docker run command and the YouTube link works (video is **Unlisted**)
- [ ] `docker compose up --build` (or `docker run`) works on your machine
- [ ] Banner and profile images work (Blob Storage configured)
- [ ] 20+ commits; repo is **Public** (or your marker has access)
- [ ] No Azure connection string or password committed to the repo

## 7. Troubleshooting
| Problem | Fix |
|---|---|
| Build error | Send me the full error text |
| `There is already an object named…` on `Update-Database` | Drop and recreate: `dotnet ef database drop --project Part2-API/RaceDay.Api --force`, then `dotnet ef database update …` |
| Website says "We can't reach the RaceDay service" | The API isn't running, or `ApiSettings:BaseUrl` is wrong. Locally it is `http://localhost:5080` |
| Image upload says "This feature is temporarily unavailable" | Blob Storage isn't configured or Azurite isn't running (Step 2) |
| Docker container can't reach the API | Start the API with `--urls http://0.0.0.0:5080`; use `host.docker.internal` in the run command |
| `docker compose up` – API restarts at first | Normal while SQL Server boots; it retries for about a minute |
| Pending/Confirmed never both show | New enrolments are Pending; confirm one as the organiser |
| Logged out suddenly | Sessions last 30 minutes idle and reset when the API restarts. Log in again |
| Actions fails: "project not found" | The workflow expects `RaceDay.All.sln` at the repo root and the `Part2-API` and `Part3-MVC` folders |

If you get stuck, tell me the step number and paste the exact error.
