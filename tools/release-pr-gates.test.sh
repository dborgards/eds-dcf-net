#!/usr/bin/env bash
# Release PRs (develop → main) must still run apicompat, npm-lockfile, and
# breaking-intent. GitHub skips every push and pull_request workflow when the
# HEAD commit contains a skip directive, before any job `if:` runs, and leaves
# required checks pending. The release commit is usually that HEAD.
#
# The fix is the release message in .releaserc.json (no skip directive; an
# omitted message makes @semantic-release/git append "[skip ci]") plus a
# push-only job `if:`. The guard keys off the commit message and the git
# committer name. github.actor is the RELEASE_TOKEN owner, not
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

expected_message = "chore(release): ${nextRelease.version}\n\n${nextRelease.notes}"
if git_message != expected_message:
    fail(
        "git plugin message must stay set to the release subject without a "
        f"skip directive.\n  expected: {expected_message!r}\n  actual:   {git_message!r}"
    )

# GitHub matches these anywhere in the commit message, which includes the
# generated notes. The template itself must not introduce one. Subjects that
# already contain a directive cannot merge while `build` is required (the
# pull_request workflow never starts), so they do not reach the notes.
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

contributing = (root / "CONTRIBUTING.md").read_text()
for snippet in (
    "must not contain a skip directive",
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
