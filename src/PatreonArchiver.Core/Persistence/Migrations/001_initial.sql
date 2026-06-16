-- Initial schema. Connection-level PRAGMAs (WAL, busy_timeout, foreign_keys)
-- are set by SqliteConnectionFactory, not here (journal_mode cannot change inside a transaction).

CREATE TABLE schema_version (
    version INTEGER NOT NULL
);

CREATE TABLE creators (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    handle       TEXT NOT NULL UNIQUE,
    display_name TEXT,
    stream_host  TEXT,
    patreon_url  TEXT
);

-- One archivable video post. (creator_id, token) is the natural key; token is
-- "{yyyymmdd}_{slug}_{hex}" — the original tool's seen_posts.txt identity.
CREATE TABLE posts (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    creator_id        INTEGER NOT NULL REFERENCES creators(id) ON DELETE CASCADE,
    token             TEXT    NOT NULL,
    stream_url        TEXT    NOT NULL,
    post_date         TEXT    NOT NULL,           -- ISO 'yyyy-MM-dd'
    slug              TEXT    NOT NULL,
    title             TEXT,
    resolved_iframe   TEXT,
    patreon_post_url  TEXT,
    video_id          TEXT,                        -- yt-dlp %(id)s
    status            INTEGER NOT NULL,            -- PostStatus
    file_path         TEXT,
    discovered_at     TEXT    NOT NULL,            -- ISO-8601 (UTC)
    published_at      TEXT,
    UNIQUE(creator_id, token)
);
CREATE INDEX ix_posts_creator_date ON posts(creator_id, post_date);
CREATE INDEX ix_posts_status       ON posts(creator_id, status);

-- Dedup set (replaces archive.txt). A yt-dlp archive line is "<extractor> <id>".
CREATE TABLE download_archive (
    creator_id  INTEGER NOT NULL REFERENCES creators(id) ON DELETE CASCADE,
    extractor   TEXT    NOT NULL,
    video_id    TEXT    NOT NULL,
    archived_at TEXT    NOT NULL,
    PRIMARY KEY (creator_id, extractor, video_id)
);

-- Per-creator coverage anchor + open gap window (replaces coverage.txt).
CREATE TABLE coverage_anchors (
    creator_id        INTEGER PRIMARY KEY REFERENCES creators(id) ON DELETE CASCADE,
    anchor_date       TEXT,                        -- newest continuously-covered date
    pending_gap_from  TEXT,                        -- prior anchor while a gap is open
    pending_gap_to    TEXT,                        -- snapshot oldest while a gap is open
    updated_at        TEXT NOT NULL
);

-- Audit trail of every download attempt.
CREATE TABLE download_history (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    post_id     INTEGER REFERENCES posts(id) ON DELETE SET NULL,
    source_url  TEXT    NOT NULL,
    outcome     INTEGER NOT NULL,                  -- DownloadOutcome
    exit_code   INTEGER,
    error       TEXT,
    started_at  TEXT    NOT NULL,
    finished_at TEXT    NOT NULL
);
