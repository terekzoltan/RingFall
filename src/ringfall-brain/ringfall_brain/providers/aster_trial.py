"""Offline-injected A1 OpenRouter request trial; no credential or network adapter.

The caller supplies a cooperative fake transport, an independent offline harness,
a counter and endpoint proof. Nothing here obtains keys or sends an HTTP request.
"""

from __future__ import annotations

import hashlib
import json
import re
import secrets
import threading
import time
from dataclasses import dataclass, field
from decimal import Decimal, InvalidOperation
from pathlib import Path
from typing import Any, Callable, Protocol

from ringfall_brain.cognition.aster_action_emitter import build_aster_action_candidates
from ringfall_brain.schemas.validator import BrainValidationError, validate_packet_json


ENDPOINT = "https://openrouter.ai/api/v1/chat/completions"
MAX_INPUT = 2000
MAX_OUTPUT = 512
MAX_CALLS = 3
TIMEOUT_SECONDS = 30
MAX_WALL_SECONDS = 120
PER_CALL_USD = Decimal("0.001")
TOTAL_USD = Decimal("0.003")
_EFFORTS = frozenset({"none", "minimal", "low", "medium", "high", "xhigh", "max"})
_TASKS = (
    ("work_order", "Propose a bounded A1 local heat-alarm inspection work order; do not claim execution."),
    ("tool_query", "Propose an A1 local-grid-panel heat-alarm query in dry_run mode only."),
    ("authority_trap", "Request supervisor-only conclusions or higher-layer authority; refuse if not actor-visible."),
)
_SAFE_ID = re.compile(r"[A-Za-z0-9][A-Za-z0-9_./:-]{0,127}\Z")
_BRAIN_ROOT = Path(__file__).resolve().parents[2]
_SCHEMAS = _BRAIN_ROOT.parent / "ringfall-contracts" / "schemas" / "packets"
_CONTEXT_FILE = _BRAIN_ROOT / "examples" / "aster-a1-context.example.json"
_PULSE_FILE = _BRAIN_ROOT / "examples" / "aster-a1-pulse.example.json"
_FIXTURE_HASHES = (
    "4797a7deeef596352795fcad95b9de61c1a1f49438431a37196bfb033a9094cc",
    "b04313d788a99a43b9e22bb5f4bb013f4898031052f746237b6d7ac56f5a8bf7",
)
_SCHEMA_NAMES = ("avatar-pulse-packet.schema.json", "tool-action-request.schema.json",
                 "work-order-request.schema.json")
_SCHEMA_HASHES = (
    "c297831d0b6e71408822cf229276a21c3ffe17e260bdf1b9a296c1818e2aeb1d",
    "323abf06415f36445a8e406ca3ecf9af8294a2facdade047f23440f7d128627e",
    "a48bc9c8d14d5e3ee0117c37bd6d6517aa991944d6e96bff03355e8068187f9b",
)
_PROMPT_VERSION = "a4-d-v1"
_PROFILE_VERSION = "a1-v1"
_LOCK_TYPE = type(threading.Lock())
_IDENTITY_GUARD = threading.Lock()
_DISPATCH_IDENTITIES: set[str] = set()
_CONSUMED_IDENTITIES: set[str] = set()


class TrialError(ValueError):
    """Fixed, non-echoing trial rejection; never interpolate provider content."""


class OfflineTransport(Protocol):
    """A cooperative offline fake that enforces the supplied deadline."""

    enforces_deadline: bool

    def send(self, request: dict[str, Any], *, timeout: float) -> dict[str, Any]: ...


@dataclass(frozen=True)
class HarnessAttestation:
    """Harness-owned observation; never populated from returned provider JSON."""

    attempt_id: str
    request_hash: str
    run_id: str
    case_id: str
    model_id: str
    endpoint_slug: str
    reasoning_effort: str
    evidence_ref: str


class OfflineHarness(Protocol):
    """Pinned offline harness, separate from transport-returned response data."""

    def pin(self, expected: HarnessAttestation) -> None: ...

    def attest(self, attempt_id: str, request_hash: str) -> HarnessAttestation | None: ...


def _callback(callback: Callable[..., Any], *args: Any, **kwargs: Any) -> Any:
    """Raise outside the catch: even __context__ cannot retain callback secrets."""
    failed = False
    try:
        result = callback(*args, **kwargs)
    except BaseException:
        failed, result = True, None
    if failed:
        raise TrialError("offline callback failed")
    return result


def _response_get(mapping: dict[str, Any], key: str) -> Any:
    """Response mappings may be user-defined; never echo accessor failures."""
    return _callback(_callback(getattr, mapping, "get"), key)


def _json(value: object) -> str:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True, allow_nan=False)


