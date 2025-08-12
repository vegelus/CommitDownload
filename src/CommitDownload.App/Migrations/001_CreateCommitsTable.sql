CREATE TABLE IF NOT EXISTS Commits (
                                       RepoOwner TEXT NOT NULL,
                                       RepoName  TEXT NOT NULL,
                                       Sha       TEXT PRIMARY KEY,
                                       Message   TEXT,
                                       Committer TEXT
);
