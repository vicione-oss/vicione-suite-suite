#!/bin/sh
# Downloads a suite .deb from this project's generic package registry into <output-path>.
#
# With <version> given, exactly that version is used, and the script fails when it carries no amd64
# .deb rather than reaching for an older one. That is the nightly's case: the pipeline builds its own
# package, so the version is known ($CI_PIPELINE_ID), and testing yesterday's build instead of this
# commit's would defeat the point of the run.
#
# Without <version>, the newest version that carries an amd64 .deb is taken. A package version is the
# id of the suite pipeline that produced it, but the .deb inside it is published later by the
# separately triggered deb-packaging pipeline, so the newest version does not necessarily carry one.
# The file name also embeds a version that changes (vicione-suite_1.3.1~<pipeline id>), so neither
# the version nor the name can be hardcoded.
set -eu

out=${1:?usage: download-suite-deb.sh <output-path> [version]}
pinned=${2:-}
api="$CI_API_V4_URL/projects/$CI_PROJECT_ID/packages"
page_size=20

fetch() { curl -fsS --header "JOB-TOKEN: $CI_JOB_TOKEN" "$1"; }

# startswith("vicione-suite_") also keeps the debug symbols out (vicione-suite-dbgsym_...).
amd64_deb_of() {
  fetch "$api/$1/package_files?per_page=100" \
    | jq -r '[.[] | select(.file_name | startswith("vicione-suite_") and endswith("_amd64.deb"))] | last | .file_name // empty'
}

if [ -n "$pinned" ]; then
  # package_name is a fuzzy search, so match the name and version exactly here.
  packages=$(fetch "$api?package_type=generic&package_name=$PACKAGE_NAME&package_version=$pinned&status=default")
  ids=$(echo "$packages" | jq -r --arg name "$PACKAGE_NAME" --arg version "$pinned" \
    '[.[] | select(.name == $name and .version == $version)] | .[].id')
else
  packages=$(fetch "$api?package_type=generic&package_name=$PACKAGE_NAME&status=default&order_by=created_at&sort=desc&per_page=$page_size")
  ids=$(echo "$packages" | jq -r '.[].id')
fi

if [ -z "$ids" ]; then
  if [ -n "$pinned" ]; then
    echo "download-suite-deb: no generic package '$PACKAGE_NAME' with version $pinned in the registry" >&2
  else
    echo "download-suite-deb: no generic package '$PACKAGE_NAME' in the registry" >&2
  fi
  exit 1
fi

file=""
version=""
for id in $ids; do
  version=$(echo "$packages" | jq -r --argjson id "$id" '.[] | select(.id == $id) | .version')
  file=$(amd64_deb_of "$id")
  [ -n "$file" ] && break
  if [ -z "$pinned" ]; then
    echo "download-suite-deb: $PACKAGE_NAME $version carries no amd64 .deb (deb packaging did not run for it); trying the previous one"
  fi
done

if [ -z "$file" ]; then
  if [ -n "$pinned" ]; then
    echo "download-suite-deb: $PACKAGE_NAME $pinned carries no amd64 .deb — the deb packaging of this pipeline did not run or did not finish" >&2
  else
    echo "download-suite-deb: none of the newest $page_size '$PACKAGE_NAME' versions carries an amd64 .deb" >&2
  fi
  exit 1
fi

echo "download-suite-deb: resolved $PACKAGE_NAME $version -> $file"
curl -fL --header "JOB-TOKEN: $CI_JOB_TOKEN" -o "$out" "$api/generic/$PACKAGE_NAME/$version/$file"