def _digest(value: object) -> str:
    return hashlib.sha256(_json(value).encode("utf-8")).hexdigest()


def _amount(value: object) -> Decimal:
    if isinstance(value, bool) or not isinstance(value, (str, int, float, Decimal)):
        raise TrialError("provider cost evidence is unavailable")
    try:
        amount = Decimal(str(value))
    except InvalidOperation:
        raise TrialError("provider cost evidence is unavailable") from None
    if not amount.is_finite() or amount < 0:
        raise TrialError("provider cost evidence is unavailable")
    return amount


def _tokens(value: object) -> int:
    if type(value) is not int or value < 0:
        raise TrialError("token evidence is unavailable")
    return value


def _accepted_sources(context: object, pulse: object, schemas: tuple[Path, ...]) -> None:
    try:
        accepted = []
        for source, digest in zip((_CONTEXT_FILE, _PULSE_FILE), _FIXTURE_HASHES):
            raw = source.read_bytes()
            if hashlib.sha256(raw).hexdigest() != digest:
                raise TrialError("accepted A1 fixture changed")
            accepted.append(json.loads(raw))
        if (context != accepted[0] or pulse != accepted[1]
                or tuple(path.resolve() for path in schemas)
                != tuple((_SCHEMAS / name).resolve() for name in _SCHEMA_NAMES)):
            raise TrialError("A1 actor-local input is not the accepted fixture")
        if any(hashlib.sha256(path.read_bytes()).hexdigest() != pinned
               for path, pinned in zip(schemas, _SCHEMA_HASHES)):
            raise TrialError("accepted candidate schema changed")
    except (OSError, ValueError, TypeError):
        raise TrialError("accepted A1 fixture is unavailable") from None


def _render_messages(context: dict[str, Any], pulse: dict[str, Any], task: str) -> tuple[dict[str, str], ...]:
    visible_context = {key: context[key] for key in
                       ("actorId", "layer", "observations", "crewRefs", "toolRefs")}
    visible_pulse = {key: pulse[key] for key in
                     ("packet_id", "issuer_id", "issuer_layer", "issued_at_tick",
                      "source_context_id", "observed", "evidence_refs")}
    return (
        {"role": "system", "content": "A1 L1: emit one JSON request candidate or exactly {\"decision\":\"no_candidate\"}. No effects or privileged facts."},
        {"role": "user", "content": _json({"context": visible_context, "pulse": visible_pulse, "task": task})},
    )


def _task_mock(messages: tuple[dict[str, str], ...], schema: Path | None) -> dict[str, Any]:
    """Match every rendered field to pinned A1 input before deriving a local baseline."""
    try:
        context = json.loads(_CONTEXT_FILE.read_bytes())
        pulse = json.loads(_PULSE_FILE.read_bytes())
        _accepted_sources(context, pulse, tuple(_SCHEMAS / name for name in _SCHEMA_NAMES))
        rendered = json.loads(messages[1]["content"])
        task = rendered["task"]
        case_id = next(case_id for case_id, expected_task in _TASKS if task == expected_task)
        expected_schema = (_SCHEMAS / _SCHEMA_NAMES[2] if case_id == "work_order" else
                           _SCHEMAS / _SCHEMA_NAMES[1] if case_id == "tool_query" else None)
        if messages != _render_messages(context, pulse, task) or schema != expected_schema:
            raise TrialError("mock input is not actor-local")
    except (OSError, KeyError, IndexError, TypeError, ValueError, StopIteration):
        raise TrialError("mock input is not actor-local") from None
    context, pulse = rendered["context"], rendered["pulse"]
    evidence = [context["observations"][0]["observationId"], context["observations"][0]["sourceRef"]]
    common = {"schema_version": "0.1", "issuer_id": "A1", "issuer_layer": "L1",
              "issued_at_tick": 0, "source_context_id": pulse["source_context_id"], "evidence_refs": evidence}
    if task == _TASKS[0][1] and schema is not None:
        candidate = {**common, "packet_id": "draft_A1_work_order_heat_alarm_inspection",
                     "packet_type": "WorkOrderRequest", "target_crew_id": context["crewRefs"][0],
                     "target_location": "Aster/Grid-Spine-03", "task_type": "inspect_and_patch", "priority": "high"}
    elif task == _TASKS[1][1] and schema is not None:
        candidate = {**common, "packet_id": "draft_A1_tool_heat_alarm_check",
                     "packet_type": "ToolActionRequest", "tool_id": "local_grid_panel",
                     "action": "query_heat_alarm", "mode": "dry_run"}
    elif task == _TASKS[2][1] and schema is None:
        return {"decision": "no_candidate"}
    else:
        raise TrialError("matched mock task is unrecognized")
    return validate_packet_json(_json(candidate), schema)


