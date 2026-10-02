#!/usr/bin/env bash
# Release PRs (develop → main) must still run apicompat, npm-lockfile, and
# breaking-intent. GitHub skips every push and pull_request workflow when the
# HEAD commit contains a skip directive, before any job `if:` runs, and leaves
# required checks pending. The release commit is usually that HEAD.
#
# The fix is the release message in .releaserc.json (subject only: no
# ${nextRelease.notes}, no skip directive; an omitted message makes
# @semantic-release/git append "[skip ci]") plus a push-only job `if:`.
# Notes stay in CHANGELOG.md. Interpolating them would copy a skip directive
# from a release-visible commit into the release HEAD. The guard keys off
# the commit message and the git committer name. github.actor is the
# RELEASE_TOKEN owner, not
# semantic-release-bot, so an actor filter either misses the push or also
# skips a maintainer's ordinary pushes.
#
# A job skipped by `if:` reports success. A workflow skipped by a directive
# stays pending.
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)

command -v python3 >/dev/null || { echo "python3 is required" >&2; exit 1; }
command -v jq >/dev/null || { echo "jq is required" >&2; exit 1; }

python3 - "$root" <<'PY'
import json, re, sys
from pathlib import Path

root = Path(sys.argv[1])
failures = []

def fail(message):
    failures.append(message)
    print(f"FAIL: {message}", file=sys.stderr)

# --- release commit message -------------------------------------------------

message = json.loads((root / ".releaserc.json").read_text())
git_message = None
for plugin in message["plugins"]:
    if isinstance(plugin, list) and plugin and plugin[0] == "@semantic-release/git":
        git_message = plugin[1].get("message")
        break

expected_message = "chore(release): ${nextRelease.version}"
if git_message != expected_message:
    fail(
        "git plugin message must be the release subject only, with no notes "
        f"and no skip directive.\n  expected: {expected_message!r}\n  actual:   {git_message!r}"
    )

# Notes are written to CHANGELOG.md by @semantic-release/changelog. Putting
# ${nextRelease.notes} in the git message copies commit subjects into the
# release HEAD. GitHub matches skip directives anywhere in that message, so a
# release-visible subject that contains one would skip the next develop→main
# pull_request workflows again.
if git_message and "${nextRelease.notes}" in git_message:
    fail("release commit template interpolates ${nextRelease.notes}")
if git_message and "nextRelease.notes" in git_message:
    fail("release commit template references nextRelease.notes")

skip_directives = (
    "[skip ci]",
    "[ci skip]",
    "[no ci]",
    "[skip actions]",
    "[actions skip]",
    "skip-checks:",
)
if git_message:
    lowered = git_message.lower()
    for directive in skip_directives:
        if directive in lowered:
            fail(f"release commit template contains skip directive {directive!r}")

# --- workflow job conditions ------------------------------------------------

# Normalized form of the push guard. True only for a push whose HEAD commit
# exists, starts with the release subject, and was committed by the bot.
release_push = (
    "github.event_name == 'push' && "
    "github.event.head_commit && "
    "startsWith(github.event.head_commit.message, 'chore(release):') && "
    "github.event.head_commit.committer.name == 'semantic-release-bot'"
)
build_if = f"github.event_name != 'workflow_run' && !({release_push})"
release_job_if = f"!({release_push})"

expected_ifs = {
    root / ".github/workflows/build.yml": {
        "build": build_if,
        "npm-lockfile": build_if,
        "breaking-intent": build_if,
        "apicompat": "github.event_name == 'pull_request'",
        "relay-release-status": "github.event_name == 'workflow_run'",
    },
    root / ".github/workflows/semantic-release.yml": {
        "release": release_job_if,
    },
}

def extract_job_ifs(text):
    lines = text.splitlines()
    start = next((i for i, line in enumerate(lines) if line == "jobs:"), None)
    if start is None:
        return None
    jobs = {}
    current = None
    i = start + 1
    while i < len(lines):
        line = lines[i]
        job = re.match(r"^  ([A-Za-z0-9_-]+):\s*$", line)
        if job:
            current = job.group(1)
            i += 1
            continue
        cond = re.match(r"^    if:\s*(.*)$", line)
        if current and cond:
            rest = cond.group(1).strip()
            if rest in (">-", ">", "|-", "|"):
                block = []
                i += 1
                while i < len(lines) and (lines[i].startswith("      ") or lines[i].strip() == ""):
                    stripped = lines[i].strip()
                    if stripped:
                        block.append(stripped)
                    elif rest.startswith("|"):
                        block.append("")
                    i += 1
                if rest.startswith(">"):
                    value = " ".join(block)
                else:
                    value = "\n".join(block).strip()
            else:
                value = rest
                i += 1
            value = value.replace("${{", "").replace("}}", "").strip()
            value = re.sub(r"\s+", " ", value)
            jobs[current] = value
            continue
        i += 1
    return jobs

