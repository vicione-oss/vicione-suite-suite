#!/usr/bin/env bash

#######################################
# Variables
#######################################

# defaults
MODULE='main'
PUBLISH_DIRECTORY='./publish-linux'
PLATFORM='linux-x64'

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
ABSOLUTE_SAMPLES_DIRECTORY=$(realpath "./samples")
VARIABLES_DIRECTORY=$(dirname $0)
if [[ -f "${VARIABLES_DIRECTORY}/.configuration" ]]; then source "${VARIABLES_DIRECTORY}/.configuration"; fi

#######################################
# Behaviour configuration
#######################################

set -o errexit
set -o nounset

prepare_app() {
  publish::core_os "${ABSOLUTE_PUBLISH_DIRECTORY}" "${PLATFORM}" || basic::return 'failed to publish suite.core.os'

  publish::uihost_blazor_server "${ABSOLUTE_PUBLISH_DIRECTORY}" "${PLATFORM}" || basic::return 'failed to publish uihost blazor.server'

  publish::modules "${ABSOLUTE_BASE_DIRECTORY}" "${ABSOLUTE_PUBLISH_DIRECTORY}" "${PLATFORM}" "All" || basic::return 'failed to publish the modules'
}

# almost copy of the deploy/review-publish


#######################################
# Modules
#######################################

main() {
  prepare_app
}

eval "${MODULE}"
