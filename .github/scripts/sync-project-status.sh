#!/usr/bin/env bash
# Moves issues on the project board to the column their "status: …" label names.
#
# The label is the source of truth: people and Claude change an issue's status by changing its label, and this
# script makes the board follow (run by .github/workflows/project-board.yml). An issue is on the board once it has
# a status label. Its column is the board's Status option whose name matches the label after "status: ", ignoring
# case. Dragging a card on the board does not change the label.
#
# Usage: sync-project-status.sh [issue-number]   (no number: every issue that has a status label)
# Env:   GH_TOKEN       classic personal access token with the project scope (the PROJECT_TOKEN secret)
#        REPO           owner/name of the repository
#        PROJECT_OWNER  login of the user who owns the board
#        PROJECT_TITLE  title of the board
#
# The GraphQL queries are in single quotes on purpose: $owner, $project and the rest are GraphQL variables.
# shellcheck disable=SC2016
set -euo pipefail

if [ -z "${GH_TOKEN:-}" ]; then
  echo "::error::The PROJECT_TOKEN secret is not set. Add a classic personal access token with the project scope."
  exit 1
fi

issue_number="${1:-}"

# The board, and its Status field with the options (columns) it offers.
projects=$(gh api graphql -f owner="$PROJECT_OWNER" -f query='
  query($owner: String!) {
    user(login: $owner) {
      projectsV2(first: 100) {
        nodes {
          id
          title
          field(name: "Status") {
            ... on ProjectV2SingleSelectField { id options { id name } }
          }
        }
      }
    }
  }')
board=$(jq --arg title "$PROJECT_TITLE" '[.data.user.projectsV2.nodes[] | select(.title == $title)]' <<<"$projects")
if [ "$(jq 'length' <<<"$board")" -ne 1 ]; then
  titles=$(jq -r '[.data.user.projectsV2.nodes[].title | "\"" + . + "\""] | join(", ") | if . == "" then "none" else . end' <<<"$projects")
  echo "::error::Expected exactly one project titled \"$PROJECT_TITLE\" owned by $PROJECT_OWNER, found $(jq 'length' <<<"$board"). Their projects: $titles."
  exit 1
fi
project_id=$(jq -r '.[0].id' <<<"$board")
field_id=$(jq -r '.[0].field.id // empty' <<<"$board")
if [ -z "$field_id" ]; then
  echo "::error::Project \"$PROJECT_TITLE\" has no single-select field named Status."
  exit 1
fi
options=$(jq -c '.[0].field.options' <<<"$board")

# The issues to sync, as [{number, id, labels}]. Pull requests share the issues API and are left out.
if [ -n "$issue_number" ]; then
  issues=$(gh api "repos/$REPO/issues/$issue_number" | jq '[{number, id: .node_id, labels: [.labels[].name]}]')
else
  issues=$(gh api --paginate "repos/$REPO/issues?state=all&per_page=100" \
    | jq -s 'add | map(select(.pull_request | not) | {number, id: .node_id, labels: [.labels[].name]})')
fi

failed=0
while read -r issue; do
  number=$(jq -r '.number' <<<"$issue")
  statuses=$(jq -c '[.labels[] | select(startswith("status: ")) | ltrimstr("status: ")]' <<<"$issue")

  case "$(jq 'length' <<<"$statuses")" in
    0)
      echo "#$number has no status label, so it is not on the board."
      continue
      ;;
    1) ;;
    *)
      echo "::error::#$number has more than one status label: $(jq -r 'join(", ")' <<<"$statuses"). Keep one."
      failed=1
      continue
      ;;
  esac

  status=$(jq -r '.[0]' <<<"$statuses")
  option_id=$(jq -r --arg status "$status" \
    'map(select((.name | ascii_downcase) == ($status | ascii_downcase)) | .id) | first // empty' <<<"$options")
  if [ -z "$option_id" ]; then
    echo "::error::#$number: the board has no Status column named \"$status\". Its columns: $(jq -r 'map(.name) | join(", ")' <<<"$options")."
    failed=1
    continue
  fi

  # Adding an issue that is already on the board returns its existing card.
  item_id=$(gh api graphql -f project="$project_id" -f content="$(jq -r '.id' <<<"$issue")" -f query='
    mutation($project: ID!, $content: ID!) {
      addProjectV2ItemById(input: { projectId: $project, contentId: $content }) { item { id } }
    }' | jq -r '.data.addProjectV2ItemById.item.id')

  gh api graphql -f project="$project_id" -f item="$item_id" -f field="$field_id" -f option="$option_id" -f query='
    mutation($project: ID!, $item: ID!, $field: ID!, $option: String!) {
      updateProjectV2ItemFieldValue(input: {
        projectId: $project, itemId: $item, fieldId: $field, value: { singleSelectOptionId: $option }
      }) { projectV2Item { id } }
    }' > /dev/null

  echo "#$number → $status"
done < <(jq -c '.[]' <<<"$issues")

exit "$failed"