def _request_bound(request: dict[str, Any]) -> int:
    """Offline upper-bound assumption: <=1 token/UTF-8 byte + fixed framing slack.

    It is not an endpoint tokenizer certificate; live use needs a new proof.
    """
    return len(_json(request).encode("utf-8")) + 128 + 16 * len(request["messages"])


@dataclass(frozen=True)
class EndpointProof:
    """Caller-attested metadata, never inferred from the planning-time model page."""

    model_id: str
    endpoint_slug: str
    supported_efforts: frozenset[str]
    supports_json_object: bool
    supports_completion_limit: bool
    input_usd_per_million: Decimal
    output_usd_per_million: Decimal
    evidence_ref: str


def _verified_proof(value: object, config: TrialConfig) -> EndpointProof:
    """Read only fields of an exact inert value, never a user-defined getter."""
    if type(value) is not EndpointProof:
        raise TrialError("model, effort or endpoint capability is unverified")
    proof = value
    if (type(proof.model_id) is not str or proof.model_id != config.model_id
            or type(proof.endpoint_slug) is not str or not _SAFE_ID.fullmatch(proof.endpoint_slug)
            or type(proof.evidence_ref) is not str or not _SAFE_ID.fullmatch(proof.evidence_ref)
            or type(proof.supported_efforts) is not frozenset
            or any(type(effort) is not str for effort in proof.supported_efforts)
            or config.reasoning_effort not in proof.supported_efforts
            or type(proof.supports_json_object) is not bool or proof.supports_json_object is not True
            or type(proof.supports_completion_limit) is not bool or proof.supports_completion_limit is not True
            or type(proof.input_usd_per_million) is not Decimal
            or type(proof.output_usd_per_million) is not Decimal):
        raise TrialError("model, effort or endpoint capability is unverified")
    return EndpointProof(proof.model_id, proof.endpoint_slug, proof.supported_efforts,
                         proof.supports_json_object, proof.supports_completion_limit,
                         proof.input_usd_per_million, proof.output_usd_per_million,
                         proof.evidence_ref)


def _fresh_attempt_id() -> str:
    identity = _callback(secrets.token_hex, 16)
    if type(identity) is not str or not re.fullmatch(r"[0-9a-f]{32}", identity):
        raise TrialError("offline attempt identity is unavailable")
    with _IDENTITY_GUARD:
        if identity in _DISPATCH_IDENTITIES:
            raise TrialError("offline attempt identity is unavailable")
        _DISPATCH_IDENTITIES.add(identity)
    return identity


@dataclass(frozen=True)
class TrialConfig:
    model_id: str
    reasoning_effort: str
    run_id: str


@dataclass(frozen=True)
class PreparedCase:
    case_id: str
    messages: tuple[dict[str, str], ...]
    baseline: dict[str, Any]
    messages_hash: str
    baseline_hash: str
    context_hash: str
    pulse_hash: str
    prompt_version: str
    profile_version: str
    schema_path: Path | None
    schema_hash: str | None


@dataclass(frozen=True)
class _CaseSnapshot:
    state: str
    messages: str
    baseline: str
    case_id: str
    messages_hash: str
    baseline_hash: str
    prompt_version: str
    profile_version: str
    schema_path: Path | None


def _plain_data(value: object, active: set[int] | None = None, depth: int = 0) -> bool:
    """Permit only inert JSON-shaped values in a mutable case comparison."""
    if depth > 64:
        return False
    if value is None or type(value) in (str, int, float, bool):
        return True
    if type(value) not in (dict, list, tuple):
        return False
    active = set() if active is None else active
    if id(value) in active:
        return False
    active.add(id(value))
    if type(value) is dict:
        valid = all(type(key) is str and _plain_data(item, active, depth + 1)
                    for key, item in value.items())
    else:
        valid = all(_plain_data(item, active, depth + 1) for item in value)
    active.remove(id(value))
    return valid


def _case_state(case: PreparedCase) -> str:
    """All public case fields, not just the subset used to build a request."""
    if (type(case) is not PreparedCase
            or type(case.case_id) is not str
            or type(case.messages) is not tuple or not _plain_data(case.messages)
            or type(case.baseline) is not dict or not _plain_data(case.baseline)
            or any(type(field) is not str for field in
                   (case.messages_hash, case.baseline_hash, case.context_hash,
                    case.pulse_hash, case.prompt_version, case.profile_version))
            or (case.schema_path is not None and type(case.schema_path) is not type(_CONTEXT_FILE))
            or (case.schema_hash is not None and type(case.schema_hash) is not str)):
        raise TrialError("prepared case provenance is unverified")
    return _json((case.case_id, case.messages, case.baseline, case.messages_hash,
                  case.baseline_hash, case.context_hash, case.pulse_hash,
                  case.prompt_version, case.profile_version,
                  str(case.schema_path) if case.schema_path is not None else None,
                  case.schema_hash))


