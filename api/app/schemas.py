from datetime import date, datetime
from uuid import UUID

from pydantic import BaseModel, Field, ConfigDict, model_validator


class ExportRequestCreate(BaseModel):

    region: str = Field(
        min_length=1,
        max_length=100
    )

    start_date: date

    end_date: date

    @model_validator(mode="after")
    def validate_dates(self):

        if self.end_date < self.start_date:
            raise ValueError(
                "End date must be greater than or equal to start date"
            )

        return self


class ExportRequestResponse(BaseModel):

    model_config = ConfigDict(from_attributes=True)

    id: UUID

    region: str

    start_date: date

    end_date: date

    status: str

    file_path: str | None = None

    created_at: datetime
