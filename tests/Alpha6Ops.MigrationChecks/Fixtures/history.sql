-- Frozen v0.22.0 storage schema and synthetic historical rows. Do not generate
-- this fixture with the current writer: migrations must still read this format.
CREATE TABLE flight(id TEXT PRIMARY KEY, pilot TEXT NOT NULL, source TEXT NOT NULL, source_detail TEXT, aircraft TEXT NOT NULL, origin TEXT, destination TEXT, flight_number TEXT, started_utc TEXT NOT NULL, ended_utc TEXT, final_phase TEXT, app_version TEXT NOT NULL);
CREATE TABLE flight_event(flight_id TEXT NOT NULL REFERENCES flight(id), sequence INTEGER NOT NULL, kind TEXT NOT NULL, recorded_utc TEXT NOT NULL, simulator_utc TEXT, detail_json TEXT NOT NULL, PRIMARY KEY(flight_id, sequence));
INSERT INTO flight VALUES('baseline-flight','Test pilot','live',NULL,'Airbus A320','YSSY','YMHB','BASE718','2026-09-17T02:05:00.0000000+00:00','2026-09-17T04:13:00.0000000+00:00','Complete','0.22.0');
INSERT INTO flight_event VALUES('baseline-flight',0,'tracking_event','2026-09-17T02:06:01+00:00','2026-09-17T02:06:00+00:00','{"Title":"Block-out / taxi out","Summary":"Movement confirmed","Detail":"GS 3 KT"}');
INSERT INTO flight_event VALUES('baseline-flight',1,'pirep_submitted','2026-09-17T04:14:00+00:00',NULL,'{"status":"Completed","actualOut":"2026-09-17T02:06:00+00:00","actualIn":"2026-09-17T04:13:00+00:00"}');