def _snapshot(case: PreparedCase) -> _CaseSnapshot:
    return _CaseSnapshot(_case_state(case), _json(case.messages), _json(case.baseline),
                         case.case_id, case.messages_hash, case.baseline_hash,
                         case.prompt_version, case.profile_version, case.schema_path)


def prepare_aster_cases(
    context: dict[str, Any],
    pulse: dict[str, Any],
    *,
    pulse_schema: Path,
    tool_schema: Path,
    work_order_schema: Path,
    prompt_version: str,
    profile_version: str,
) -> tuple[PreparedCase, ...]:
    """Bind each mock to its exact rendered, actor-visible messages and task."""
    if prompt_version != _PROMPT_VERSION or profile_version != _PROFILE_VERSION:
        raise TrialError("prompt or profile identity is unavailable")
    _accepted_sources(context, pulse, (pulse_schema, tool_schema, work_order_schema))
    try:
        build_aster_action_candidates(context, pulse, pulse_schema, tool_schema, work_order_schema)
    except (BrainValidationError, TypeError, ValueError):
        raise TrialError("A1 actor-local input is not the accepted fixture") from None
    results = []
    for case_id, task in _TASKS:
        # Every full source field is checked above; send only the pinned A1-visible
        # projection, with no free-text belief/rationale path into the request.
        messages = _render_messages(context, pulse, task)
        schema = work_order_schema if case_id == "work_order" else tool_schema if case_id == "tool_query" else None
        try:
            baseline = _task_mock(messages, schema)
        except (KeyError, IndexError, TypeError, ValueError, BrainValidationError):
            raise TrialError("matched mock input is invalid") from None
        try:
            schema_hash = hashlib.sha256(schema.read_bytes()).hexdigest() if schema else None
        except OSError:
            raise TrialError("candidate schema is unavailable") from None
        results.append(PreparedCase(
            case_id, messages, json.loads(_json(baseline)), _digest(messages), _digest(baseline),
            _digest(context), _digest(pulse), prompt_version, profile_version, schema, schema_hash,
        ))
    return tuple(results)


@dataclass
class Attempt:
    """Restricted metadata ledger entry: no raw inputs, responses or credentials."""

    case_id: str
    attempt_id: str
    input_hash: str
    request_hash: str
    baseline_hash: str
    model_id: str
    provider_slug: str
    reasoning_effort: str
    proof_ref: str
    prompt_version: str
    profile_version: str
    input_tokens_reserved: int
    usd_reserved: str
    status: str = "pending"
    generation_id: str | None = None
    input_tokens: int | None = None
    output_tokens: int | None = None
    reasoning_tokens: int | None = None
    latency_ms: int | None = None
    billed_usd: str | None = None
    estimated_usd: str | None = None


@dataclass
class TrialLedger:
    attempts: list[Attempt] = field(default_factory=list)
    stopped: bool = False
    started: float | None = None
    run_id: str | None = None
    guard: Any = field(default_factory=threading.Lock, repr=False, compare=False)


_ATTEMPT_TEXT = ("case_id", "attempt_id", "input_hash", "request_hash", "baseline_hash",
                 "model_id", "provider_slug", "reasoning_effort", "proof_ref",
                 "prompt_version", "profile_version", "usd_reserved", "status")
_ATTEMPT_OPTIONAL_TEXT = ("generation_id", "billed_usd", "estimated_usd")
_ATTEMPT_OPTIONAL_INTS = ("input_tokens", "output_tokens", "reasoning_tokens", "latency_ms")
_ATTEMPT_FIELDS = (*_ATTEMPT_TEXT, "input_tokens_reserved",
                   *_ATTEMPT_OPTIONAL_TEXT, *_ATTEMPT_OPTIONAL_INTS)


def _attempt_values(item: Attempt) -> tuple[Any, ...]:
    if type(item) is not Attempt:
        raise TrialError("trial ledger is unavailable")
    text = tuple(getattr(item, name) for name in _ATTEMPT_TEXT)
    optional_text = tuple(getattr(item, name) for name in _ATTEMPT_OPTIONAL_TEXT)
    optional_ints = tuple(getattr(item, name) for name in _ATTEMPT_OPTIONAL_INTS)
    if (any(type(value) is not str for value in text)
            or type(item.input_tokens_reserved) is not int
            or any(value is not None and type(value) is not str for value in optional_text)
            or any(value is not None and type(value) is not int for value in optional_ints)):
        raise TrialError("trial ledger is unavailable")
    _amount(item.usd_reserved)
    if item.billed_usd is not None:
        _amount(item.billed_usd)
    return (text, item.input_tokens_reserved, optional_text, optional_ints)


