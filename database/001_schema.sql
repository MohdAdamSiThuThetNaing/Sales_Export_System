CREATE TABLE SalesTransactions (
    Id BIGSERIAL PRIMARY KEY,
    Region VARCHAR(100) NOT NULL,
    TransactionDate TIMESTAMP NOT NULL,
    CustomerName VARCHAR(255) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL
);

CREATE TABLE ExportRequests (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    Region VARCHAR(100) NOT NULL,

    StartDate DATE NOT NULL,
    EndDate DATE NOT NULL,

    Status VARCHAR(20) NOT NULL DEFAULT 'Pending',

    FilePath TEXT,

    ErrorMessage TEXT,

    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

    StartedAt TIMESTAMP,

    CompletedAt TIMESTAMP,

    CONSTRAINT valid_status CHECK (
        Status IN (
            'Pending',
            'Processing',
            'Completed',
            'Failed'
        )
    ),

    CONSTRAINT valid_date_range CHECK (
        EndDate >= StartDate
    )
);