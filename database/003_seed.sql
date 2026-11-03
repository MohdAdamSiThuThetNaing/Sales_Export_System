INSERT INTO SalesTransactions (
    Region,
    TransactionDate,
    CustomerName,
    Amount
)
SELECT
    CASE
        WHEN i % 4 = 0 THEN 'Bangkok'
        WHEN i % 4 = 1 THEN 'Singapore'
        WHEN i % 4 = 2 THEN 'Malaysia'
        ELSE 'Vietnam'
    END,

    TIMESTAMP '2024-01-01'
        + random() * INTERVAL '730 days',

    CASE
        WHEN i % 3 = 0 THEN 'ABC Corporation'
        WHEN i % 3 = 1 THEN 'XYZ Company'
        ELSE 'Retail Customer'
    END,

    ROUND((random() * 10000)::numeric, 2)

FROM generate_series(1, 100000) AS i;