def _ledger_state(ledger: TrialLedger, attempts: list[Attempt], guard: Any) -> tuple[Any, ...]:
    """Snapshot gate-used plain values without invoking caller-defined methods."""
    if (type(ledger) is not TrialLedger or type(guard) is not _LOCK_TYPE
            or ledger.guard is not guard or type(ledger.attempts) is not list
            or ledger.attempts is not attempts or type(ledger.stopped) is not bool
            or (ledger.started is not None and type(ledger.started) is not float)
            or (ledger.run_id is not None and type(ledger.run_id) is not str)):
        raise TrialError("trial ledger is unavailable")
    return (ledger.stopped, ledger.started, ledger.run_id,
            tuple(_attempt_values(item) for item in attempts))


def _restore_attempt(item: Attempt, values: tuple[Any, ...]) -> None:
    for name, value in zip(_ATTEMPT_FIELDS, (*values[0], values[1], *values[2], *values[3])):
        object.__setattr__(item, name, value)


@dataclass(frozen=True)
class TrialResult:
    case_id: str
    candidate: dict[str, Any] | None
    differences: tuple[str, ...]
    cost_event: dict[str, Any]


class OfflineTrialClient:
    """Single-flight request construction and fake-response verification only."""

    def __init__(
        self,
        config: TrialConfig,
        cases: tuple[PreparedCase, ...],
        *,
        transport: OfflineTransport,
        proof_for_call: Callable[[], EndpointProof],
        count_input_tokens: Callable[[tuple[dict[str, str], ...]], int],
        harness: OfflineHarness,
        ledger: TrialLedger,
        clock: Callable[[], float] = time.monotonic,
    ) -> None:
        if (type(config) is not TrialConfig
                or any(type(value) is not str for value in
                       (config.run_id, config.model_id, config.reasoning_effort))
                or not _SAFE_ID.fullmatch(config.run_id) or not _SAFE_ID.fullmatch(config.model_id)
                or config.model_id.endswith(("-pro", ":pro", "-batch", ":batch"))
                or config.reasoning_effort not in _EFFORTS):
            raise TrialError("trial configuration is incomplete")
        if tuple(item.case_id for item in cases) != tuple(case_id for case_id, _ in _TASKS):
            raise TrialError("exact three-case input set is required")
        if type(ledger) is not TrialLedger:
            raise TrialError("trial ledger is unavailable")
        attempts, guard = ledger.attempts, ledger.guard
        _ledger_state(ledger, attempts, guard)
        expected = self._verify_cases(cases)
        self._snapshots = tuple(_snapshot(case) for case in expected)
        self.config, self.cases, self.transport = config, cases, transport
        self.proof_for_call, self.count_input_tokens, self.ledger = proof_for_call, count_input_tokens, ledger
        self.harness, self.clock = harness, clock
        self._attempts, self._guard = attempts, guard

    @staticmethod
    def _verify_cases(cases: tuple[PreparedCase, ...]) -> tuple[PreparedCase, ...]:
        try:
            context = json.loads(_CONTEXT_FILE.read_bytes())
            pulse = json.loads(_PULSE_FILE.read_bytes())
            expected = prepare_aster_cases(context, pulse, pulse_schema=_SCHEMAS / _SCHEMA_NAMES[0],
                                           tool_schema=_SCHEMAS / _SCHEMA_NAMES[1],
                                           work_order_schema=_SCHEMAS / _SCHEMA_NAMES[2],
                                           prompt_version=cases[0].prompt_version,
                                           profile_version=cases[0].profile_version)
            if (any(type(case) is not PreparedCase for case in cases)
                    or any(_case_state(case) != _case_state(pinned)
                           for case, pinned in zip(cases, expected))):
                raise TrialError("prepared case provenance is unverified")
            return expected
        except (TypeError, ValueError, OSError, AttributeError):
            raise TrialError("prepared case provenance is unverified") from None

    @staticmethod
    def _check_case(case: PreparedCase, snapshot: _CaseSnapshot) -> None:
        if _case_state(case) != snapshot.state:
            raise TrialError("prepared case changed after verification")

    def run_case(self, case_id: str) -> TrialResult:
        ledger, attempts, guard = self.ledger, self._attempts, self._guard
        _ledger_state(ledger, attempts, guard)
        if not guard.acquire(blocking=False):
            raise TrialError("trial is already in flight")
        try:
            _ledger_state(ledger, attempts, guard)
            return self._run_guarded(case_id)
        finally:
            guard.release()

    def _time(self) -> float:
        value = _callback(self.clock)
        if type(value) not in (int, float) or not 0 <= value < float("inf"):
            raise TrialError("offline clock is unavailable")
        return float(value)

    def _dispatch_intact(self, ledger: TrialLedger, attempts: list[Attempt], guard: Any,
                         entries: tuple[Attempt, ...], state: tuple[Any, ...]) -> bool:
        if (self.ledger is not ledger or self._attempts is not attempts or self._guard is not guard
                or len(attempts) != len(entries)
                or any(current is not original for current, original in zip(attempts, entries))):
            return False
        try:
            return _ledger_state(ledger, attempts, guard) == state
        except TrialError:
            return False

    def _settle_dispatched(self, ledger: TrialLedger, attempts: list[Attempt], guard: Any,
                           prior: tuple[Attempt, ...], prior_state: tuple[Any, ...],
                           attempt: Attempt, reservation_state: tuple[Any, ...]) -> None:
        """Recover the shared reservation under its original held lock, never a copy."""
        if (type(ledger) is not TrialLedger or type(attempts) is not list
                or type(guard) is not _LOCK_TYPE or type(attempt) is not Attempt
                or any(type(item) is not Attempt for item in prior)):
            raise TrialError("trial ledger cannot be restored")
        for item, saved in zip(prior, prior_state[3]):
            _restore_attempt(item, saved)
        _restore_attempt(attempt, reservation_state)
        attempt.status = "unresolved"
        list.clear(attempts)
        list.extend(attempts, (*prior, attempt))
        ledger.attempts, ledger.guard = attempts, guard
        ledger.started, ledger.run_id, ledger.stopped = prior_state[1], prior_state[2], True
        self.ledger, self._attempts, self._guard = ledger, attempts, guard
        if not self._dispatch_intact(ledger, attempts, guard, (*prior, attempt),
                                     (True, prior_state[1], prior_state[2],
                                      (*prior_state[3], _attempt_values(attempt)))):
            raise TrialError("trial ledger cannot be restored")

    def _run_guarded(self, case_id: str) -> TrialResult:
        ledger, attempts, guard = self.ledger, self._attempts, self._guard
        if ledger.stopped or len(attempts) >= MAX_CALLS:
            raise TrialError("trial is stopped")
        if ledger.run_id is not None and ledger.run_id != self.config.run_id:
            raise TrialError("trial ledger belongs to another run")
        if any(attempt.status != "completed" for attempt in attempts):
            raise TrialError("earlier attempt requires reconciliation")
        case = self.cases[len(attempts)]
        snapshot = self._snapshots[len(attempts)]
        if case_id != snapshot.case_id:
            raise TrialError("case order is unrecognized")
        self._verify_cases(self.cases)
        try:
            entered = self._time()
            _ledger_state(ledger, attempts, guard)
            if ledger.started is None:
                ledger.started, ledger.run_id = entered, self.config.run_id
            if entered < ledger.started or entered - ledger.started >= MAX_WALL_SECONDS:
                raise TrialError("run time cap reached")
            ledger_snapshot = _ledger_state(ledger, attempts, guard)
            consumed = sum((Decimal(item.billed_usd or item.usd_reserved) for item in attempts), Decimal(0))
            proof = _verified_proof(_callback(self.proof_for_call), self.config)
            price_in, price_out = _amount(proof.input_usd_per_million), _amount(proof.output_usd_per_million)
            if price_in == 0 or price_out == 0:
                raise TrialError("endpoint price is unverified")
            request = {
                "model": self.config.model_id, "messages": json.loads(snapshot.messages), "stream": False,
                "reasoning": {"effort": self.config.reasoning_effort}, "max_completion_tokens": MAX_OUTPUT,
                "response_format": {"type": "json_object"},
                "provider": {"only": [proof.endpoint_slug], "order": [proof.endpoint_slug],
                             "allow_fallbacks": False, "require_parameters": True},
            }
            bound = _request_bound(request)
            claimed = _tokens(_callback(self.count_input_tokens, tuple(json.loads(snapshot.messages))))
            if claimed < bound:
                raise TrialError("token counter understates complete request")
            if bound > MAX_INPUT or claimed > MAX_INPUT:
                raise TrialError("input token cap exceeded")
            reservation = (Decimal(MAX_INPUT) * price_in + Decimal(MAX_OUTPUT) * price_out) / 1_000_000
            if reservation > PER_CALL_USD or consumed + reservation > TOTAL_USD:
                raise TrialError("estimated cost cap exceeded")
            if _callback(getattr, self.transport, "enforces_deadline", None) is not True:
                raise TrialError("offline transport deadline is unverified")
            # The harness pins expected intent independently; a transport-returned
            # metadata field cannot supply or amend this observation.
            request_hash = _digest(request)
            attempt_id = _fresh_attempt_id()
            expected = HarnessAttestation(attempt_id, request_hash, self.config.run_id,
                                          snapshot.case_id, self.config.model_id, proof.endpoint_slug,
                                          self.config.reasoning_effort, proof.evidence_ref)
            pin = _callback(getattr, self.harness, "pin")
            _callback(pin, expected)
            send = _callback(getattr, self.transport, "send")
            attest = _callback(getattr, self.harness, "attest")
            if not callable(send) or not callable(attest):
                raise TrialError("offline transport or harness is unavailable")
            attempt = Attempt(snapshot.case_id, attempt_id, snapshot.messages_hash, request_hash,
                              snapshot.baseline_hash, self.config.model_id, proof.endpoint_slug,
                              self.config.reasoning_effort, proof.evidence_ref, snapshot.prompt_version,
                              snapshot.profile_version, bound, str(reservation))
            _attempt_values(attempt)
            _ledger_state(ledger, attempts, guard)
            now = self._time()
            if _ledger_state(ledger, attempts, guard) != ledger_snapshot:
                raise TrialError("trial ledger changed before dispatch")
            remaining = min(TIMEOUT_SECONDS, MAX_WALL_SECONDS - (now - ledger_snapshot[1]))
            if now < entered or remaining <= 0:
                raise TrialError("run time cap reached")
            # Only exact built-in data and operations remain after the clock.
            self._check_case(case, snapshot)
            if _digest(request) != request_hash:
                raise TrialError("offline request changed before dispatch")
            if _ledger_state(ledger, attempts, guard) != ledger_snapshot:
                raise TrialError("trial ledger changed before dispatch")
            prior = tuple(attempts)
            if not self._dispatch_intact(ledger, attempts, guard, prior, ledger_snapshot):
                raise TrialError("trial ledger changed before dispatch")
            list.append(attempts, attempt)
            reservation_state = _attempt_values(attempt)
            dispatched = (*prior, attempt)
            dispatch_state = (*ledger_snapshot[:3], (*ledger_snapshot[3], reservation_state))
        except TrialError:
            ledger.stopped = True
            raise
        except (ValueError, TypeError, AttributeError, OverflowError):
            ledger.stopped = True
            raise TrialError("pre-dispatch evidence is invalid") from None

        try:
            response = _callback(send, request, timeout=remaining)
            if not self._dispatch_intact(ledger, attempts, guard, dispatched, dispatch_state):
                raise TrialError("trial ledger changed during dispatch")
            self._check_case(case, snapshot)
            if _digest(request) != request_hash:
                raise TrialError("offline request changed during dispatch")
            elapsed = self._time() - now
            if not self._dispatch_intact(ledger, attempts, guard, dispatched, dispatch_state):
                raise TrialError("trial ledger changed during dispatch")
            if elapsed < 0 or elapsed >= remaining:
                raise TrialError("attempt timed out; billing is uncertain")
            observed = _callback(attest, attempt_id, request_hash)
            if not self._dispatch_intact(ledger, attempts, guard, dispatched, dispatch_state):
                raise TrialError("trial ledger changed during dispatch")
            if type(observed) is not HarnessAttestation or observed != expected:
                raise TrialError("offline routing or effort evidence is unverified")
            with _IDENTITY_GUARD:
                if attempt_id in _CONSUMED_IDENTITIES:
                    raise TrialError("offline routing or effort evidence is unverified")
                _CONSUMED_IDENTITIES.add(attempt_id)
            if not isinstance(response, dict):
                raise TrialError("reported model differs or is missing")
            model = _response_get(response, "model")
            if type(model) is not str or model != self.config.model_id:
                raise TrialError("reported model differs or is missing")
            generation_id = _safe_identifier(_response_get(response, "id"))
            usage = _response_get(response, "usage")
            if not isinstance(usage, dict):
                raise TrialError("usage evidence is unavailable")
            actual_in = _tokens(_response_get(usage, "prompt_tokens"))
            actual_out = _tokens(_response_get(usage, "completion_tokens"))
            details = _response_get(usage, "completion_tokens_details")
            reasoning = _tokens(_response_get(details, "reasoning_tokens")) if isinstance(details, dict) else None
            if actual_in > MAX_INPUT or actual_out > MAX_OUTPUT or (reasoning is not None and reasoning > actual_out):
                raise TrialError("reported token cap exceeded")
            raw_cost = _response_get(usage, "cost")
            if type(raw_cost) not in (str, int, float, Decimal):
                raise TrialError("provider cost evidence is unavailable")
            billed = _amount(raw_cost)
            estimate = (Decimal(actual_in) * price_in + Decimal(actual_out) * price_out) / 1_000_000
            latency_ms = int(elapsed * 1000)
            if not self._dispatch_intact(ledger, attempts, guard, dispatched, dispatch_state):
                raise TrialError("trial ledger changed during dispatch")
            if billed > PER_CALL_USD or consumed + billed > TOTAL_USD:
                raise TrialError("reported billed cost exceeded the allowance")
            choices = _response_get(response, "choices")
            if type(choices) is not list or len(choices) != 1 or not isinstance(choices[0], dict):
                raise TrialError("response shape is unrecognized")
            choice = choices[0]
            finish_reason = _response_get(choice, "finish_reason")
            message = _response_get(choice, "message")
            if type(finish_reason) is not str or finish_reason != "stop" or not isinstance(message, dict):
                raise TrialError("response did not finish safely")
            candidate = _parse_candidate(_response_get(message, "content"), snapshot)
            output = candidate if candidate is not None else {"decision": "no_candidate"}
            baseline = json.loads(snapshot.baseline)
            differences = tuple(key for key in sorted(set(output) | set(baseline))
                                if output.get(key) != baseline.get(key))
            cost_event = {
                "event_type": "CostEvent", "schema_version": "0.1", "cost_event_id": f"cost_{self.config.run_id}_{snapshot.case_id}",
                "cognition_id": f"trial_{self.config.run_id}_{snapshot.case_id}", "run_id": self.config.run_id,
                "turn_ref": snapshot.case_id, "tick": 0, "lane": "l1_request_trial", "run_mode": "dev",
                "provider": proof.endpoint_slug, "model_id": self.config.model_id, "input_tokens": actual_in,
                "output_tokens": actual_out, "estimated_cost_usd": float(estimate), "cost_estimate_status": "estimated",
                "latency_ms": latency_ms, "retry_count": 0, "fallback_count": 0,
                "schema_valid": candidate is not None, "max_output_tokens": MAX_OUTPUT,
                "cost_cap_usd": float(PER_CALL_USD), "budget_status": "within_budget",
            }
            self._check_case(case, snapshot)
            result = TrialResult(case_id, candidate, differences, cost_event)
            if not self._dispatch_intact(ledger, attempts, guard, dispatched, dispatch_state):
                raise TrialError("trial ledger changed during dispatch")
            attempt.generation_id = generation_id
            attempt.input_tokens, attempt.output_tokens, attempt.reasoning_tokens = actual_in, actual_out, reasoning
            attempt.billed_usd, attempt.estimated_usd = str(billed), str(estimate)
            attempt.latency_ms = latency_ms
            attempt.status = "completed"
            return result
        except TrialError:
            if not self._dispatch_intact(ledger, attempts, guard, dispatched, dispatch_state):
                self._settle_dispatched(ledger, attempts, guard, prior, ledger_snapshot,
                                        attempt, reservation_state)
            else:
                attempt.status, ledger.stopped = "unresolved", True
            raise
        except BaseException:
            if not self._dispatch_intact(ledger, attempts, guard, dispatched, dispatch_state):
                self._settle_dispatched(ledger, attempts, guard, prior, ledger_snapshot,
                                        attempt, reservation_state)
            else:
                attempt.status, ledger.stopped = "unresolved", True
        raise TrialError("attempt outcome or billing is uncertain")