for path, expected in expected_ifs.items():
    found = extract_job_ifs(path.read_text())
    if found is None:
        fail(f"{path} has no jobs: block")
        continue
    for job, want in expected.items():
        got = found.get(job)
        if got is None:
            fail(f"{path.name} job {job} has no if:")
            continue
        if got != want:
            fail(
                f"{path.name} job {job} if drifted.\n  expected: {want}\n  actual:   {got}"
            )
        if "github.actor" in got:
            fail(f"{path.name} job {job} filters on github.actor")

# --- same predicate the expressions encode ----------------------------------

def is_release_push(event, has_head, message, committer):
    return (
        event == "push"
        and has_head
        and message.startswith("chore(release):")
        and committer == "semantic-release-bot"
    )

def build_runs(event, has_head, message, committer):
    if event == "workflow_run":
        return False
    if is_release_push(event, has_head, message, committer):
        return False
    return True

def apicompat_runs(event):
    return event == "pull_request"

def release_runs(event, has_head, message, committer):
    return not is_release_push(event, has_head, message, committer)

notes = "\n\n## 1.2.3\n\n* feat: something"
# Columns are the job `if:` results, not GitHub's pre-workflow skip filter.
# semantic-release.yml is not triggered by pull_request or workflow_run; its
# if still evaluates true there so a future trigger is not how release PRs
# lose the gates. A skip directive in the message is ignored by the if on
# purpose — the template test above is what keeps it out of the commit.
cases = [
    # event, has_head, message, committer, build, apicompat, semantic-release
    ("push", True, "chore(release): 1.2.3" + notes, "semantic-release-bot", False, False, False),
    ("push", True, "chore(release): 1.2.3-beta.4" + notes, "semantic-release-bot", False, False, False),
    ("push", True, "Merge pull request #1 from org/develop", "GitHub", True, False, True),
    ("push", True, "chore(release): manual note", "Jane Doe", True, False, True),
    ("push", True, "feat: add a reader", "Jane Doe", True, False, True),
    ("push", False, "", "", True, False, True),
    ("pull_request", True, "chore(release): 1.2.3" + notes, "semantic-release-bot", True, True, True),
    ("pull_request", True, "chore(release): 1.2.3 [skip ci]" + notes, "semantic-release-bot", True, True, True),
    ("pull_request", True, "feat: add a reader", "Jane Doe", True, True, True),
    ("workflow_run", True, "chore(release): 1.2.3" + notes, "semantic-release-bot", False, False, True),
    ("workflow_dispatch", True, "chore(release): 1.2.3" + notes, "semantic-release-bot", True, False, True),
    ("workflow_dispatch", False, "", "", True, False, True),
]

for event, has_head, message, committer, want_build, want_api, want_release in cases:
    label = f"{event} head={has_head} committer={committer!r} message={message.splitlines()[0] if message else ''!r}"
    got = (
        build_runs(event, has_head, message, committer),
        apicompat_runs(event),
        release_runs(event, has_head, message, committer),
    )
    want = (want_build, want_api, want_release)
    if got != want:
        fail(f"truth table {label}: expected build/apicompat/release={want}, got {got}")

# --- concurrency: a skipped release push must not join the release queue ----
#
# The job `if:` runs after the workflow is queued. One pending run per
# concurrency group means that queue entry would cancel a pending main or
# develop release. Release-commit pushes therefore use their own group.
# cancel-in-progress stays false so an in-progress publish is not aborted.

sr_workflow = (root / ".github/workflows/semantic-release.yml").read_text()
if "cancel-in-progress: false" not in sr_workflow:
    fail("semantic-release.yml must keep cancel-in-progress: false")
