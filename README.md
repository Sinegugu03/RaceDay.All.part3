# RaceDayPoePart3

**Student:** Sinegugu Sibanyoni (ST10479060)
**Module:** PROG6212 – Portfolio of Evidence, Part 3 (Automation of Web Application)

RaceDay is a race-event management system. Organisers create events (runs, walks and cycle races) with categories such as Under 20, Senior, 10km and 21km. Participants register, enrol in a category and view their results after the race.

This repository contains the **RaceDay API** (Part 2) and the **RaceDay MVC website** (Part 3), with Azure Blob Storage for images and Docker support.

## What is in this repository

| Folder / file | What it is |
|---|---|
| `Part2-API` | ASP.NET Core Web API: SQL Server (EF Core Code-First), session authentication, Swagger, Azure Blob Storage, unit tests |
| `Part3-MVC` | ASP.NET Core MVC website that gets all its data from the API, plus the `Dockerfile` |
| `docker-compose.yml` | Starts the whole system (SQL Server, Azure Blob emulator, API and website) with one command |
| `RaceDay.All.sln` | One solution containing the API, the tests and the MVC project |
| `.github/workflows` | GitHub Actions: builds the API, tests and MVC project, runs the tests and builds both Docker images |

## The two roles

**Organiser** – dashboard of their events (enrolments per event and upcoming dates), creates, edits and deletes events with a banner image, adds categories, views and confirms enrolments, and captures finish times and positions after a race.

**Participant** – browses and filters upcoming events, views event details and categories, enrols in a category, sees their enrolments with a colour-coded status (green = Confirmed, amber = Pending), views their results (for example "47th out of 312 finishers") and manages their profile and profile picture.

## How it fits together

```
Browser  →  Part3-MVC (website)  →  Part2-API  →  SQL Server
                                        └───────→  Azure Blob Storage (images)
```

The MVC website has no database access and no business rules. Every read and write goes through the API. Images are uploaded to the API, which stores them in Azure Blob Storage. The website never talks to Azure directly.

## Run with Docker (one command)

Needs Docker Desktop and about 3 GB of free memory.

```
git clone https://github.com/Sinegugu03/RaceDayPoePart3.git
cd RaceDayPoePart3
docker compose up --build
```

- **Website:** http://localhost:8081
- **API / Swagger:** http://localhost:5080/swagger

Demo accounts (password `Password123!`):

| Email | Role |
|---|---|
| organiser@raceday.test | Organiser |
| participant@raceday.test | Participant |

Stop with `Ctrl+C`, then `docker compose down`.

## Run only the website in Docker

With the API already running on your computer:

```
docker build -t raceday-mvc ./Part3-MVC
docker run --rm -p 8081:8080 -e ApiSettings__BaseUrl=http://host.docker.internal:5080 raceday-mvc
```

Start the API so the container can reach it:

```
dotnet run --project Part2-API/RaceDay.Api --urls http://0.0.0.0:5080
```

Then open http://localhost:8081.

## Run without Docker

You need the .NET 8 SDK and SQL Server (Express, LocalDB or full).

1. Open `RaceDay.All.sln`.
2. In `Part2-API/RaceDay.Api/appsettings.json`, set `Server=` in the connection string to your SQL Server.
3. Create the database:
   ```
   dotnet ef database update --project Part2-API/RaceDay.Api
   ```
4. Give the API an Azure Blob Storage connection string as a **user secret** (never commit it):
   ```
   dotnet user-secrets init --project Part2-API/RaceDay.Api
   dotnet user-secrets set "BlobStorage:ConnectionString" "<your connection string>" --project Part2-API/RaceDay.Api
   dotnet user-secrets set "BlobStorage:ContainerName" "raceday-images" --project Part2-API/RaceDay.Api
   ```
   (Without an Azure account you can use the emulator: `docker run -d -p 10000:10000 mcr.microsoft.com/azure-storage/azurite azurite-blob --blobHost 0.0.0.0 --skipApiVersionCheck`.)
5. Start the API: `dotnet run --project Part2-API/RaceDay.Api --launch-profile http`
6. Start the website: `dotnet run --project Part3-MVC/RaceDay.Mvc --launch-profile http` and open http://localhost:5090.

## Tests

```
dotnet test RaceDay.All.sln
```

77 unit tests run the real API in memory with an in-memory database and a fake blob store, so they need neither SQL Server nor Azure.

## CI/CD

The GitHub Actions workflow runs on every push. It builds the API, the tests and the MVC project, runs all unit tests, and builds both Docker images.

**Successful build screenshot:**

[Drag your green GitHub Actions screenshot here while editing this file on GitHub]

## Video walkthroughs

- Video 1: https://youtu.be/fr4OaNNtsGA
- Video 2: https://youtu.be/itXIqB6gN1A
