from fastapi import FastAPI, Depends, HTTPException, status
from sqlalchemy.orm import Session
from sqlalchemy import select

from app.database import get_db
from app.models import ExportRequest
from app.schemas import (
    ExportRequestCreate,
    ExportRequestResponse
)

app = FastAPI(
    title="Sales Export API",
    description="Self-service sales data export system",
    version="1.0.0"
)


@app.get("/health")
def health_check():
    return {
        "status": "healthy",
        "service": "Sales Export API"
    }


@app.post(
    "/api/exports",
    response_model=ExportRequestResponse,
    status_code=status.HTTP_202_ACCEPTED
)
def create_export_request(
    request: ExportRequestCreate,
    db: Session = Depends(get_db)
):

    export_request = ExportRequest(
        region=request.region,
        start_date=request.start_date,
        end_date=request.end_date,
        status="Pending"
    )

    try:
        db.add(export_request)
        db.commit()
        db.refresh(export_request)

        return export_request

    except Exception:
        db.rollback()

        raise HTTPException(
            status_code=500,
            detail="Failed to create export request"
        )


@app.get(
    "/api/exports/{request_id}",
    response_model=ExportRequestResponse
)
def get_export_request(
    request_id: str,
    db: Session = Depends(get_db)
):

    from uuid import UUID

    try:
        parsed_id = UUID(request_id)
    except ValueError:
        raise HTTPException(
            status_code=400,
            detail="Invalid export request ID"
        )

    export_request = db.execute(
        select(ExportRequest).where(
            ExportRequest.id == parsed_id
        )
    ).scalar_one_or_none()

    if export_request is None:
        raise HTTPException(
            status_code=404,
            detail="Export request not found"
        )

    return export_request
