import uuid

from sqlalchemy import (
    Column,
    String,
    Date,
    DateTime,
    Numeric,
    BigInteger,
    Text,
    CheckConstraint,
    Index,
    func
)

from sqlalchemy.dialects.postgresql import UUID

from app.database import Base


class SalesTransaction(Base):

    __tablename__ = "sales_transactions"

    id = Column(BigInteger, primary_key=True)

    region = Column(String(100), nullable=False)

    transaction_date = Column(
        DateTime,
        nullable=False
    )

    customer_name = Column(
        String(255),
        nullable=False
    )

    amount = Column(
        Numeric(18, 2),
        nullable=False
    )


class ExportRequest(Base):

    __tablename__ = "export_requests"

    id = Column(
        UUID(as_uuid=True),
        primary_key=True,
        default=uuid.uuid4
    )

    region = Column(String(100), nullable=False)

    start_date = Column(Date, nullable=False)

    end_date = Column(Date, nullable=False)

    status = Column(
        String(20),
        nullable=False,
        default="Pending"
    )

    file_path = Column(Text, nullable=True)

    error_message = Column(Text, nullable=True)

    created_at = Column(
        DateTime,
        nullable=False,
        server_default=func.now()
    )

    started_at = Column(DateTime, nullable=True)

    completed_at = Column(DateTime, nullable=True)

    __table_args__ = (
        CheckConstraint(
            "status IN ('Pending', 'Processing', 'Completed', 'Failed')",
            name="valid_status"
        ),
        CheckConstraint(
            "end_date >= start_date",
            name="valid_date_range"
        ),
    )
