
import logging
from uuid import UUID

from fastapi import (
    Depends,
    FastAPI,
    HTTPException,
    status,
)
from sqlalchemy import select
from sqlalchemy.exc import SQLAlchemyError
from sqlalchemy.orm import Session

from app.database import get_db
from app.models import ExportRequest
from app.schemas import (
    ExportRequestCreate,
    ExportRequestResponse,
)


logger = logging.getLogger(__name__)


app = FastAPI(
    title="Sales Export API",
    description="Self-service sales data export system",
    version="1.0.0",
)


@app.get(
    "/health",
    tags=["Health"],
    status_code=status.HTTP_200_OK,
)
def health_check() -> dict[str, str]:
    """Check API application availability."""

    return {
        "status": "healthy",
        "service": "Sales Export API",
    }


@app.post(
    "/api/exports",
    response_model=ExportRequestResponse,
    status_code=status.HTTP_202_ACCEPTED,
    tags=["Exports"],
)
def create_export_request(
    request: ExportRequestCreate,
    db: Session = Depends(get_db),
) -> ExportRequest:
    """
    Create a new sales export request.

    The request is saved as Pending and processed
    asynchronously by the .NET background worker.
    """

    export_request = ExportRequest(
        region=request.region,
        start_date=request.start_date,
        end_date=request.end_date,
        status="Pending",
    )

    try:
        db.add(export_request)
        db.commit()
        db.refresh(export_request)

        logger.info(
            "Export request created: request_id=%s, region=%s",
            export_request.id,
            export_request.region,
        )

        return export_request

    except SQLAlchemyError as exc:
        db.rollback()

        logger.exception(
            "Failed to create export request."
        )

        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to create export request.",
        ) from exc


@app.get(
    "/api/exports/{request_id}",
    response_model=ExportRequestResponse,
    status_code=status.HTTP_200_OK,
    tags=["Exports"],
)
def get_export_request(
    request_id: UUID,
    db: Session = Depends(get_db),
) -> ExportRequest:
    """
    Retrieve an export request and its current processing status.
    """

    try:
        statement = select(ExportRequest).where(
            ExportRequest.id == request_id
        )

        export_request = db.execute(
            statement
        ).scalar_one_or_none()

        if export_request is None:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="Export request not found.",
            )

        return export_request

    except HTTPException:
        raise

    except SQLAlchemyError as exc:
        logger.exception(
            "Failed to retrieve export request: request_id=%s",
            request_id,
        )

        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Failed to retrieve export request.",
        ) from exc
