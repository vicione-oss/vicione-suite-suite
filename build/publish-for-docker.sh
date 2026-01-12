#!/usr/bin/env bash

#######################################
# Variables
#######################################

# defaults
MODULE='main'
PUBLISH_DIRECTORY="${1}"
PLATFORM="${2}"

#######################################
# Package handling
#######################################

PACKAGES_DIRECTORY=$(dirname $0)
source "${PACKAGES_DIRECTORY}/package_basic.sh"
source "${PACKAGES_DIRECTORY}/package_publish.sh"

# calculated
mkdir -p "${PUBLISH_DIRECTORY}" # need to be created in order to get the real path
ABSOLUTE_PUBLISH_DIRECTORY=$(realpath "${PUBLISH_DIRECTORY}")
ABSOLUTE_BASE_DIRECTORY=$(realpath "./")
VARIABLES_DIRECTORY=$(dirname $0)
if [[ -f "${VARIABLES_DIRECTORY}/.configuration" ]]; then source "${VARIABLES_DIRECTORY}/.configuration"; fi

#######################################
# Behaviour configuration
#######################################

set -o errexit
set -o nounset

prepare_app() {
  publish::core_deployment "${PLATFORM}" || basic::return 'failed to prepare suite.core.os'

  publish::core_os "${ABSOLUTE_PUBLISH_DIRECTORY}" "${PLATFORM}" || basic::return 'failed to publish suite.core.os'

  publish::modules "${ABSOLUTE_BASE_DIRECTORY}" "${ABSOLUTE_PUBLISH_DIRECTORY}" "${PLATFORM}" "MINIMAL" || basic::return 'failed to publish the modules'
}

#######################################
# Modules
#######################################

main() {
  prepare_app
}

eval "${MODULE}"