if "cancel-in-progress: true" in sr_workflow:
    fail("semantic-release.yml sets cancel-in-progress: true")

def extract_concurrency_group(text):
    lines = text.splitlines()
    for i, line in enumerate(lines):
        if line != "concurrency:":
            continue
        for j in range(i + 1, len(lines)):
            group = re.match(r"^  group:\s*(.*)$", lines[j])
            if not group:
                stripped = lines[j].strip()
                if stripped == "" or stripped.startswith("#"):
                    continue
                return None
            rest = group.group(1).strip()
            if rest in (">-", ">", "|-", "|"):
                block = []
                k = j + 1
                while k < len(lines) and (lines[k].startswith("    ") or lines[k].strip() == ""):
                    stripped = lines[k].strip()
                    if stripped:
                        block.append(stripped)
                    k += 1
                value = " ".join(block) if rest.startswith(">") else "\n".join(block).strip()
            else:
                value = rest
            value = value.replace("${{", "").replace("}}", "").strip()
            return re.sub(r"\s+", " ", value)
    return None

expected_group = (
    "(github.event_name == 'push' && "
    "github.event.head_commit && "
    "startsWith(github.event.head_commit.message, 'chore(release):') && "
    "github.event.head_commit.committer.name == 'semantic-release-bot') && "
    "format('semantic-release-noop-{0}', github.sha) || "
    "(github.ref == 'refs/heads/main' || github.ref == 'refs/heads/develop') && "
    "'semantic-release-channels' || "
    "format('semantic-release-{0}', github.ref)"
)
got_group = extract_concurrency_group(sr_workflow)
if got_group != expected_group:
    fail(
        "semantic-release concurrency group drifted.\n"
        f"  expected: {expected_group}\n  actual:   {got_group}"
    )

def concurrency_group(event, ref, sha, has_head, message, committer):
    if is_release_push(event, has_head, message, committer):
        return f"semantic-release-noop-{sha}"
    if ref in ("refs/heads/main", "refs/heads/develop"):
        return "semantic-release-channels"
    return f"semantic-release-{ref}"

release_sha = "aaa111"
other_sha = "bbb222"
group_cases = [
    ("push", "refs/heads/develop", release_sha, True, "chore(release): 1.2.3" + notes, "semantic-release-bot", f"semantic-release-noop-{release_sha}"),
    ("push", "refs/heads/main", release_sha, True, "chore(release): 1.2.3" + notes, "semantic-release-bot", f"semantic-release-noop-{release_sha}"),
    ("push", "refs/heads/develop", other_sha, True, "Merge pull request #1 from org/develop", "GitHub", "semantic-release-channels"),
    ("push", "refs/heads/main", other_sha, True, "feat: add a reader", "Jane Doe", "semantic-release-channels"),
    ("push", "refs/heads/develop", other_sha, True, "chore(release): manual note", "Jane Doe", "semantic-release-channels"),
    ("push", "refs/heads/feature", other_sha, True, "feat: add a reader", "Jane Doe", "semantic-release-refs/heads/feature"),
    ("workflow_dispatch", "refs/heads/develop", release_sha, True, "chore(release): 1.2.3" + notes, "semantic-release-bot", "semantic-release-channels"),
    ("workflow_dispatch", "refs/heads/topic", release_sha, False, "", "", "semantic-release-refs/heads/topic"),
]
channels = "semantic-release-channels"
for event, ref, sha, has_head, message, committer, want in group_cases:
    got = concurrency_group(event, ref, sha, has_head, message, committer)
    label = f"{event} {ref} committer={committer!r}"
    if got != want:
        fail(f"concurrency {label}: expected {want}, got {got}")
    if is_release_push(event, has_head, message, committer) and got == channels:
        fail(f"concurrency {label}: release-commit push joined {channels}")

contributing = (root / "CONTRIBUTING.md").read_text()
for snippet in (
    "must not contain a skip directive",
    "must not appear in the git commit message",
    "${nextRelease.notes}",
    "develop` → `main",
    "npm-lockfile",
    "breaking-intent",
):
    if snippet not in contributing:
        fail(f"CONTRIBUTING.md no longer documents release-PR gates ({snippet!r})")

if failures:
    print(f"{len(failures)} failure(s)", file=sys.stderr)
    sys.exit(1)

print("release PR gate conditions match the push-only skip.")
PY
