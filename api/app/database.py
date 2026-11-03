
import logging
import os
from collections.abc import Generator

from sqlalchemy import create_engine, text
from sqlalchemy.engine import Engine
from sqlalchemy.orm import Session, declarative_base, sessionmaker


logger = logging.getLogger(__name__)


def get_database_url() -> str:
    """
    Resolve the database connection URL from environment variables.
    """

    database_url = os.getenv("DATABASE_URL")

    if not database_url:
        raise RuntimeError(
            "DATABASE_URL environment variable is not configured."
        )

    return database_url


DATABASE_URL = get_database_url()


engine: Engine = create_engine(
    DATABASE_URL,
    pool_pre_ping=True,
    pool_size=5,
    max_overflow=10,
    pool_timeout=30,
    pool_recycle=1800,
    echo=False,
    future=True,
)


SessionLocal = sessionmaker(
    bind=engine,
    class_=Session,
    autoflush=False,
    expire_on_commit=False,
)


Base = declarative_base()


def get_db() -> Generator[Session, None, None]:
    """
    Provide a database session for the lifetime of an API request.

    The session is always closed after the request completes.
    """

    db = SessionLocal()

    try:
        yield db

    except Exception:
        db.rollback()
        raise

    finally:
        db.close()


def check_database_connection() -> bool:
    """
    Verify that the database is reachable.
    """

    try:
        with engine.connect() as connection:
            connection.execute(text("SELECT 1"))

        return True

    except Exception:
        logger.exception("Database connectivity check failed.")
        return False