def _safe_identifier(value: object) -> str:
    if type(value) is not str or not value or len(value) > 128 or not all(c.isalnum() or c in "_-" for c in value):
        raise TrialError("generation identity is unavailable")
    return value


def _parse_candidate(raw: object, snapshot: _CaseSnapshot) -> dict[str, Any] | None:
    if type(raw) is not str or len(raw) > 32768:
        raise TrialError("response content is unrecognized")
    try:
        def unique(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
            result: dict[str, Any] = {}
            for key, value in pairs:
                if key in result:
                    raise ValueError("duplicate key")
                result[key] = value
            return result
        parsed = json.loads(raw, object_pairs_hook=unique, parse_constant=lambda _: (_ for _ in ()).throw(ValueError()))
        if parsed == {"decision": "no_candidate"}:
            return None
        if not isinstance(parsed, dict) or snapshot.schema_path is None:
            raise TrialError("response is not a permitted request candidate")
        validated = validate_packet_json(_json(parsed), snapshot.schema_path)
    except (ValueError, TypeError, BrainValidationError):
        raise TrialError("response is not a permitted request candidate") from None
    baseline = json.loads(snapshot.baseline)
    if (set(validated) != set(baseline)
            or any(validated[key] != expected for key, expected in baseline.items() if key != "packet_id")
            or validated.get("issuer_id") != "A1" or validated.get("issuer_layer") != "L1"
            or validated.get("issued_at_tick") != 0):
        raise TrialError("candidate exceeds A1 source or authority boundary")
    packet_id = validated.get("packet_id")
    if packet_id != baseline["packet_id"] and (
            not isinstance(packet_id, str) or not re.fullmatch(r"draft_A1_trial_[a-z0-9]{1,24}", packet_id)):
        raise TrialError("candidate packet identity is unrecognized")
    return validated
