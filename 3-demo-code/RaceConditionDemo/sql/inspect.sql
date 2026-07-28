-- =============================================================================
--  Optional "prove it at the database level" queries.
--  Keep SSMS / Azure Data Studio open on a second screen during the demo.
-- =============================================================================
USE RaceDemo;
GO

-- 1. Current state -------------------------------------------------------------
SELECT Id, Owner, Balance, RowVersion FROM dbo.Wallets;

-- Watch RowVersion change on every single UPDATE. Run the vulnerable demo,
-- then run this again: the value moved even though the vulnerable path never
-- looked at it. SQL Server maintains it for you regardless.


-- 2. The audit trail that contradicts the balance --------------------------------
SELECT Operation, COUNT(*) AS Confirmed, SUM(Amount) AS TotalClaimed
FROM dbo.Ledger
GROUP BY Operation;

-- After the vulnerable run: TotalClaimed will be far more than the amount that
-- actually left the wallet. That gap is the fraud number in a real incident.


-- 3. Show the lock, live (run DURING a pessimistic demo with thinkTimeMs high) ---
SELECT
    r.session_id,
    r.status,                    -- 'suspended' == blocked
    r.blocking_session_id,
    r.wait_type,
    r.wait_time  AS wait_ms,
    t.text       AS running_sql
FROM sys.dm_exec_requests r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
WHERE r.session_id > 50;

-- Point at blocking_session_id on the projector. That number is UPDLOCK doing
-- its job: request #2 is not reading stale data, it is not running at all.


-- 4. What locks are actually held -----------------------------------------------
SELECT
    l.request_session_id,
    l.resource_type,
    l.request_mode,              -- U = Update lock, X = Exclusive
    l.request_status,            -- GRANT / WAIT
    OBJECT_NAME(p.object_id) AS table_name
FROM sys.dm_tran_locks l
LEFT JOIN sys.partitions p ON l.resource_associated_entity_id = p.hobt_id
WHERE l.resource_database_id = DB_ID('RaceDemo')
ORDER BY l.request_session_id;


-- 5. Reset by hand if the API is misbehaving ------------------------------------
-- DELETE FROM dbo.Ledger;
-- UPDATE dbo.Wallets SET Balance = 1000;
