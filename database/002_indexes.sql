CREATE INDEX idx_sales_region_date
ON SalesTransactions (Region, TransactionDate);

CREATE INDEX idx_export_pending
ON ExportRequests (CreatedAt, Id)
WHERE Status = 'Pending';

CREATE INDEX idx_sales_customer
ON SalesTransactions (CustomerName);