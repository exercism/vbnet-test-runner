#!/usr/bin/env bash
# Write the shapes/ folder from the history of the exercism/vbnet track repo.
#
# A shape is a distinct xUnit v3 project file, ignoring the project name and the settings that do
# not affect compiling. This script reads every exercise's .vbproj in every commit since the track
# switched to xUnit v3, groups them by shape, and writes one representative project file per shape
# to <out_dir>/<n>/ExercismShape.vbproj, with net10.0 replaced by net11.0 as the runner does. The
# Dockerfile builds each one (bin/build-shapes.sh), so that the fast path can reuse its compiler
# arguments.
#
# The grouping here is close to ProjectShape.Key in the runner. If the two ever disagree, the worst
# case will be a duplicate template, or a project file with no matching template, which uses MSBuild.
#
# usage: generate-shapes.sh <out_dir> [path to the exercism/vbnet repo, default ../vbnet]
set -euo pipefail

if [ $# -lt 1 ]; then
  echo "usage: $0 <out_dir> [path to the exercism/vbnet repo]" >&2
  exit 1
fi
out=$1
track=${2:-../vbnet}
export LC_ALL=C

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
mkdir "$tmp/blobs" "$tmp/shapes"

# List "<blob> <exercise>" for every .vbproj in every commit, newest first, then HEAD.
{ git -C "$track" log --format=%H --since=2026-07-01 -- exercises; echo HEAD; } |
  while read -r commit; do
    git -C "$track" ls-tree -r "$commit" exercises |
      awk -F '\t' '$2 ~ /\.vbproj$/ { split($1, m, " "); split($2, p, "/"); print m[3], p[3] }'
  done > "$tmp/pairs"

# Print a project file's shape as sorted lines, one per element. It drops the wrapper tags and the
# settings that do not affect compiling, and fails on any line it does not understand, so that a new
# kind of project file is noticed.
shape_key() {
  sed -e 's/net10\.0/net11.0/g' -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' "$1" |
    grep -v -E '^$|^<Project |^</Project>$|^</?(PropertyGroup|ItemGroup)>$' |
    grep -v -E '^<(RestorePackagesWithLockFile|IsPackable|IsTestProject|RootNamespace)>' |
    while IFS= read -r line; do
      if [[ ! $line =~ ^\<[A-Za-z]+\>[^\<]*\</[A-Za-z]+\>$ && ! $line =~ ^\<[A-Za-z]+(\ [A-Za-z]+=\"[^\"]*\")*\ ?/\>$ ]]; then
        echo "generate-shapes.sh: cannot read this line of $1: $line" >&2
        exit 1
      fi
      echo "$line"
    done | sort
}

# Group each distinct blob by its shape. The first blob seen for a shape becomes its representative.
awk '!seen[$1]++ { print $1 }' "$tmp/pairs" | while read -r blob; do
  git -C "$track" cat-file blob "$blob" > "$tmp/blobs/$blob"
  grep -q 'xunit\.v3' "$tmp/blobs/$blob" || continue
  shape_key "$tmp/blobs/$blob" > "$tmp/key"
  id=$(git hash-object "$tmp/key")
  if [ ! -e "$tmp/shapes/$id" ]; then
    cp "$tmp/blobs/$blob" "$tmp/shapes/$id"
    tr '\n' '\001' < "$tmp/key" > "$tmp/shapes/$id.key"
  fi
  echo "$blob $id"
done > "$tmp/blob-shapes"

# Count the exercises for each shape, and order the shapes by that count (most first), then by key.
awk 'NR == FNR { shape[$1] = $2; next } $1 in shape { print shape[$1], $2 }' \
  "$tmp/blob-shapes" "$tmp/pairs" | sort -u | awk '{ print $1 }' | uniq -c |
  while read -r count id; do
    printf '%s\t%s\t%s\n' "$count" "$(cat "$tmp/shapes/$id.key")" "$id"
  done | sort -t "$(printf '\t')" -k1,1nr -k2,2 | cut -f 1,3 > "$tmp/order"

# Write one numbered folder per shape.
rm -rf "$out"
i=0
while read -r count id; do
  dir=$(printf '%s/%02d' "$out" "$i")
  mkdir -p "$dir"
  sed 's/net10\.0/net11.0/g' "$tmp/shapes/$id" > "$dir/ExercismShape.vbproj"
  echo "$i: $count exercises, $(grep -c '<PackageReference' "$dir/ExercismShape.vbproj") packages"
  i=$((i + 1))
done < "$tmp/order"
