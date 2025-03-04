#!/usr/bin/env bash

# Synopsis:
# Run the test runner on a solution.

# Arguments:
# $1: exercise slug
# $2: absolute path to solution folder
# $3: absolute path to output directory

# Output:
# Writes the test results to a results.json file in the passed-in output directory.
# The test results are formatted according to the specifications at https://github.com/exercism/docs/blob/main/building/tooling/test-runners/interface.md

# Example:
# ./bin/run.sh two-fer /absolute/path/to/two-fer/solution/folder/ /absolute/path/to/output/directory/

# If any required arguments is missing, print the usage and exit
if [ -z "$1" ] || [ -z "$2" ] || [ -z "$3" ]; then
    echo "usage: ./bin/run.sh exercise-slug /absolute/path/to/two-fer/solution/folder/ /absolute/path/to/output/directory/"
    exit 1
fi

slug="$1"
input_dir="${2%/}"
output_dir="${3%/}"
exercise=$(echo "${slug}" | awk -F'-' '{ for (i=1; i<=NF; i++) { $i = toupper(substr($i,1,1)) substr($i,2) } } 1' OFS='')
tests_file="${input_dir}/$(jq -r '.files.test[0]' "${input_dir}/.meta/config.json")"
tests_file_original="${tests_file}.original"
results_file="${output_dir}/results.json"
project_file="${input_dir}/${exercise}.vbproj"

# Create the output directory if it doesn't exist
mkdir -p "${output_dir}"

echo "${slug}: testing..."

cp "${tests_file}" "${tests_file_original}"

# Unskip tests
sed -i -E 's/Skip *:= *"Remove this Skip property to run this test"//' "${tests_file}"

# Fixup target framework
sed -i -E 's#<TargetFramework>(.+?)</TargetFramework>#<TargetFramework>net9.0</TargetFramework>#' "${project_file}"

pushd "${input_dir}" > /dev/null

# Run the tests for the provided implementation file and redirect stdout and
# stderr to capture it
test_output=$(dotnet restore --source /root/.nuget/packages/ && dotnet test -c release --no-restore 2>&1)
exit_code=$?

popd > /dev/null

# Restore the original file
mv -f "${tests_file_original}" "${tests_file}"

# Write the results.json file based on the exit code of the command that was 
# just executed that tested the implementation file
if [ ${exit_code} -eq 0 ]; then
    jq -n '{version: 1, status: "pass"}' > ${results_file}
else
    sanitized_test_output=$(printf "${test_output}" | sed -E '/  (All projects are up-to-date for restore.|Determining projects to restore\.\.\.|Restored \/.+?)/d')

    # Sanitize the output
    if grep -q "matched the specified pattern" <<< "${sanitized_test_output}" ; then
        sanitized_test_output=$(printf "${sanitized_test_output}" | sed -n -E -e '1,/matched the specified pattern.$/!p')
    fi

    jq -n --arg output "${sanitized_test_output}" '{version: 1, status: "fail", message: $output}' > ${results_file}
fi

echo "${slug}: done"
