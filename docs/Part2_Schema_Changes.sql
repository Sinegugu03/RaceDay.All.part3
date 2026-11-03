/* ============================================================
   RaceDay - Part 2 schema changes
   The Part 2 brief requires every Event to capture a distance and an
   event type (run, walk or cycle). These two columns were not in the
   Part 1 Events table, so Part 2 adds them. EF Core Code-First creates
   them automatically in the API database; this script applies the same
   change to the Part 1 database (RaceDayDB) so both stay consistent.
   ============================================================ */
USE RaceDayDB;
GO

IF COL_LENGTH('dbo.Events', 'EventType') IS NULL
    ALTER TABLE dbo.Events ADD EventType VARCHAR(10) NOT NULL
        CONSTRAINT DF_Events_EventType DEFAULT 'Run';
GO

IF COL_LENGTH('dbo.Events', 'DistanceKm') IS NULL
    ALTER TABLE dbo.Events ADD DistanceKm DECIMAL(6,2) NOT NULL
        CONSTRAINT DF_Events_DistanceKm DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Events_EventType')
    ALTER TABLE dbo.Events ADD CONSTRAINT CK_Events_EventType
        CHECK (EventType IN ('Run', 'Walk', 'Cycle'));
GO

-- Give the Part 1 seed events realistic values
UPDATE dbo.Events SET EventType = 'Run', DistanceKm = 21.10 WHERE Name = 'Durban Beachfront Marathon';
UPDATE dbo.Events SET EventType = 'Run', DistanceKm = 5.00  WHERE Name = 'Umhlanga Fun Run';
UPDATE dbo.Events SET EventType = 'Run', DistanceKm = 15.00 WHERE Name = 'Joburg City Trail Run';
GO

SELECT EventId, Name, EventType, DistanceKm FROM dbo.Events;
