"""Shell-only OpenRouter environment handling for B3-E."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Mapping
from urllib.parse import urlsplit


DEFAULT_OPENROUTER_MODEL = "openrouter/manual-unconfigured"


class OpenRouterConfigError(ValueError):
    """Raised when the OpenRouter shell config is not usable."""


@dataclass(frozen=True)
class OpenRouterConfig:
    api_key_present: bool
    model_id: str
    base_url_present: bool = False
    base_url_host: str | None = None

    def as_public_dict(self) -> dict[str, object]:
        return {
            "provider": "openrouter",
            "api_key_present": self.api_key_present,
            "model_id": self.model_id,
            "base_url_present": self.base_url_present,
            "base_url_host": self.base_url_host,
        }


def load_openrouter_config(environ: Mapping[str, str]) -> OpenRouterConfig:
    api_key = environ.get("OPENROUTER_API_KEY", "")
    if not api_key.strip():
        raise OpenRouterConfigError("OPENROUTER_API_KEY is required for manual OpenRouter shell checks")

    model_id = environ.get("OPENROUTER_MODEL", "").strip() or DEFAULT_OPENROUTER_MODEL
    base_url_present, base_url_host = _safe_base_url_metadata(environ.get("OPENROUTER_BASE_URL", ""))
    return OpenRouterConfig(
        api_key_present=True,
        model_id=model_id,
        base_url_present=base_url_present,
        base_url_host=base_url_host,
    )


def _safe_base_url_metadata(raw_base_url: str) -> tuple[bool, str | None]:
    stripped = raw_base_url.strip()
    if not stripped:
        return False, None

    try:
        return True, urlsplit(stripped).hostname
    except ValueError:
        return True, None
